using MacroHelper.Core.Entities;
using MacroHelper.Services;

namespace MacroHelper.Tests;

/// <summary>
/// As regras que separam um compromisso de uma tarefa com prazo: a hora é obrigatória, os
/// avisos são distâncias até ela, e mexer na hora tem de reposicionar tudo sozinho.
/// </summary>
public class CompromissoTests
{
    private static readonly DateTime QuartaAs9    = new(2026, 8, 19,  9, 0, 0);
    private static readonly DateTime QuintaAs14   = new(2026, 8, 20, 14, 0, 0);

    private static CompromissoService Montar(BancoDeTeste banco, DateTime agora) =>
        new(banco.Compromissos, new RelogioFalso(agora));

    private static Compromisso Visita(DateTime quando) => new()
    {
        Titulo                 = "Visita do técnico",
        Local                  = "Em casa",
        Quando                 = quando,
        AvisoAntecipadoMinutos = Antecedencia.PadraoAntecipado,
        AvisoNaHoraMinutos     = Antecedencia.PadraoNaHora,
    };

    // ── Validação ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SemTitulo_NaoSalva()
    {
        using var banco = new BancoDeTeste();
        var svc = Montar(banco, QuartaAs9);

        var (ok, msg, _) = await svc.SalvarAsync(new Compromisso { Quando = QuintaAs14 });

        Assert.False(ok);
        Assert.Contains("título", msg, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// O lembrete REMARCADO: existe, mas ainda não tem data nova.
    ///
    /// Era proibido até a 005, e a proibição custava caro: para gravar "a visita saiu de
    /// quinta e não sei quando volta" era preciso inventar uma data, e data inventada dispara
    /// aviso na hora errada. Guardar sem data é mais honesto do que mentir uma.
    ///
    /// Continua valendo que meia data não marca nada: dia sem hora, ou hora sem dia, é erro —
    /// mas isso é regra de FORMULÁRIO, porque só ele conhece os dois campos separados. Ver
    /// CompromissosViewTests.SalvarSemHora_ReclamaNoFormulario.
    /// </summary>
    [Fact]
    public async Task SemData_Salva_ESemAviso()
    {
        using var banco = new BancoDeTeste();
        var svc = Montar(banco, QuartaAs9);

        var (ok, _, salvo) = await svc.SalvarAsync(new Compromisso
        {
            Titulo                 = "Visita do técnico, a remarcar",
            AvisoAntecipadoMinutos = Antecedencia.PadraoAntecipado,
            AvisoNaHoraMinutos     = Antecedencia.PadraoNaHora,
        });

        Assert.True(ok);
        Assert.NotNull(salvo);
        Assert.Null(salvo!.Quando);
        Assert.False(salvo.TemData);

        // Sem instante não há de onde descontar a antecedência: os avisos que vieram do
        // formulário são descartados, e não guardados à espera de uma data.
        Assert.Null(salvo.AvisoAntecipadoMinutos);
        Assert.Null(salvo.AvisoNaHoraMinutos);

        var relido = Assert.Single(await svc.ObterTodosAsync());
        Assert.Null(relido.Quando);
    }

    /// <summary>
    /// O que não tem data não venceu, não choca com nada e não aparece no "hoje": ele está
    /// esperando decisão, e mandá-lo para o histórico enterraria justamente isso.
    /// </summary>
    [Fact]
    public async Task SemData_NaoVence_NaoChoca_ENaoEDeHoje()
    {
        using var banco = new BancoDeTeste();
        var svc = Montar(banco, QuintaAs14);

        var porMarcar = new Compromisso { Titulo = "A remarcar" };
        var comHora   = Visita(QuintaAs14);

        Assert.False(porMarcar.Passou(QuintaAs14.AddYears(5)));
        Assert.False(porMarcar.ParaHoje(QuintaAs14));
        Assert.False(porMarcar.ChocaCom(comHora));
        Assert.False(comHora.ChocaCom(porMarcar));
        Assert.Equal("sem data marcada", porMarcar.ContagemTexto(QuintaAs14));

        await svc.SalvarAsync(porMarcar);
        await svc.SalvarAsync(comHora);

        // Não é "de hoje", mas continua na lista do que ainda vem: some da agenda do dia,
        // não da vista onde se decide o que fazer com ele.
        Assert.DoesNotContain(await svc.ObterDeHojeAsync(), c => c.Titulo == "A remarcar");
        Assert.Contains(await svc.PesquisarAsync("remarcar"), c => c.Titulo == "A remarcar");
    }

    [Fact]
    public async Task DuracaoZero_ViraAusenciaDeDuracao()
    {
        using var banco = new BancoDeTeste();
        var svc = Montar(banco, QuartaAs9);

        var (_, _, salvo) = await svc.SalvarAsync(
            new Compromisso { Titulo = "Entregar o documento", Quando = QuintaAs14, DuracaoMinutos = 0 });

        Assert.Null(salvo!.DuracaoMinutos);
        Assert.Equal("14:00", salvo.HorarioTexto);
    }

    // ── Choque de horário ────────────────────────────────────────────────────

    /// <summary>Avisa, e salva assim mesmo: duas coisas ao mesmo tempo às vezes são de propósito.</summary>
    [Fact]
    public async Task DoisNoMesmoHorario_SalvaMasAvisaDoChoque()
    {
        using var banco = new BancoDeTeste();
        var svc = Montar(banco, QuartaAs9);

        await svc.SalvarAsync(new Compromisso
        {
            Titulo = "Reunião de equipe", Quando = QuintaAs14, DuracaoMinutos = 60,
        });

        var (ok, msg, salvo) = await svc.SalvarAsync(new Compromisso
        {
            Titulo = "Visita do técnico", Quando = QuintaAs14.AddMinutes(30),
        });

        Assert.True(ok);
        Assert.NotNull(salvo);
        Assert.Contains("Reunião de equipe", msg, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HorariosQueNaoSeCruzam_NaoAvisamNada()
    {
        using var banco = new BancoDeTeste();
        var svc = Montar(banco, QuartaAs9);

        await svc.SalvarAsync(new Compromisso
        {
            Titulo = "Reunião de equipe", Quando = QuintaAs14, DuracaoMinutos = 60,
        });

        var (ok, msg, _) = await svc.SalvarAsync(new Compromisso
        {
            Titulo = "Visita do técnico", Quando = QuintaAs14.AddHours(3),
        });

        Assert.True(ok);
        Assert.DoesNotContain("Atenção", msg, StringComparison.Ordinal);
    }

    /// <summary>Um compromisso cancelado não ocupa mais lugar nenhum na agenda.</summary>
    [Fact]
    public async Task CanceladoNaoEntraNoChoque()
    {
        using var banco = new BancoDeTeste();
        var svc = Montar(banco, QuartaAs9);

        var (_, _, primeiro) = await svc.SalvarAsync(new Compromisso
        {
            Titulo = "Reunião desmarcada", Quando = QuintaAs14, DuracaoMinutos = 60,
        });
        await svc.DefinirSituacaoAsync(primeiro!.Id, SituacaoCompromisso.Cancelado);

        var (_, msg, _) = await svc.SalvarAsync(new Compromisso
        {
            Titulo = "Visita do técnico", Quando = QuintaAs14,
        });

        Assert.DoesNotContain("Reunião desmarcada", msg, StringComparison.Ordinal);
    }

    // ── Rearme dos avisos ────────────────────────────────────────────────────

    /// <summary>
    /// O caso que faz o módulo inteiro valer: a visita foi remarcada. Se os avisos continuassem
    /// marcados como dados, o compromisso novo nunca avisaria — em silêncio, que é o pior jeito
    /// de falhar num app cuja única função ali é avisar.
    /// </summary>
    [Fact]
    public async Task MudarAHora_RearmaOsDoisAvisos()
    {
        using var banco = new BancoDeTeste();
        var svc = Montar(banco, QuartaAs9);

        var (_, _, salvo) = await svc.SalvarAsync(Visita(QuintaAs14));

        // Simula os dois avisos já dados.
        await svc.MarcarAvisoDisparadoAsync(salvo!.Id, antecipado: true);
        await svc.MarcarAvisoDisparadoAsync(salvo.Id, antecipado: false);

        var remarcado = await svc.ObterPorIdAsync(salvo.Id);
        remarcado!.Quando = QuintaAs14.AddDays(1);
        await svc.SalvarAsync(remarcado);

        var depois = await svc.ObterPorIdAsync(salvo.Id);
        Assert.False(depois!.AvisoAntecipadoDisparado);
        Assert.False(depois.AvisoNaHoraDisparado);
    }

    [Fact]
    public async Task MudarSoOTitulo_NaoRearmaNada()
    {
        using var banco = new BancoDeTeste();
        var svc = Montar(banco, QuartaAs9);

        var (_, _, salvo) = await svc.SalvarAsync(Visita(QuintaAs14));
        await svc.MarcarAvisoDisparadoAsync(salvo!.Id, antecipado: true);

        var editado = await svc.ObterPorIdAsync(salvo.Id);
        editado!.Titulo = "Visita do técnico (2ª tentativa)";
        await svc.SalvarAsync(editado);

        Assert.True((await svc.ObterPorIdAsync(salvo.Id))!.AvisoAntecipadoDisparado);
    }

    [Fact]
    public async Task TrocarAAntecedencia_RearmaSoAqueleAviso()
    {
        using var banco = new BancoDeTeste();
        var svc = Montar(banco, QuartaAs9);

        var (_, _, salvo) = await svc.SalvarAsync(Visita(QuintaAs14));
        await svc.MarcarAvisoDisparadoAsync(salvo!.Id, antecipado: true);
        await svc.MarcarAvisoDisparadoAsync(salvo.Id, antecipado: false);

        var editado = await svc.ObterPorIdAsync(salvo.Id);
        editado!.AvisoNaHoraMinutos = 10;
        await svc.SalvarAsync(editado);

        var depois = await svc.ObterPorIdAsync(salvo.Id);
        Assert.True(depois!.AvisoAntecipadoDisparado);
        Assert.False(depois.AvisoNaHoraDisparado);
    }

    /// <summary>
    /// Marcar "hoje às 14h, avisar 1 dia antes" às 9h da manhã não pode gerar um balão trinta
    /// segundos depois do Salvar: o aviso de véspera já passou. O de meia hora antes continua
    /// armado, porque esse ainda está no futuro.
    /// </summary>
    [Fact]
    public async Task AvisoQueJaPassouNoMomentoDeSalvar_NasceSilenciado()
    {
        using var banco = new BancoDeTeste();
        var svc = Montar(banco, QuartaAs9);

        var (_, _, salvo) = await svc.SalvarAsync(Visita(QuartaAs9.AddHours(5)));

        Assert.True(salvo!.AvisoAntecipadoDisparado);
        Assert.False(salvo.AvisoNaHoraDisparado);
    }

    // ── Busca do Ctrl+Espaço ─────────────────────────────────────────────────

    /// <summary>Como a busca de tarefas: "tecnico" tem de achar "técnico".</summary>
    [Fact]
    public async Task Pesquisar_IgnoraAcentoECaixa()
    {
        using var banco = new BancoDeTeste();
        var svc = Montar(banco, QuartaAs9);

        await svc.SalvarAsync(Visita(QuintaAs14));

        Assert.Single(await svc.PesquisarAsync("TECNICO"));
        Assert.Single(await svc.PesquisarAsync("técnico"));
        Assert.Empty(await svc.PesquisarAsync("dentista"));
    }

    /// <summary>O local e as observações também entram na busca — é por onde se acha "sala do gestor".</summary>
    [Fact]
    public async Task Pesquisar_OlhaTambemOLocal()
    {
        using var banco = new BancoDeTeste();
        var svc = Montar(banco, QuartaAs9);

        await svc.SalvarAsync(new Compromisso
        {
            Titulo = "Entregar o documento", Local = "Sala do gestor", Quando = QuintaAs14,
        });

        Assert.Single(await svc.PesquisarAsync("gestor"));
    }

    /// <summary>
    /// A ordem do Ctrl+Espaço é diferente da ordem da tela: primeiro o que ainda vem, do mais
    /// próximo para o mais distante, e só depois o que já passou. Quem abre a busca quer o
    /// próximo lembrete no topo, não o mais antigo do banco.
    /// </summary>
    [Fact]
    public async Task Pesquisar_PoeOQueAindaVemNoTopo()
    {
        using var banco = new BancoDeTeste();
        var svc = Montar(banco, QuartaAs9);

        await svc.SalvarAsync(new Compromisso { Titulo = "Semana que vem", Quando = QuintaAs14.AddDays(7) });
        await svc.SalvarAsync(new Compromisso { Titulo = "Ja passou",      Quando = QuartaAs9.AddHours(-2) });
        await svc.SalvarAsync(new Compromisso { Titulo = "Amanha",         Quando = QuintaAs14 });

        var achados = (await svc.PesquisarAsync(string.Empty)).Select(c => c.Titulo).ToList();

        Assert.Equal(["Amanha", "Semana que vem", "Ja passou"], achados);
    }

    /// <summary>Um lembrete cancelado desce junto com o passado: ele não vai mais acontecer.</summary>
    [Fact]
    public async Task Pesquisar_CanceladoNaoFicaNoTopo()
    {
        using var banco = new BancoDeTeste();
        var svc = Montar(banco, QuartaAs9);

        var (_, _, cancelado) = await svc.SalvarAsync(
            new Compromisso { Titulo = "Cancelado", Quando = QuintaAs14 });
        await svc.DefinirSituacaoAsync(cancelado!.Id, SituacaoCompromisso.Cancelado);

        await svc.SalvarAsync(new Compromisso { Titulo = "De pe", Quando = QuintaAs14.AddDays(3) });

        var achados = (await svc.PesquisarAsync(string.Empty)).Select(c => c.Titulo).ToList();

        Assert.Equal(["De pe", "Cancelado"], achados);
    }

    // ── Situação ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancelar_MantemORegistroNaLista()
    {
        using var banco = new BancoDeTeste();
        var svc = Montar(banco, QuartaAs9);

        var (_, _, salvo) = await svc.SalvarAsync(Visita(QuintaAs14));
        await svc.DefinirSituacaoAsync(salvo!.Id, SituacaoCompromisso.Cancelado);

        var depois = await svc.ObterPorIdAsync(salvo.Id);
        Assert.True(depois!.Cancelado);
        Assert.Single(await svc.ObterTodosAsync());
    }

    [Fact]
    public async Task ObterDeHoje_TrazSoOsAgendadosDeHoje()
    {
        using var banco = new BancoDeTeste();
        var svc = Montar(banco, QuartaAs9);

        await svc.SalvarAsync(new Compromisso { Titulo = "Hoje",   Quando = QuartaAs9.AddHours(6) });
        await svc.SalvarAsync(new Compromisso { Titulo = "Amanhã", Quando = QuintaAs14 });

        var (_, _, cancelado) = await svc.SalvarAsync(
            new Compromisso { Titulo = "Hoje, mas cancelado", Quando = QuartaAs9.AddHours(7) });
        await svc.DefinirSituacaoAsync(cancelado!.Id, SituacaoCompromisso.Cancelado);

        var hoje = (await svc.ObterDeHojeAsync()).ToList();

        Assert.Single(hoje);
        Assert.Equal("Hoje", hoje[0].Titulo);
    }

    // ── Texto (é o que a pessoa lê no cartão e no balão) ─────────────────────

    [Theory]
    [InlineData(0,     "na hora")]
    [InlineData(10,    "10 min antes")]
    [InlineData(30,    "30 min antes")]
    [InlineData(60,    "1 hora antes")]
    [InlineData(180,   "3 horas antes")]
    [InlineData(1440,  "1 dia antes")]
    [InlineData(2880,  "2 dias antes")]
    [InlineData(10080, "1 semana antes")]
    public void Antecedencia_ViraTextoDeGente(int minutos, string esperado)
        => Assert.Equal(esperado, Antecedencia.Texto(minutos));

    [Fact]
    public void AvisosTexto_DizOQueVaiAcontecer()
    {
        Assert.Equal("Avisa 1 dia antes e 30 min antes", Visita(QuintaAs14).AvisosTexto);

        Assert.Equal("Sem aviso", new Compromisso { Quando = QuintaAs14 }.AvisosTexto);
    }

    [Fact]
    public void HorarioTexto_MostraOIntervaloSoQuandoHaDuracao()
    {
        Assert.Equal("14:00", new Compromisso { Quando = QuintaAs14 }.HorarioTexto);

        Assert.Equal("14:00 às 15:30",
            new Compromisso { Quando = QuintaAs14, DuracaoMinutos = 90 }.HorarioTexto);
    }

    /// <summary>
    /// O balão diz quanto FALTA, não que horas são: é a informação que faz alguém se levantar
    /// da cadeira sem precisar fazer a conta.
    /// </summary>
    [Fact]
    public void ContagemTexto_ContaOTempoQueFalta()
    {
        var compromisso = new Compromisso { Quando = QuintaAs14 };

        Assert.Equal("agora",           compromisso.ContagemTexto(QuintaAs14));
        Assert.Equal("agora",           compromisso.ContagemTexto(QuintaAs14.AddMinutes(10)));
        Assert.Equal("em 25 min",       compromisso.ContagemTexto(QuintaAs14.AddMinutes(-25)));
        Assert.Equal("hoje às 14:00",   compromisso.ContagemTexto(QuintaAs14.AddHours(-3)));
        Assert.Equal("amanhã às 14:00", compromisso.ContagemTexto(QuintaAs14.AddDays(-1)));
        Assert.Equal("Qui 20/08 às 14:00", compromisso.ContagemTexto(QuintaAs14.AddDays(-3)));
    }

    /// <summary>Sem duração, dois compromissos na mesma hora ainda são um choque.</summary>
    [Fact]
    public void ChocaCom_ConsideraUmMinimoParaQuemNaoTemDuracao()
    {
        var entrega = new Compromisso { Quando = QuintaAs14 };
        var outro   = new Compromisso { Quando = QuintaAs14.AddMinutes(5) };
        var tarde   = new Compromisso { Quando = QuintaAs14.AddHours(2) };

        Assert.True(entrega.ChocaCom(outro));
        Assert.False(entrega.ChocaCom(tarde));
    }
}
