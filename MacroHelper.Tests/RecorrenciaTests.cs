using MacroHelper.Core.Entities;
using MacroHelper.Services;

namespace MacroHelper.Tests;

/// <summary>
/// O cálculo da próxima ocorrência — sem banco e sem esperar o calendário passar.
///
/// A âncora dos testes é uma quinta-feira, 20/08/2026, para que sábado e domingo apareçam nos
/// casos de dias úteis sem ninguém precisar contar nos dedos ao ler.
/// </summary>
public class RecorrenciaTests
{
    private static readonly DateTime Quinta = new(2026, 8, 20, 9, 0, 0);

    [Fact]
    public void SemRecorrencia_NaoTemProxima()
    {
        Assert.Null(Recorrencia.Proxima(RecorrenciaTarefa.Nenhuma, Quinta, Quinta));
        Assert.Null(Recorrencia.ProximaOcorrencia(new Tarefa { Titulo = "Solta" }, Quinta));
    }

    [Fact]
    public void Diaria_VaiParaODiaSeguinte_PreservandoOHorario()
    {
        var proxima = Recorrencia.Proxima(RecorrenciaTarefa.Diaria, Quinta, Quinta);
        Assert.Equal(new DateTime(2026, 8, 21, 9, 0, 0), proxima);
    }

    /// <summary>
    /// Concluir às 15h um lembrete que era para as 9h de hoje manda a próxima para AMANHÃ às
    /// 9h — não para daqui a pouco. A comparação é por dia justamente por isso.
    /// </summary>
    [Fact]
    public void Diaria_ConcluidaMaisTardeNoMesmoDia_VaiParaAmanha()
    {
        var proxima = Recorrencia.Proxima(RecorrenciaTarefa.Diaria, Quinta, Quinta.Date.AddHours(15));
        Assert.Equal(new DateTime(2026, 8, 21, 9, 0, 0), proxima);
    }

    /// <summary>
    /// O caso que evita a fila de tarefas vencidas: uma semanal esquecida por três semanas vai
    /// para a PRÓXIMA semana, e não para a semana passada. Sem isto, concluí-la geraria uma
    /// ocorrência já atrasada, que teria de ser concluída de novo, e de novo.
    /// </summary>
    [Fact]
    public void Semanal_ConcluidaComTresSemanasDeAtraso_CaiNoFuturo()
    {
        var agora   = Quinta.AddDays(20);
        var proxima = Recorrencia.Proxima(RecorrenciaTarefa.Semanal, Quinta, agora);

        Assert.True(proxima > agora);
        Assert.Equal(new DateTime(2026, 9, 10, 9, 0, 0), proxima);   // 20/08 + 21 dias
        Assert.Equal(DayOfWeek.Thursday, proxima!.Value.DayOfWeek);  // continua na quinta
    }

    [Fact]
    public void Quinzenal_AndaDeQuatorzeEmQuatorze()
    {
        var proxima = Recorrencia.Proxima(RecorrenciaTarefa.Quinzenal, Quinta, Quinta);
        Assert.Equal(new DateTime(2026, 9, 3, 9, 0, 0), proxima);
    }

    /// <summary>Sexta + 1 dia útil é segunda: o fim de semana é pulado, não acumulado.</summary>
    [Fact]
    public void DiasUteis_DeSextaVaiParaSegunda()
    {
        var sexta   = new DateTime(2026, 8, 21, 9, 0, 0);
        var proxima = Recorrencia.Proxima(RecorrenciaTarefa.DiasUteis, sexta, sexta);

        Assert.Equal(DayOfWeek.Monday, proxima!.Value.DayOfWeek);
        Assert.Equal(new DateTime(2026, 8, 24, 9, 0, 0), proxima);
    }

    /// <summary>
    /// O dominó do dia 31. Somando mês a mês a partir da ocorrência anterior, 31/jan viraria
    /// 28/fev e daí em diante TODO mês seria dia 28. Calculando sempre a partir da data
    /// original, o 31 é aparado só onde o mês não tem e volta no mês seguinte.
    /// </summary>
    [Fact]
    public void Mensal_DiaTrintaEUm_NaoPerdeODiaDepoisDeFevereiro()
    {
        var janeiro = new DateTime(2026, 1, 31, 8, 0, 0);

        var fevereiro = Recorrencia.Proxima(RecorrenciaTarefa.Mensal, janeiro, janeiro);
        Assert.Equal(new DateTime(2026, 2, 28, 8, 0, 0), fevereiro);

        // A partir de janeiro de novo, dois meses adiante: o dia 31 continua lá.
        var marco = Recorrencia.Proxima(RecorrenciaTarefa.Mensal, janeiro, new DateTime(2026, 2, 28, 8, 0, 0));
        Assert.Equal(new DateTime(2026, 3, 31, 8, 0, 0), marco);
    }

    [Fact]
    public void Anual_AndaUmAno()
    {
        var proxima = Recorrencia.Proxima(RecorrenciaTarefa.Anual, Quinta, Quinta);
        Assert.Equal(new DateTime(2027, 8, 20, 9, 0, 0), proxima);
    }

    /// <summary>
    /// Prazo e lembrete andam JUNTOS, pelo mesmo deslocamento. É o que preserva "me avise na
    /// véspera às 9h": recalculando os dois em separado, o lembrete escorregaria para depois do
    /// prazo em todo mês de tamanho diferente.
    /// </summary>
    [Fact]
    public void ProximaOcorrencia_DeslocaPrazoELembreteJuntos()
    {
        var tarefa = new Tarefa
        {
            Titulo      = "Enviar relatório",
            Recorrencia = RecorrenciaTarefa.Semanal,
            Prazo       = new DateTime(2026, 8, 20, 0, 0, 0),
            Lembrete    = new DateTime(2026, 8, 19, 9, 0, 0),   // véspera, 9h
        };

        var distanciaOriginal = tarefa.Prazo!.Value - tarefa.Lembrete!.Value;

        var proxima = Recorrencia.ProximaOcorrencia(tarefa, Quinta)!;

        Assert.Equal(new DateTime(2026, 8, 27), proxima.Prazo);
        Assert.Equal(new DateTime(2026, 8, 26, 9, 0, 0), proxima.Lembrete);

        // O invariante: a distância entre lembrete e prazo é a MESMA de antes.
        Assert.Equal(distanciaOriginal, proxima.Prazo!.Value - proxima.Lembrete!.Value);
    }

    /// <summary>A nova ocorrência nasce aberta e com a notificação rearmada.</summary>
    [Fact]
    public void ProximaOcorrencia_NasceAbertaEComLembreteRearmado()
    {
        var tarefa = new Tarefa
        {
            Titulo            = "Backup",
            Recorrencia       = RecorrenciaTarefa.Diaria,
            Prazo             = Quinta.Date,
            Lembrete          = Quinta,
            Concluida         = true,
            LembreteDisparado = true,
            DataConclusao     = Quinta,
        };

        var proxima = Recorrencia.ProximaOcorrencia(tarefa, Quinta)!;

        Assert.False(proxima.Concluida);
        Assert.False(proxima.LembreteDisparado);
        Assert.Null(proxima.DataConclusao);
        Assert.Equal(0, proxima.Id);
    }

    /// <summary>
    /// Recorrente sem data nenhuma continua sem data nenhuma — "todo dia: revisar a caixa de
    /// entrada" não ganha um prazo que ninguém pediu só por ser recorrente.
    /// </summary>
    [Fact]
    public void ProximaOcorrencia_SemPrazoNemLembrete_NaoInventaDatas()
    {
        var tarefa = new Tarefa { Titulo = "Revisar caixa", Recorrencia = RecorrenciaTarefa.Diaria };

        var proxima = Recorrencia.ProximaOcorrencia(tarefa, Quinta)!;

        Assert.Null(proxima.Prazo);
        Assert.Null(proxima.Lembrete);
        Assert.Equal(RecorrenciaTarefa.Diaria, proxima.Recorrencia);
    }

    /// <summary>Sem prazo, a âncora é o lembrete — e é ele que anda.</summary>
    [Fact]
    public void ProximaOcorrencia_SemPrazo_AncoraNoLembrete()
    {
        var tarefa = new Tarefa
        {
            Titulo      = "Tomar remédio",
            Recorrencia = RecorrenciaTarefa.Diaria,
            Lembrete    = new DateTime(2026, 8, 20, 22, 0, 0),
        };

        var proxima = Recorrencia.ProximaOcorrencia(tarefa, Quinta)!;

        Assert.Equal(new DateTime(2026, 8, 21, 22, 0, 0), proxima.Lembrete);
    }
}
