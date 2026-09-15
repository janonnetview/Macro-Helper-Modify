using MacroHelper.Core.Entities;
using MacroHelper.Services;
using Microsoft.Data.Sqlite;

namespace MacroHelper.Tests;

public class TarefaTests
{
    private static readonly DateTime Hoje = new(2026, 8, 20, 9, 0, 0);

    /// <summary>
    /// A razão de prioridade ser INTEGER e não TEXT: como texto, ORDER BY colocaria "Alta"
    /// antes de "Baixa" antes de "Normal" — ordem alfabética, não de urgência.
    /// </summary>
    [Fact]
    public async Task Prioridade_EhGravadaComoNumeroEOrdenaPorUrgencia()
    {
        using var banco = new BancoDeTeste();
        var mesmoPrazo = Hoje.Date;

        await banco.Tarefas.InsertAsync(new Tarefa { Titulo = "Baixa",  Prioridade = PrioridadeTarefa.Baixa,  Prazo = mesmoPrazo });
        await banco.Tarefas.InsertAsync(new Tarefa { Titulo = "Alta",   Prioridade = PrioridadeTarefa.Alta,   Prazo = mesmoPrazo });
        await banco.Tarefas.InsertAsync(new Tarefa { Titulo = "Normal", Prioridade = PrioridadeTarefa.Normal, Prazo = mesmoPrazo });

        Assert.Equal(2, banco.Escalar<int>("SELECT prioridade FROM tarefas WHERE titulo = 'Alta'"));

        var ordem = (await banco.Tarefas.GetAllAsync()).Select(t => t.Titulo).ToList();
        Assert.Equal(["Alta", "Normal", "Baixa"], ordem);
    }

    /// <summary>
    /// A busca de tarefas não passa pelo SQL: tarefas não têm a coluna <c>busca</c> normalizada
    /// que as notas têm, e o LIKE do SQLite só ignora maiúsculas para ASCII. Este teste é o que
    /// diz que a normalização está sendo aplicada dos dois lados — sem ela, quem escreveu
    /// "Reunião" nunca mais acharia a tarefa digitando do jeito fácil.
    /// </summary>
    [Fact]
    public async Task Busca_IgnoraAcentoECaixa()
    {
        using var banco = new BancoDeTeste();
        var svc = new TarefaService(banco.Tarefas, new RelogioFalso(Hoje));

        await banco.Tarefas.InsertAsync(new Tarefa { Titulo = "Reunião de alinhamento" });
        await banco.Tarefas.InsertAsync(new Tarefa { Titulo = "Comprar café" });

        foreach (var termo in new[] { "reuniao", "REUNIÃO", "Reuniao" })
            Assert.Equal("Reunião de alinhamento",
                Assert.Single(await svc.PesquisarAsync(termo)).Titulo);
    }

    /// <summary>O que a pessoa lembra da tarefa costuma estar na observação, não no título.</summary>
    [Fact]
    public async Task Busca_TambemOlhaAsObservacoes()
    {
        using var banco = new BancoDeTeste();
        var svc = new TarefaService(banco.Tarefas, new RelogioFalso(Hoje));

        await banco.Tarefas.InsertAsync(new Tarefa { Titulo = "Ligar", Observacoes = "para o cartório" });
        await banco.Tarefas.InsertAsync(new Tarefa { Titulo = "Ligar", Observacoes = "para o contador" });

        Assert.Equal("para o cartório",
            Assert.Single(await svc.PesquisarAsync("cartorio")).Observacoes);
    }

    /// <summary>Sem termo, a busca é a listagem — é o que a aba do Ctrl+Espaço abre mostrando.</summary>
    [Fact]
    public async Task BuscaSemTermo_DevolveTudoNaOrdemPadrao()
    {
        using var banco = new BancoDeTeste();
        var svc = new TarefaService(banco.Tarefas, new RelogioFalso(Hoje));

        var feita = await banco.Tarefas.InsertAsync(new Tarefa { Titulo = "Feita" });
        await banco.Tarefas.ToggleConcluidaAsync(feita, true);
        await banco.Tarefas.InsertAsync(new Tarefa { Titulo = "Aberta", Prazo = Hoje.Date });

        var titulos = (await svc.PesquisarAsync("   ")).Select(t => t.Titulo).ToList();

        Assert.Equal(["Aberta", "Feita"], titulos);
    }

    /// <summary>Sem o CHECK, um 3 gravado por engano viraria uma prioridade inexistente que o enum não sabe ler.</summary>
    [Fact]
    public void PrioridadeForaDaFaixa_EhRecusadaPeloCheck()
    {
        using var banco = new BancoDeTeste();

        Assert.Throws<SqliteException>(() => banco.Executar("""
            INSERT INTO tarefas (titulo, concluida, prioridade, lembrete_disparado, data_criacao)
            VALUES ('Inválida', 0, 3, 0, '2026-08-20 09:00:00')
            """));
    }

    [Fact]
    public void ConcluidaForaDeZeroOuUm_EhRecusada()
    {
        using var banco = new BancoDeTeste();

        Assert.Throws<SqliteException>(() => banco.Executar("""
            INSERT INTO tarefas (titulo, concluida, prioridade, lembrete_disparado, data_criacao)
            VALUES ('Inválida', 2, 1, 0, '2026-08-20 09:00:00')
            """));
    }

    [Fact]
    public async Task Listagem_AbertasPrimeiroEQuemTemPrazoAntesDeQuemNaoTem()
    {
        using var banco = new BancoDeTeste();

        var concluida = await banco.Tarefas.InsertAsync(new Tarefa { Titulo = "Feita", Prazo = Hoje.Date.AddDays(-5) });
        await banco.Tarefas.ToggleConcluidaAsync(concluida, true);

        await banco.Tarefas.InsertAsync(new Tarefa { Titulo = "Sem prazo" });
        await banco.Tarefas.InsertAsync(new Tarefa { Titulo = "Vence amanhã", Prazo = Hoje.Date.AddDays(1) });
        await banco.Tarefas.InsertAsync(new Tarefa { Titulo = "Vencia ontem", Prazo = Hoje.Date.AddDays(-1) });

        var ordem = (await banco.Tarefas.GetAllAsync()).Select(t => t.Titulo).ToList();
        Assert.Equal(["Vencia ontem", "Vence amanhã", "Sem prazo", "Feita"], ordem);
    }

    [Fact]
    public async Task Concluir_GravaEDepoisLimpaADataDeConclusao()
    {
        using var banco = new BancoDeTeste();
        var id = await banco.Tarefas.InsertAsync(new Tarefa { Titulo = "Ida e volta" });

        await banco.Tarefas.ToggleConcluidaAsync(id, true);
        var feita = await banco.Tarefas.GetByIdAsync(id);
        Assert.True(feita!.Concluida);
        Assert.NotNull(feita.DataConclusao);

        await banco.Tarefas.ToggleConcluidaAsync(id, false);
        var reaberta = await banco.Tarefas.GetByIdAsync(id);
        Assert.False(reaberta!.Concluida);
        Assert.Null(reaberta.DataConclusao);
    }

    /// <summary>Prazo e lembrete são independentes: querer ser avisado na quarta sobre algo que vence na quinta é o caso normal.</summary>
    [Fact]
    public async Task PrazoELembrete_SaoGravadosSeparadamente()
    {
        using var banco = new BancoDeTeste();
        var id = await banco.Tarefas.InsertAsync(new Tarefa
        {
            Titulo   = "Independentes",
            Prazo    = new DateTime(2026, 8, 21),
            Lembrete = new DateTime(2026, 8, 20, 17, 30, 0),
        });

        var lida = await banco.Tarefas.GetByIdAsync(id);
        Assert.Equal(new DateTime(2026, 8, 21), lida!.Prazo);
        Assert.Equal(new DateTime(2026, 8, 20, 17, 30, 0), lida.Lembrete);
    }

    // ── Estado derivado, o que a tela pinta ──────────────────────────────────────

    [Fact]
    public void Atrasada_ComparaPorDiaENaoPorInstante()
    {
        // Vence hoje às 00:00 e agora são 23h: NÃO está atrasada — o prazo é o dia inteiro.
        var hoje = new Tarefa { Prazo = Hoje.Date };
        Assert.False(hoje.Atrasada(Hoje.Date.AddHours(23)));
        Assert.True(hoje.ParaHoje(Hoje.Date.AddHours(23)));

        var ontem = new Tarefa { Prazo = Hoje.Date.AddDays(-1) };
        Assert.True(ontem.Atrasada(Hoje));
    }

    [Fact]
    public void TarefaConcluida_NuncaEstaAtrasadaNemEhDeHoje()
    {
        var feita = new Tarefa { Prazo = Hoje.Date.AddDays(-3), Concluida = true };
        Assert.False(feita.Atrasada(Hoje));
        Assert.False(feita.ParaHoje(Hoje));
    }

    [Fact]
    public void LembretePendente_SoContaSeNaoDisparouENaoEstaConcluida()
    {
        Assert.True(new Tarefa { Lembrete = Hoje }.TemLembretePendente);
        Assert.False(new Tarefa { Lembrete = Hoje, LembreteDisparado = true }.TemLembretePendente);
        Assert.False(new Tarefa { Lembrete = Hoje, Concluida = true }.TemLembretePendente);
        Assert.False(new Tarefa().TemLembretePendente);
    }

    // ── TarefaService ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Salvar_SemTitulo_EhRecusado()
    {
        using var banco = new BancoDeTeste();
        var servico = new TarefaService(banco.Tarefas, new RelogioFalso(Hoje));

        var (ok, msg, _) = await servico.SalvarAsync(new Tarefa { Titulo = "   " });

        Assert.False(ok);
        Assert.Contains("título", msg, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>É o que alimenta o cartão do Início.</summary>
    [Fact]
    public async Task DeHojeEAtrasadas_TrazSoOQueVenceHojeOuJaVenceu()
    {
        using var banco = new BancoDeTeste();
        var servico = new TarefaService(banco.Tarefas, new RelogioFalso(Hoje));

        await servico.SalvarAsync(new Tarefa { Titulo = "Hoje",     Prazo = Hoje.Date });
        await servico.SalvarAsync(new Tarefa { Titulo = "Atrasada", Prazo = Hoje.Date.AddDays(-2) });
        await servico.SalvarAsync(new Tarefa { Titulo = "Amanhã",   Prazo = Hoje.Date.AddDays(1) });
        await servico.SalvarAsync(new Tarefa { Titulo = "Sem prazo" });

        var (_, _, feita) = await servico.SalvarAsync(new Tarefa { Titulo = "Feita", Prazo = Hoje.Date });
        await servico.ConcluirAsync(feita!.Id, true);

        var pendentes = (await servico.ObterDeHojeEAtrasadasAsync()).Select(t => t.Titulo).Order().ToList();
        Assert.Equal(["Atrasada", "Hoje"], pendentes);
    }

    [Fact]
    public async Task Excluir_InformaOTituloDoQueSaiu()
    {
        using var banco = new BancoDeTeste();
        var servico = new TarefaService(banco.Tarefas, new RelogioFalso(Hoje));
        var (_, _, tarefa) = await servico.SalvarAsync(new Tarefa { Titulo = "Comprar café" });

        var (ok, msg) = await servico.ExcluirAsync(tarefa!.Id);

        Assert.True(ok);
        Assert.Contains("Comprar café", msg);
        Assert.Equal(0, banco.Escalar<int>("SELECT COUNT(*) FROM tarefas"));
    }

    [Fact]
    public async Task Excluir_IdInexistente_NaoQuebra()
    {
        using var banco = new BancoDeTeste();
        var servico = new TarefaService(banco.Tarefas, new RelogioFalso(Hoje));

        var (ok, _) = await servico.ExcluirAsync(999);
        Assert.False(ok);
    }

    // ── Lembrete x prazo ─────────────────────────────────────────────

    /// <summary>
    /// Notificação marcada para depois do prazo tocaria para cobrar uma tarefa que já venceu.
    /// </summary>
    [Fact]
    public async Task Salvar_RecusaLembreteDepoisDoPrazo()
    {
        using var banco = new BancoDeTeste();
        var servico = new TarefaService(banco.Tarefas, new RelogioFalso(Hoje));

        var (ok, msg, _) = await servico.SalvarAsync(new Tarefa
        {
            Titulo   = "Entregar o relatório",
            Prazo    = Hoje.Date.AddDays(5),
            Lembrete = Hoje.Date.AddDays(6).AddHours(9),
        });

        Assert.False(ok);
        Assert.Contains("depois do prazo", msg);
        Assert.Equal(0, banco.Escalar<int>("SELECT COUNT(*) FROM tarefas"));
    }

    /// <summary>
    /// O par mais comum dos dois campos: vence hoje, avise hoje. O prazo não tem hora — fica em
    /// 00:00 —, então comparar instante em vez de dia recusaria justamente o caso normal.
    /// </summary>
    [Fact]
    public async Task Salvar_AceitaLembreteNoMesmoDiaDoPrazo()
    {
        using var banco = new BancoDeTeste();
        var servico = new TarefaService(banco.Tarefas, new RelogioFalso(Hoje));

        var (ok, _, tarefa) = await servico.SalvarAsync(new Tarefa
        {
            Titulo   = "Entregar o relatório",
            Prazo    = Hoje.Date,
            Lembrete = Hoje.Date.AddHours(12),
        });

        Assert.True(ok);
        Assert.Equal(Hoje.Date.AddHours(12), tarefa!.Lembrete);
    }

    /// <summary>
    /// Adiar um lembrete que tocou empurra ele para depois do prazo de propósito. A tarefa que
    /// chegou assim continua editável: a recusa é do par NOVO, não do estado em que a tarefa
    /// está — senão trocar uma vírgula do título viraria um erro sem saída.
    /// </summary>
    [Fact]
    public async Task Salvar_NaoRecusaLembreteQueJaEstavaDepoisDoPrazo()
    {
        using var banco = new BancoDeTeste();
        var servico = new TarefaService(banco.Tarefas, new RelogioFalso(Hoje));

        var (_, _, tarefa) = await servico.SalvarAsync(new Tarefa { Titulo = "Ligar", Prazo = Hoje.Date });
        await servico.AdiarLembreteAsync(tarefa!.Id, TimeSpan.FromDays(1));

        var adiada = await servico.ObterPorIdAsync(tarefa.Id);
        adiada!.Titulo = "Ligar para o contador";

        var (ok, _, _) = await servico.SalvarAsync(adiada);

        Assert.True(ok);
        Assert.Equal("Ligar para o contador", (await servico.ObterPorIdAsync(tarefa.Id))!.Titulo);
    }
}
