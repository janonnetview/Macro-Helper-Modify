using MacroHelper.Core.Entities;
using MacroHelper.Services;
using System.Windows.Threading;

namespace MacroHelper.Tests;

/// <summary>
/// O comportamento que justifica a tela de Compromissos existir: ser avisado ANTES, na
/// antecedência escolhida, e uma vez só por aviso — inclusive quando o app passou o dia
/// fechado.
///
/// Como em <see cref="LembreteTests"/>, tudo roda por <c>VarrerCompromissosAsync</c>, sem o
/// <c>DispatcherTimer</c>: o timer só decide QUANDO a varredura roda, e o relógio falso
/// substitui isso.
/// </summary>
public class AvisoDeCompromissoTests
{
    private static readonly DateTime QuartaAs9  = new(2026, 8, 19,  9, 0, 0);
    private static readonly DateTime QuintaAs14 = new(2026, 8, 20, 14, 0, 0);

    private static (LembreteService Lembretes, CompromissoService Compromissos, RelogioFalso Relogio)
        Montar(BancoDeTeste banco, DateTime agora)
    {
        var relogio      = new RelogioFalso(agora);
        var compromissos = new CompromissoService(banco.Compromissos, relogio);
        var lembretes    = new LembreteService(
            new TarefaService(banco.Tarefas, relogio), compromissos,
            relogio, Dispatcher.CurrentDispatcher);

        return (lembretes, compromissos, relogio);
    }

    /// <summary>Visita marcada para quinta às 14h, avisos de véspera e de meia hora antes.</summary>
    private static Compromisso Visita() => new()
    {
        Titulo                 = "Visita do técnico",
        Local                  = "Em casa",
        Quando                 = QuintaAs14,
        AvisoAntecipadoMinutos = Antecedencia.PadraoAntecipado,
        AvisoNaHoraMinutos     = Antecedencia.PadraoNaHora,
    };

    [Fact]
    public async Task AntesDaAntecedencia_NaoAvisaNada()
    {
        using var banco = new BancoDeTeste();
        var (lembretes, compromissos, _) = Montar(banco, QuartaAs9);

        await compromissos.SalvarAsync(Visita());

        // Quarta de manhã: o aviso de véspera só sai às 14h de quarta.
        Assert.Empty(await lembretes.VarrerCompromissosAsync());
    }

    [Fact]
    public async Task NaVespera_SaiOAvisoDeFolgaESoEle()
    {
        using var banco = new BancoDeTeste();
        var (lembretes, compromissos, relogio) = Montar(banco, QuartaAs9);

        await compromissos.SalvarAsync(Visita());

        relogio.Agora = QuintaAs14.AddDays(-1).AddMinutes(1);

        var avisados = new List<AvisoVencido>();
        lembretes.AvisosDeCompromisso += a => avisados.AddRange(a);

        var saiu = await lembretes.VarrerCompromissosAsync();

        Assert.Single(saiu);
        Assert.True(saiu[0].Antecipado);
        Assert.Equal("Visita do técnico", saiu[0].Compromisso.Titulo);

        // O evento também disparou — é ele que a MainWindow escuta para mostrar o balão.
        Assert.Single(avisados);

        // E o texto do balão conta o tempo que falta, não o relógio.
        Assert.Equal("amanhã às 14:00", saiu[0].Compromisso.ContagemTexto(relogio.Agora));
    }

    [Fact]
    public async Task EmCimaDaHora_SaiOSegundoAviso()
    {
        using var banco = new BancoDeTeste();
        var (lembretes, compromissos, relogio) = Montar(banco, QuartaAs9);

        await compromissos.SalvarAsync(Visita());

        relogio.Agora = QuintaAs14.AddDays(-1).AddMinutes(1);
        Assert.Single(await lembretes.VarrerCompromissosAsync());

        relogio.Agora = QuintaAs14.AddMinutes(-30);

        var saiu = await lembretes.VarrerCompromissosAsync();

        Assert.Single(saiu);
        Assert.False(saiu[0].Antecipado);
        Assert.Equal("em 30 min", saiu[0].Compromisso.ContagemTexto(relogio.Agora));
    }

    [Fact]
    public async Task VarreduraSeguinte_NaoRepeteOMesmoAviso()
    {
        using var banco = new BancoDeTeste();
        var (lembretes, compromissos, relogio) = Montar(banco, QuartaAs9);

        await compromissos.SalvarAsync(Visita());

        relogio.Agora = QuintaAs14.AddDays(-1).AddMinutes(1);
        Assert.Single(await lembretes.VarrerCompromissosAsync());

        relogio.Avancar(TimeSpan.FromSeconds(30));
        Assert.Empty(await lembretes.VarrerCompromissosAsync());
    }

    /// <summary>
    /// App fechado desde ontem: os dois avisos venceram. Dois balões seguidos sobre a mesma
    /// visita não informam mais que um, e o de véspera diria "amanhã" quando faltam 15 minutos.
    /// Sai o de cima da hora — e o outro fica marcado, para não voltar a cada 30 segundos.
    /// </summary>
    [Fact]
    public async Task OsDoisAvisosVencidosJuntos_ViramUmBalaoSo()
    {
        using var banco = new BancoDeTeste();
        var (lembretes, compromissos, relogio) = Montar(banco, QuartaAs9);

        var (_, _, salvo) = await compromissos.SalvarAsync(Visita());

        relogio.Agora = QuintaAs14.AddMinutes(-15);

        var saiu = await lembretes.VarrerCompromissosAsync();

        Assert.Single(saiu);
        Assert.False(saiu[0].Antecipado);

        var depois = await compromissos.ObterPorIdAsync(salvo!.Id);
        Assert.True(depois!.AvisoAntecipadoDisparado);
        Assert.True(depois.AvisoNaHoraDisparado);

        Assert.Empty(await lembretes.VarrerCompromissosAsync());
    }

    /// <summary>Cancelar precisa CALAR o que ainda não saiu — é a razão de a coluna situacao existir.</summary>
    [Fact]
    public async Task Cancelado_NaoAvisaMais()
    {
        using var banco = new BancoDeTeste();
        var (lembretes, compromissos, relogio) = Montar(banco, QuartaAs9);

        var (_, _, salvo) = await compromissos.SalvarAsync(Visita());
        await compromissos.DefinirSituacaoAsync(salvo!.Id, SituacaoCompromisso.Cancelado);

        relogio.Agora = QuintaAs14.AddMinutes(-15);

        Assert.Empty(await lembretes.VarrerCompromissosAsync());
    }

    [Fact]
    public async Task Realizado_NaoAvisaMais()
    {
        using var banco = new BancoDeTeste();
        var (lembretes, compromissos, relogio) = Montar(banco, QuartaAs9);

        var (_, _, salvo) = await compromissos.SalvarAsync(Visita());
        await compromissos.DefinirSituacaoAsync(salvo!.Id, SituacaoCompromisso.Realizado);

        relogio.Agora = QuintaAs14.AddMinutes(-15);

        Assert.Empty(await lembretes.VarrerCompromissosAsync());
    }

    /// <summary>
    /// Voltar de uma semana de férias não deve trazer balão de reunião que já aconteceu — não
    /// há mais nada a fazer com essa informação, e ela continua visível no histórico da tela.
    /// Mas o aviso também não pode ficar pendente para sempre.
    /// </summary>
    [Fact]
    public async Task CompromissoQueJaPassouHaMuito_NaoViraBalaoMasFicaMarcado()
    {
        using var banco = new BancoDeTeste();
        var (lembretes, compromissos, relogio) = Montar(banco, QuartaAs9);

        var (_, _, salvo) = await compromissos.SalvarAsync(Visita());

        relogio.Agora = QuintaAs14.AddDays(5);

        Assert.Empty(await lembretes.VarrerCompromissosAsync());

        var depois = await compromissos.ObterPorIdAsync(salvo!.Id);
        Assert.True(depois!.AvisoAntecipadoDisparado);
        Assert.True(depois.AvisoNaHoraDisparado);
    }

    /// <summary>
    /// A marca de "já avisei" é gravada ANTES de o evento sair. Se o app fechar entre uma coisa
    /// e outra, o pior caso é um balão perdido; o inverso seria o mesmo aviso reaparecendo a
    /// cada 30 segundos.
    /// </summary>
    [Fact]
    public async Task AMarcaEhGravadaAntesDeONotificarAcontecer()
    {
        using var banco = new BancoDeTeste();
        var (lembretes, compromissos, relogio) = Montar(banco, QuartaAs9);

        var (_, _, salvo) = await compromissos.SalvarAsync(Visita());

        relogio.Agora = QuintaAs14.AddMinutes(-30);

        var jaMarcadoNoBanco = false;
        lembretes.AvisosDeCompromisso += _ =>
            jaMarcadoNoBanco = banco.Escalar<int>(
                $"SELECT aviso_na_hora_disparado FROM compromissos WHERE id = {salvo!.Id}") == 1;

        await lembretes.VarrerCompromissosAsync();

        Assert.True(jaMarcadoNoBanco);
    }

    /// <summary>
    /// Remarcar depois de o aviso já ter saído tem de fazer o compromisso avisar de novo. Sem
    /// isto, a visita adiada de quinta para sexta passaria em silêncio.
    /// </summary>
    [Fact]
    public async Task Remarcado_VoltaAAvisar()
    {
        using var banco = new BancoDeTeste();
        var (lembretes, compromissos, relogio) = Montar(banco, QuartaAs9);

        var (_, _, salvo) = await compromissos.SalvarAsync(Visita());

        relogio.Agora = QuintaAs14.AddMinutes(-30);
        Assert.NotEmpty(await lembretes.VarrerCompromissosAsync());

        // Adiada para a semana seguinte, ainda dentro do horário comercial.
        var remarcado = await compromissos.ObterPorIdAsync(salvo!.Id);
        remarcado!.Quando = QuintaAs14.AddDays(7);
        await compromissos.SalvarAsync(remarcado);

        Assert.Empty(await lembretes.VarrerCompromissosAsync());

        relogio.Agora = QuintaAs14.AddDays(7).AddMinutes(-30);

        var saiu = await lembretes.VarrerCompromissosAsync();
        Assert.Single(saiu);
        Assert.False(saiu[0].Antecipado);
    }

    /// <summary>Sem aviso configurado, a varredura não tem o que fazer — e não inventa um.</summary>
    [Fact]
    public async Task SemAvisoNenhum_AVarreduraIgnora()
    {
        using var banco = new BancoDeTeste();
        var (lembretes, compromissos, relogio) = Montar(banco, QuartaAs9);

        await compromissos.SalvarAsync(new Compromisso
        {
            Titulo = "Só anotado na agenda", Quando = QuintaAs14,
        });

        relogio.Agora = QuintaAs14.AddMinutes(-1);

        Assert.Empty(await lembretes.VarrerCompromissosAsync());
    }
}
