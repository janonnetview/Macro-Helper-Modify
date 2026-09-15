using MacroHelper.Core.Entities;

namespace MacroHelper.Services;

/// <summary>
/// Quando é a próxima vez.
///
/// Cálculo puro, sem banco e sem relógio implícito: o "agora" entra por parâmetro, então dá
/// para afirmar sobre uma tarefa mensal concluída com três meses de atraso sem esperar três
/// meses. É a mesma razão de <see cref="IRelogio"/> existir.
/// </summary>
public static class Recorrencia
{
    /// <summary>
    /// A próxima ocorrência depois de <paramref name="baseData"/>, sempre no futuro em
    /// relação a <paramref name="agora"/>.
    ///
    /// Concluir com atraso não gera uma ocorrência já vencida: uma tarefa semanal marcada
    /// como feita três semanas depois vai para a próxima semana, não para a semana passada.
    /// É o que Todoist e o To Do fazem, e é a única leitura que não obriga a concluir a mesma
    /// tarefa várias vezes seguidas só para "alcançar" o presente.
    ///
    /// O horário de <paramref name="baseData"/> é preservado: um lembrete das 9h continua às
    /// 9h. A comparação com <paramref name="agora"/> é por DIA — concluir às 15h um lembrete
    /// que era para as 9h de hoje manda a próxima para amanhã de manhã, e não para daqui a
    /// pouco.
    /// </summary>
    /// <returns>null quando não há recorrência.</returns>
    public static DateTime? Proxima(RecorrenciaTarefa recorrencia, DateTime baseData, DateTime agora)
    {
        var diff = (agora.Date - baseData.Date).Days;

        return recorrencia switch
        {
            RecorrenciaTarefa.Diaria    => PorDias(baseData, diff, 1),
            RecorrenciaTarefa.Semanal   => PorDias(baseData, diff, 7),
            RecorrenciaTarefa.Quinzenal => PorDias(baseData, diff, 14),
            RecorrenciaTarefa.DiasUteis => ProximoDiaUtil(baseData, agora),
            RecorrenciaTarefa.Mensal    => PorMesOuAno(baseData, agora, meses: 1),
            RecorrenciaTarefa.Anual     => PorMesOuAno(baseData, agora, meses: 12),
            _                           => null,
        };
    }

    /// <summary>
    /// Intervalo fixo: dá para achar o salto por conta em vez de somar de um em um. Uma tarefa
    /// diária esquecida por dois anos custaria 700 voltas de laço; aqui custa uma divisão.
    /// </summary>
    private static DateTime PorDias(DateTime baseData, int diff, int passo)
    {
        // k é quantos passos cabem no atraso, mais um — e nunca menos que 1, para que uma
        // tarefa concluída ANTES do prazo ainda ande para a ocorrência seguinte.
        var k = Math.Max(1, diff / passo + 1);
        return baseData.AddDays((long)k * passo);
    }

    /// <summary>
    /// Segunda a sexta. Sábado e domingo não são "adiados" para segunda: são pulados, então
    /// uma tarefa de sexta cai na segunda e não numa segunda-feira acumulada com o fim de
    /// semana inteiro.
    /// </summary>
    private static DateTime ProximoDiaUtil(DateTime baseData, DateTime agora)
    {
        var data = baseData;

        // O limite é uma trava contra data corrompida no banco, não uma regra de negócio:
        // 40 anos de dias úteis, e o laço não pode prender a UI de jeito nenhum.
        for (var passos = 0; passos < 10_000; passos++)
        {
            data = data.AddDays(1);
            if (data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            if (data.Date > agora.Date) return data;
        }

        return data;
    }

    /// <summary>
    /// Mensal e anual andam pelo CALENDÁRIO, não por 30 ou 365 dias — "todo dia 5" tem de cair
    /// no dia 5.
    ///
    /// O dia de origem fica guardado e cada candidato é calculado a partir da data ORIGINAL,
    /// nunca da anterior. É o que impede o efeito dominó do dia 31: somando mês a mês,
    /// 31/jan viraria 28/fev e daí em diante todo mês seria dia 28. Aqui 31/jan → 28/fev →
    /// 31/mar, com o 31 preservado e aparado só onde o mês não tem.
    /// </summary>
    private static DateTime PorMesOuAno(DateTime baseData, DateTime agora, int meses)
    {
        var dia = baseData.Day;

        for (var n = 1; n < 1_200; n++)
        {
            var candidato = ComDia(baseData.AddMonths(n * meses), dia);
            if (candidato.Date > agora.Date) return candidato;
        }

        return baseData.AddMonths(meses);
    }

    /// <summary>O mesmo instante, no dia pedido — aparado ao último dia do mês quando ele não existe.</summary>
    private static DateTime ComDia(DateTime data, int dia)
    {
        var ultimo = DateTime.DaysInMonth(data.Year, data.Month);
        return new DateTime(data.Year, data.Month, Math.Min(dia, ultimo),
                            data.Hour, data.Minute, data.Second);
    }

    /// <summary>
    /// A próxima ocorrência de uma tarefa, pronta para gravar: prazo e lembrete deslocados
    /// juntos, conclusão limpa e lembrete rearmado.
    ///
    /// Prazo e lembrete andam pelo MESMO deslocamento, e é isso que preserva a distância entre
    /// os dois. Quem pede para ser avisado na véspera às 9h continua sendo avisado na véspera
    /// às 9h da ocorrência seguinte — recalcular os dois em separado jogaria o lembrete para
    /// depois do prazo em todo mês de tamanho diferente.
    /// </summary>
    /// <returns>null quando a tarefa não é recorrente.</returns>
    public static Tarefa? ProximaOcorrencia(Tarefa concluida, DateTime agora)
    {
        if (!concluida.Recorrente) return null;

        // A âncora é o prazo; sem prazo, o lembrete; sem nenhum dos dois, o momento da
        // conclusão — uma tarefa recorrente sem data ainda precisa voltar amanhã.
        var ancora = concluida.Prazo ?? concluida.Lembrete ?? agora;
        var nova   = Proxima(concluida.Recorrencia, ancora, agora);
        if (nova == null) return null;

        var deslocamento = nova.Value - ancora;

        return new Tarefa
        {
            Titulo      = concluida.Titulo,
            Observacoes = concluida.Observacoes,
            Prioridade  = concluida.Prioridade,
            Recorrencia = concluida.Recorrencia,

            // Só desloca o que existia: uma tarefa sem prazo continua sem prazo.
            Prazo    = concluida.Prazo?.Add(deslocamento),
            Lembrete = concluida.Lembrete?.Add(deslocamento),

            Concluida         = false,
            LembreteDisparado = false,
            DataCriacao       = agora,
        };
    }
}
