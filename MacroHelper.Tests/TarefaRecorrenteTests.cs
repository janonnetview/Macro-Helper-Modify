using MacroHelper.Core.Entities;
using MacroHelper.Services;

namespace MacroHelper.Tests;

/// <summary>
/// Concluir uma tarefa recorrente sobre o banco de verdade: o que fica gravado, quantas linhas
/// existem depois, e o que acontece se a pessoa reabrir o que acabou de concluir.
/// </summary>
public class TarefaRecorrenteTests
{
    private static readonly DateTime Quinta = new(2026, 8, 20, 9, 0, 0);

    private static TarefaService Servico(BancoDeTeste banco, DateTime? agora = null) =>
        new(banco.Tarefas, new RelogioFalso(agora ?? Quinta));

    [Fact]
    public async Task Concluir_TarefaRecorrente_CriaAProximaOcorrencia()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        var id = await banco.Tarefas.InsertAsync(new Tarefa
        {
            Titulo      = "Enviar relatório",
            Observacoes = "Modelo na pasta X",
            Prioridade  = PrioridadeTarefa.Alta,
            Recorrencia = RecorrenciaTarefa.Semanal,
            Prazo       = Quinta.Date,
        });

        var (ok, msg) = await svc.ConcluirAsync(id, true);
        Assert.True(ok);
        Assert.Contains("27/08", msg);

        var todas = (await svc.ObterTodasAsync()).ToList();
        Assert.Equal(2, todas.Count);

        var nova = todas.Single(t => t.Id != id);
        Assert.Equal("Enviar relatório", nova.Titulo);
        Assert.Equal("Modelo na pasta X", nova.Observacoes);
        Assert.Equal(PrioridadeTarefa.Alta, nova.Prioridade);
        Assert.Equal(new DateTime(2026, 8, 27), nova.Prazo);
        Assert.False(nova.Concluida);
    }

    /// <summary>
    /// O bastão passa: a concluída deixa de ser recorrente e a nova assume. É o que impede que
    /// reabrir e reconcluir a mesma tarefa gere uma segunda cópia da mesma ocorrência.
    /// </summary>
    [Fact]
    public async Task Concluir_PassaARecorrenciaParaANova_ENaoDuplicaAoReabrir()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        var id = await banco.Tarefas.InsertAsync(new Tarefa
        {
            Titulo      = "Backup",
            Recorrencia = RecorrenciaTarefa.Diaria,
            Prazo       = Quinta.Date,
        });

        await svc.ConcluirAsync(id, true);

        var concluida = (await svc.ObterPorIdAsync(id))!;
        Assert.Equal(RecorrenciaTarefa.Nenhuma, concluida.Recorrencia);
        Assert.True(concluida.Concluida);
        Assert.NotNull(concluida.DataConclusao);

        var nova = (await svc.ObterTodasAsync()).Single(t => t.Id != id);
        Assert.Equal(RecorrenciaTarefa.Diaria, nova.Recorrencia);

        // Reabrir e concluir de novo não pode criar uma terceira linha.
        await svc.ConcluirAsync(id, false);
        await svc.ConcluirAsync(id, true);
        Assert.Equal(2, (await svc.ObterTodasAsync()).Count());
    }

    [Fact]
    public async Task Concluir_TarefaComum_NaoCriaNada()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        var id = await banco.Tarefas.InsertAsync(new Tarefa { Titulo = "Ligar para o cliente" });

        var (_, msg) = await svc.ConcluirAsync(id, true);

        Assert.Equal("Tarefa concluída!", msg);
        Assert.Single(await svc.ObterTodasAsync());
    }

    /// <summary>A recorrência sobrevive a uma ida e volta pelo banco — a coluna nova é gravada e lida.</summary>
    [Fact]
    public async Task Recorrencia_EhGravadaComoNumeroEVoltaIgual()
    {
        using var banco = new BancoDeTeste();

        var id = await banco.Tarefas.InsertAsync(new Tarefa
        {
            Titulo      = "Mensal",
            Recorrencia = RecorrenciaTarefa.Mensal,
        });

        Assert.Equal(5, banco.Escalar<int>($"SELECT recorrencia FROM tarefas WHERE id = {id}"));
        Assert.Equal(RecorrenciaTarefa.Mensal, (await banco.Tarefas.GetByIdAsync(id))!.Recorrencia);
    }

    /// <summary>Tarefa que já existia antes da migração 003 continua não-recorrente.</summary>
    [Fact]
    public void Recorrencia_TemPadraoNenhumaParaLinhasAntigas()
    {
        using var banco = new BancoDeTeste();

        banco.Executar("""
            INSERT INTO tarefas (titulo, concluida, prioridade, lembrete_disparado, data_criacao)
            VALUES ('Antiga', 0, 1, 0, '2026-01-01 08:00:00')
            """);

        Assert.Equal(0, banco.Escalar<int>("SELECT recorrencia FROM tarefas WHERE titulo = 'Antiga'"));
    }

    // ── Adiar ────────────────────────────────────────────────────────────────

    /// <summary>
    /// O ponto do adiamento: um lembrete que JÁ disparou volta a valer. Sem rearmar
    /// <c>lembrete_disparado</c>, adiar seria um botão que muda o horário e nunca mais avisa.
    /// </summary>
    [Fact]
    public async Task Adiar_ReamaOLembreteQueJaDisparou()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        var id = await banco.Tarefas.InsertAsync(new Tarefa
        {
            Titulo            = "Ligar para o cliente",
            Lembrete          = Quinta.AddHours(-2),
            LembreteDisparado = true,
        });

        var (ok, msg) = await svc.AdiarLembreteAsync(id, TimeSpan.FromMinutes(15));
        Assert.True(ok);
        Assert.Contains("09:15", msg);

        var tarefa = (await svc.ObterPorIdAsync(id))!;
        Assert.Equal(Quinta.AddMinutes(15), tarefa.Lembrete);
        Assert.False(tarefa.LembreteDisparado);

        // E volta a ser encontrada pela varredura, que é o que faz a notificação sair de novo.
        var vencidos = await svc.ObterLembretesVencidosAsync(Quinta.AddMinutes(20));
        Assert.Single(vencidos);
    }

    /// <summary>
    /// Adiar conta a partir de AGORA, não do horário antigo: "15 minutos" às 14h30 de um
    /// lembrete que era para as 9h significa 14h45, e não 9h15 — que já passou.
    /// </summary>
    [Fact]
    public async Task Adiar_ContaAPartirDeAgoraENaoDoLembreteAntigo()
    {
        using var banco = new BancoDeTeste();
        var agora = Quinta.Date.AddHours(14).AddMinutes(30);
        var svc   = Servico(banco, agora);

        var id = await banco.Tarefas.InsertAsync(new Tarefa
        {
            Titulo            = "Atrasado",
            Lembrete          = Quinta.Date.AddHours(9),
            LembreteDisparado = true,
        });

        await svc.AdiarLembreteAsync(id, TimeSpan.FromMinutes(15));

        Assert.Equal(agora.AddMinutes(15), (await svc.ObterPorIdAsync(id))!.Lembrete);
    }

    [Fact]
    public async Task Reagendar_ColocaOLembreteNoInstantePedido()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        var id = await banco.Tarefas.InsertAsync(new Tarefa
        {
            Titulo            = "Amanhã de manhã",
            Lembrete          = Quinta,
            LembreteDisparado = true,
        });

        var amanhaAs9 = Quinta.Date.AddDays(1).AddHours(9);
        await svc.ReagendarLembreteAsync(id, amanhaAs9);

        var tarefa = (await svc.ObterPorIdAsync(id))!;
        Assert.Equal(amanhaAs9, tarefa.Lembrete);
        Assert.False(tarefa.LembreteDisparado);
    }

    [Fact]
    public async Task Adiar_TarefaInexistente_FalhaSemEstourar()
    {
        using var banco = new BancoDeTeste();

        var (ok, msg) = await Servico(banco).AdiarLembreteAsync(9999, TimeSpan.FromHours(1));

        Assert.False(ok);
        Assert.Contains("não encontrada", msg);
    }
}
