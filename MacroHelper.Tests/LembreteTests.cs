using MacroHelper.Core.Entities;
using MacroHelper.Services;
using System.Windows.Threading;

namespace MacroHelper.Tests;

/// <summary>
/// O comportamento que justifica a tela de Tarefas existir: o lembrete precisa avisar, uma vez
/// só, mesmo que a hora marcada tenha passado com o app fechado.
///
/// Tudo aqui roda por <c>VarrerAsync</c>, sem o <c>DispatcherTimer</c>. A varredura é pública
/// exatamente para isso — o timer só decide QUANDO ela roda, e isso o relógio falso substitui.
/// </summary>
public class LembreteTests
{
    private static readonly DateTime QuintaAs14 = new(2026, 8, 20, 14, 0, 0);

    private static (LembreteService Lembretes, TarefaService Tarefas, RelogioFalso Relogio)
        Montar(BancoDeTeste banco, DateTime agora)
    {
        var relogio = new RelogioFalso(agora);
        var tarefas = new TarefaService(banco.Tarefas, relogio);

        // Dispatcher.CurrentDispatcher cria um para a thread do teste. Nada nunca vai tiquetaquear
        // nele — o timer existe, mas Start() jamais é chamado.
        var lembretes = new LembreteService(
            tarefas, new CompromissoService(banco.Compromissos, relogio),
            relogio, Dispatcher.CurrentDispatcher);
        return (lembretes, tarefas, relogio);
    }

    /// <summary>O cenário do plano: marcado para as 14h, app aberto às 16h.</summary>
    [Fact]
    public async Task LembreteVencidoComOAppFechado_DisparaNaPrimeiraVarredura()
    {
        using var banco = new BancoDeTeste();
        var (lembretes, tarefas, relogio) = Montar(banco, QuintaAs14);

        await tarefas.SalvarAsync(new Tarefa { Titulo = "Responder o chamado 4821", Lembrete = QuintaAs14.AddMinutes(30) });

        // O app ficou fechado; quando abre, já são 16h05.
        relogio.Agora = QuintaAs14.AddHours(2).AddMinutes(5);

        var avisados = new List<Tarefa>();
        lembretes.LembretesVencidos += ts => avisados.AddRange(ts);

        var resultado = await lembretes.VarrerTarefasAsync();

        Assert.Single(resultado);
        Assert.Equal("Responder o chamado 4821", resultado[0].Titulo);

        // O evento também disparou — é ele que a MainWindow escuta para mostrar o balão.
        Assert.Single(avisados);

        // E o horário ORIGINAL sobrevive: é o que a notificação mostra ("Lembrete de 20/08 às 14:30").
        Assert.Equal(QuintaAs14.AddMinutes(30), resultado[0].Lembrete);
    }

    [Fact]
    public async Task VarreduraSeguinte_NaoRepeteOMesmoLembrete()
    {
        using var banco = new BancoDeTeste();
        var (lembretes, tarefas, relogio) = Montar(banco, QuintaAs14);

        await tarefas.SalvarAsync(new Tarefa { Titulo = "Uma vez só", Lembrete = QuintaAs14.AddMinutes(-1) });

        Assert.Single(await lembretes.VarrerTarefasAsync());

        relogio.Avancar(TimeSpan.FromSeconds(30));
        Assert.Empty(await lembretes.VarrerTarefasAsync());
    }

    [Fact]
    public async Task LembreteFuturo_NaoDisparaAntesDaHora()
    {
        using var banco = new BancoDeTeste();
        var (lembretes, tarefas, relogio) = Montar(banco, QuintaAs14);

        await tarefas.SalvarAsync(new Tarefa { Titulo = "Reunião", Lembrete = QuintaAs14.AddHours(4) });

        Assert.Empty(await lembretes.VarrerTarefasAsync());

        relogio.Agora = QuintaAs14.AddHours(4).AddSeconds(30);
        Assert.Single(await lembretes.VarrerTarefasAsync());
    }

    /// <summary>
    /// Voltar de férias não deve encher a tela de balões. Mas o lembrete velho também não pode
    /// continuar "pendente": seria reavaliado a cada 30 segundos, para sempre.
    /// </summary>
    [Fact]
    public async Task LembreteVelhoDemais_NaoNotificaMasTambemNaoFicaPendente()
    {
        using var banco = new BancoDeTeste();
        var (lembretes, tarefas, _) = Montar(banco, QuintaAs14);

        await tarefas.SalvarAsync(new Tarefa { Titulo = "Coisa das férias", Lembrete = QuintaAs14.AddDays(-29) });

        Assert.Empty(await lembretes.VarrerTarefasAsync());
        Assert.Empty(await tarefas.ObterLembretesVencidosAsync(QuintaAs14));
    }

    [Fact]
    public async Task NoLimiteDaJanelaDeSeteDias_AindaNotifica()
    {
        using var banco = new BancoDeTeste();
        var (lembretes, tarefas, _) = Montar(banco, QuintaAs14);

        await tarefas.SalvarAsync(new Tarefa { Titulo = "Seis dias atrás", Lembrete = QuintaAs14.AddDays(-6) });

        Assert.Single(await lembretes.VarrerTarefasAsync());
    }

    /// <summary>Sem isto, adiar um lembrete já disparado não avisaria de novo — e pareceria que o adiamento não funcionou.</summary>
    [Fact]
    public async Task AdiarUmLembreteJaDisparado_RearmaANotificacao()
    {
        using var banco = new BancoDeTeste();
        var (lembretes, tarefas, relogio) = Montar(banco, QuintaAs14);

        var (_, _, tarefa) = await tarefas.SalvarAsync(
            new Tarefa { Titulo = "Adiável", Lembrete = QuintaAs14.AddMinutes(-1) });

        await lembretes.VarrerTarefasAsync();
        Assert.True((await tarefas.ObterPorIdAsync(tarefa!.Id))!.LembreteDisparado);

        var recarregada = await tarefas.ObterPorIdAsync(tarefa.Id);
        recarregada!.Lembrete = QuintaAs14.AddHours(1);
        await tarefas.SalvarAsync(recarregada);

        Assert.False((await tarefas.ObterPorIdAsync(tarefa.Id))!.LembreteDisparado);

        relogio.Agora = QuintaAs14.AddHours(1).AddMinutes(1);
        Assert.Single(await lembretes.VarrerTarefasAsync());
    }

    /// <summary>Editar outra coisa da tarefa NÃO pode fazer o lembrete avisar de novo.</summary>
    [Fact]
    public async Task EditarOTituloSemMexerNoLembrete_NaoRearma()
    {
        using var banco = new BancoDeTeste();
        var (lembretes, tarefas, _) = Montar(banco, QuintaAs14);

        var (_, _, tarefa) = await tarefas.SalvarAsync(
            new Tarefa { Titulo = "Antes", Lembrete = QuintaAs14.AddMinutes(-1) });
        await lembretes.VarrerTarefasAsync();

        var recarregada = await tarefas.ObterPorIdAsync(tarefa!.Id);
        recarregada!.Titulo = "Depois";
        await tarefas.SalvarAsync(recarregada);

        Assert.True((await tarefas.ObterPorIdAsync(tarefa.Id))!.LembreteDisparado);
        Assert.Empty(await lembretes.VarrerTarefasAsync());
    }

    /// <summary>Concluir a tarefa cancela o lembrete: o índice parcial exige concluida = 0.</summary>
    [Fact]
    public async Task TarefaConcluida_NaoDisparaMaisOLembrete()
    {
        using var banco = new BancoDeTeste();
        var (lembretes, tarefas, _) = Montar(banco, QuintaAs14);

        var (_, _, tarefa) = await tarefas.SalvarAsync(
            new Tarefa { Titulo = "Já feita", Lembrete = QuintaAs14.AddMinutes(-1) });

        await tarefas.ConcluirAsync(tarefa!.Id, true);

        Assert.Empty(await lembretes.VarrerTarefasAsync());
    }

    /// <summary>
    /// A marca de "já avisei" é gravada ANTES de o evento sair. Se o app fechar entre uma coisa
    /// e outra, o pior caso é um balão perdido; o inverso seria o mesmo lembrete reaparecendo a
    /// cada 30 segundos.
    /// </summary>
    [Fact]
    public async Task AMarcaEhGravadaAntesDeONotificarAcontecer()
    {
        using var banco = new BancoDeTeste();
        var (lembretes, tarefas, _) = Montar(banco, QuintaAs14);

        var (_, _, tarefa) = await tarefas.SalvarAsync(
            new Tarefa { Titulo = "Ordem importa", Lembrete = QuintaAs14.AddMinutes(-1) });

        var jaMarcadoNoBanco = false;
        lembretes.LembretesVencidos += _ =>
            jaMarcadoNoBanco = banco.Escalar<int>(
                $"SELECT lembrete_disparado FROM tarefas WHERE id = {tarefa!.Id}") == 1;

        await lembretes.VarrerTarefasAsync();

        Assert.True(jaMarcadoNoBanco);
    }
}
