using Dapper;
using MacroHelper.Core.Entities;
using MacroHelper.Data.Context;

namespace MacroHelper.Data.Repositories;

public class TarefaRepository
{
    private readonly SqliteContext _ctx;
    public TarefaRepository(SqliteContext ctx) => _ctx = ctx;

    private const string Colunas = """
        id, titulo, observacoes, concluida, prioridade, prazo, lembrete, recorrencia,
        lembrete_disparado AS LembreteDisparado,
        data_criacao       AS DataCriacao,
        data_conclusao     AS DataConclusao
        """;

    /// <summary>
    /// Abertas primeiro; dentro delas, quem tem prazo antes de quem não tem, do mais próximo
    /// para o mais distante; empate desempata por prioridade.
    /// </summary>
    private const string OrdemPadrao = """
        ORDER BY concluida,
                 prazo IS NULL,
                 prazo,
                 prioridade DESC,
                 data_criacao DESC
        """;

    public async Task<IEnumerable<Tarefa>> GetAllAsync()
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QueryAsync<Tarefa>($"SELECT {Colunas} FROM tarefas {OrdemPadrao}");
    }

    public async Task<Tarefa?> GetByIdAsync(int id)
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QuerySingleOrDefaultAsync<Tarefa>(
            $"SELECT {Colunas} FROM tarefas WHERE id = @id", new { id });
    }

    public async Task<int> InsertAsync(Tarefa t)
    {
        const string sql = """
            INSERT INTO tarefas (titulo, observacoes, concluida, prioridade, prazo,
                                 lembrete, recorrencia, lembrete_disparado,
                                 data_criacao, data_conclusao)
            VALUES (@Titulo, @Observacoes, @Concluida, @Prioridade, @Prazo,
                    @Lembrete, @Recorrencia, @LembreteDisparado,
                    @DataCriacao, @DataConclusao)
            RETURNING id
            """;

        t.DataCriacao = DateTime.Now;

        using var conexao = await _ctx.AbrirAsync();
        return await conexao.ExecuteScalarAsync<int>(sql, t);
    }

    public async Task UpdateAsync(Tarefa t)
    {
        const string sql = """
            UPDATE tarefas SET
                titulo             = @Titulo,
                observacoes        = @Observacoes,
                concluida          = @Concluida,
                prioridade         = @Prioridade,
                prazo              = @Prazo,
                lembrete           = @Lembrete,
                recorrencia        = @Recorrencia,
                lembrete_disparado = @LembreteDisparado,
                data_conclusao     = @DataConclusao
            WHERE id = @Id
            """;

        using var conexao = await _ctx.AbrirAsync();
        await conexao.ExecuteAsync(sql, t);
    }

    public async Task ToggleConcluidaAsync(int id, bool concluida)
    {
        using var conexao = await _ctx.AbrirAsync();
        await conexao.ExecuteAsync(
            "UPDATE tarefas SET concluida = @concluida, data_conclusao = @dataConclusao WHERE id = @id",
            new { id, concluida, dataConclusao = concluida ? DateTime.Now : (DateTime?)null });
    }

    public async Task DeleteAsync(int id)
    {
        using var conexao = await _ctx.AbrirAsync();
        await conexao.ExecuteAsync("DELETE FROM tarefas WHERE id = @id", new { id });
    }

    /// <summary>
    /// Lembretes já vencidos que ainda não notificaram. O WHERE casa exatamente com o índice
    /// parcial ix_tarefas_lembrete_pendente, então esta consulta — que roda a cada 30 segundos —
    /// custa o número de lembretes por disparar, e não o tamanho da tabela.
    /// </summary>
    public async Task<IEnumerable<Tarefa>> GetLembretesVencidosAsync(DateTime ate)
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QueryAsync<Tarefa>(
            $"""
             SELECT {Colunas} FROM tarefas
             WHERE lembrete IS NOT NULL AND lembrete_disparado = 0 AND concluida = 0
               AND lembrete <= @ate
             ORDER BY lembrete
             """, new { ate });
    }

    /// <summary>
    /// Grava a marca de "já avisei" ANTES de a notificação sair. Se o app fechar entre uma
    /// coisa e outra, o pior caso é um balão perdido — o inverso seria o mesmo lembrete
    /// reaparecendo a cada 30 segundos, para sempre.
    /// </summary>
    public async Task MarcarLembreteDisparadoAsync(int id)
    {
        using var conexao = await _ctx.AbrirAsync();
        await conexao.ExecuteAsync(
            "UPDATE tarefas SET lembrete_disparado = 1 WHERE id = @id", new { id });
    }
}
