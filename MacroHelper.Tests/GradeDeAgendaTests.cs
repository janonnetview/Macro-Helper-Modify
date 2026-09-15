using MacroHelper.Core.Agenda;
using MacroHelper.Core.Entities;

namespace MacroHelper.Tests;

/// <summary>
/// A aritmética da grade da semana.
///
/// O que ela resolve é uma coisa só: dois compromissos que se cruzam não podem ocupar o mesmo
/// espaço na tela, senão um esconde o outro e o choque de horário, que é o motivo da grade
/// existir, fica invisível. Estes testes falam de colunas paralelas porque é assim que a grade
/// mostra o choque.
/// </summary>
public class GradeDeAgendaTests
{
    private static readonly DateTime Quarta = new(2026, 8, 19);

    private static Compromisso Em(string hora, int? duracao = null,
                                  SituacaoCompromisso situacao = SituacaoCompromisso.Agendado) =>
        new()
        {
            Titulo         = $"Compromisso das {hora}",
            Quando         = Quarta.Add(TimeSpan.Parse(hora)),
            DuracaoMinutos = duracao,
            Situacao       = situacao,
        };

    [Fact]
    public void SemSeCruzar_TodosOcupamALarguraInteiraDoDia()
    {
        var blocos = GradeDeAgenda.Montar([Em("09:00", 60), Em("11:00", 60), Em("14:00", 30)]);

        Assert.All(blocos, b => Assert.Equal(1, b.TotalColunas));
        Assert.All(blocos, b => Assert.Equal(0, b.Coluna));
        Assert.All(blocos, b => Assert.False(b.Choca));
    }

    [Fact]
    public void DoisQueSeCruzam_FicamLadoALadoEMarcadosComoChoque()
    {
        var blocos = GradeDeAgenda.Montar([Em("09:00", 60), Em("09:30", 60)]);

        Assert.All(blocos, b => Assert.Equal(2, b.TotalColunas));
        Assert.Equal([0, 1], blocos.Select(b => b.Coluna));
        Assert.All(blocos, b => Assert.True(b.Choca));
    }

    /// <summary>
    /// A ponta solta do encadeamento: A cruza com B, B cruza com C, e A não encosta em C. Os
    /// três disputam a largura do dia mesmo assim, senão B ficaria espremido em cima dos dois.
    /// </summary>
    [Fact]
    public void OGrupoEhTransitivo_AindaQueAsPontasNaoSeToquem()
    {
        var blocos = GradeDeAgenda.Montar([Em("09:00", 60), Em("09:30", 60), Em("10:00", 60)]);

        Assert.All(blocos, b => Assert.Equal(2, b.TotalColunas));

        // A e C não se cruzam, então podem dividir a mesma coluna.
        Assert.Equal([0, 1, 0], blocos.Select(b => b.Coluna));
    }

    /// <summary>
    /// No encadeamento, cada um está em choque com ALGUM vizinho, e é isso que a marca diz.
    /// Das 9h e das 10h não se sobrepõem entre si (uma acaba quando a outra começa), mas as
    /// duas dividem horário com a das 9h30.
    /// </summary>
    [Fact]
    public void NoGrupoEncadeado_CadaUmEstaEmChoqueComAlgumVizinho()
    {
        var blocos = GradeDeAgenda.Montar([Em("09:00", 60), Em("09:30", 60), Em("10:00", 60)]);

        Assert.All(blocos, b => Assert.True(b.Choca));
        Assert.Equal(600, blocos[2].InicioMinutos);
    }

    /// <summary>
    /// Sem duração o compromisso ocupa o mínimo. É o MESMO mínimo do aviso de choque que o
    /// serviço dá ao salvar: se os dois números divergissem, a grade mostraria encavalamento
    /// sem aviso, ou aviso sem encavalamento.
    /// </summary>
    [Fact]
    public void SemDuracao_OcupaOMinimoEChocaComOutroNaMesmaHora()
    {
        var blocos = GradeDeAgenda.Montar([Em("10:00"), Em("10:00")]);

        Assert.All(blocos, b => Assert.Equal(GradeDeAgenda.DuracaoMinima, b.DuracaoMinutos));
        Assert.All(blocos, b => Assert.True(b.Choca));
    }

    [Fact]
    public void OCancelado_NaoOcupaLugarNaGrade()
    {
        var blocos = GradeDeAgenda.Montar(
        [
            Em("09:00", 60),
            Em("09:30", 60, SituacaoCompromisso.Cancelado),
        ]);

        var unico = Assert.Single(blocos);
        Assert.Equal(1, unico.TotalColunas);
        Assert.False(unico.Choca);
    }

    /// <summary>O realizado continua na grade (o dia aconteceu), mas não gera aviso de choque.</summary>
    [Fact]
    public void ORealizado_ApareceMasNaoEhChoque()
    {
        var blocos = GradeDeAgenda.Montar(
        [
            Em("09:00", 60),
            Em("09:30", 60, SituacaoCompromisso.Realizado),
        ]);

        Assert.Equal(2, blocos.Count);
        Assert.All(blocos, b => Assert.False(b.Choca));
    }

    [Fact]
    public void OQueNaoTemData_NaoEntraNaGrade()
    {
        var blocos = GradeDeAgenda.Montar([new Compromisso { Titulo = "A remarcar" }, Em("09:00", 60)]);

        Assert.Single(blocos);
    }

    /// <summary>
    /// A grade é de UM dia. Um compromisso das 23h com duas horas de duração seria desenhado
    /// por cima do que vier depois dele na tela, então ele para na meia-noite.
    /// </summary>
    [Fact]
    public void OQuePassaDaMeiaNoite_EhCortadoNoFimDoDia()
    {
        var bloco = GradeDeAgenda.Montar([Em("23:00", 120)]).Single();

        Assert.Equal(1380, bloco.InicioMinutos);
        Assert.Equal(1440, bloco.FimMinutos);
    }

    [Fact]
    public void AHoraViraMinutosDesdeAMeiaNoite()
    {
        var bloco = GradeDeAgenda.Montar([Em("14:30", 45)]).Single();

        Assert.Equal((14 * 60) + 30, bloco.InicioMinutos);
        Assert.Equal(45, bloco.DuracaoMinutos);
    }

    // ── O aviso do dia inteiro ───────────────────────────────────────────────

    [Fact]
    public void TemChoque_EhVerdadeiroQuandoDoisMarcadosDividemOHorario()
    {
        Assert.True(GradeDeAgenda.TemChoque([Em("09:00", 60), Em("09:30", 60)]));
        Assert.False(GradeDeAgenda.TemChoque([Em("09:00", 60), Em("11:00", 60)]));
    }

    [Fact]
    public void TemChoque_IgnoraOCanceladoEOQueNaoTemData()
    {
        Assert.False(GradeDeAgenda.TemChoque(
        [
            Em("09:00", 60),
            Em("09:30", 60, SituacaoCompromisso.Cancelado),
        ]));

        Assert.False(GradeDeAgenda.TemChoque([new Compromisso { Titulo = "A remarcar" }]));
    }
}
