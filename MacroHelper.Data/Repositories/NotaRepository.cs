using Dapper;
using MacroHelper.Core;
using MacroHelper.Core.Entities;
using MacroHelper.Data.Context;

namespace MacroHelper.Data.Repositories;

public class NotaRepository
{
    private readonly SqliteContext _ctx;
    public NotaRepository(SqliteContext ctx) => _ctx = ctx;

    private const string Colunas = """
        id, titulo, conteudo, fixada,
        data_criacao     AS DataCriacao,
        data_atualizacao AS DataAtualizacao
        """;

    /// <summary>Fixadas no topo; o resto pela edição mais recente.</summary>
    private const string OrdemPadrao =
        "ORDER BY fixada DESC, COALESCE(data_atualizacao, data_criacao) DESC";

    public async Task<IEnumerable<Nota>> GetAllAsync()
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QueryAsync<Nota>($"SELECT {Colunas} FROM notas {OrdemPadrao}");
    }

    /// <summary>
    /// Busca sobre a coluna <c>busca</c>, que já está minúscula e sem acentos — o termo passa
    /// pela mesma normalização, então "acao", "AÇÃO" e "Ação" encontram a mesma nota.
    ///
    /// LIKE e não FTS5: algumas centenas de notas cabem num full scan de poucos milissegundos,
    /// e o FTS5 custaria uma tabela virtual mais três gatilhos de sincronia — a fonte clássica
    /// de "a busca não acha a nota que acabei de editar" — para entregar um ranking que
    /// ninguém pediu.
    /// </summary>
    public async Task<IEnumerable<Nota>> SearchAsync(string termo)
    {
        var normalizado = TextoNormalizado.Para(termo);
        if (string.IsNullOrWhiteSpace(normalizado)) return await GetAllAsync();

        // % e _ são curingas do LIKE: sem escapar, procurar "50%" listaria tudo.
        var padrao = "%" + normalizado
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_") + "%";

        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QueryAsync<Nota>(
            $"""
             SELECT {Colunas} FROM notas
             WHERE busca LIKE @padrao ESCAPE '\'
             {OrdemPadrao}
             """, new { padrao });
    }

    public async Task<int> InsertAsync(Nota n)
    {
        const string sql = """
            INSERT INTO notas (titulo, conteudo, fixada, busca, data_criacao, data_atualizacao)
            VALUES (@Titulo, @Conteudo, @Fixada, @Busca, @DataCriacao, @DataAtualizacao)
            RETURNING id
            """;

        n.DataCriacao = DateTime.Now;

        using var conexao = await _ctx.AbrirAsync();
        return await conexao.ExecuteScalarAsync<int>(sql, new
        {
            n.Titulo, n.Conteudo, n.Fixada, n.DataCriacao, n.DataAtualizacao,
            Busca = MontarBusca(n),
        });
    }

    public async Task UpdateAsync(Nota n)
    {
        const string sql = """
            UPDATE notas SET
                titulo           = @Titulo,
                conteudo         = @Conteudo,
                fixada           = @Fixada,
                busca            = @Busca,
                data_atualizacao = @DataAtualizacao
            WHERE id = @Id
            """;

        n.DataAtualizacao = DateTime.Now;

        using var conexao = await _ctx.AbrirAsync();
        await conexao.ExecuteAsync(sql, new
        {
            n.Id, n.Titulo, n.Conteudo, n.Fixada, n.DataAtualizacao,
            Busca = MontarBusca(n),
        });
    }

    public async Task ToggleFixadaAsync(int id, bool fixada)
    {
        using var conexao = await _ctx.AbrirAsync();
        await conexao.ExecuteAsync("UPDATE notas SET fixada = @fixada WHERE id = @id",
            new { id, fixada });
    }

    public async Task DeleteAsync(int id)
    {
        using var conexao = await _ctx.AbrirAsync();
        await conexao.ExecuteAsync("DELETE FROM notas WHERE id = @id", new { id });
    }

    /// <summary>
    /// Calculada aqui, dentro do mesmo objeto de parâmetros do INSERT/UPDATE. Não existe
    /// caminho em que titulo ou conteudo sejam gravados sem a coluna busca acompanhar.
    /// </summary>
    private static string MontarBusca(Nota n) =>
        TextoNormalizado.Para(n.Titulo + " " + n.Conteudo);
}
