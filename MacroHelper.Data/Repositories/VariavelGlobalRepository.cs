using Dapper;
using MacroHelper.Core.Entities;
using MacroHelper.Data.Context;
using Microsoft.Data.Sqlite;

namespace MacroHelper.Data.Repositories;

public class VariavelGlobalRepository
{
    private readonly SqliteContext _ctx;
    public VariavelGlobalRepository(SqliteContext ctx) => _ctx = ctx;

    private const string Colunas = """
        id, nome, descricao,
        valor_padrao AS ValorPadrao,
        data_criacao AS DataCriacao
        """;

    public async Task<IEnumerable<VariavelGlobal>> GetAllAsync()
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QueryAsync<VariavelGlobal>(
            $"SELECT {Colunas} FROM variaveis_globais ORDER BY nome COLLATE NOCASE");
    }

    public async Task<int> InsertAsync(VariavelGlobal v)
    {
        const string sql = """
            INSERT INTO variaveis_globais (nome, valor_padrao, descricao, data_criacao)
            VALUES (@Nome, @ValorPadrao, @Descricao, @DataCriacao)
            RETURNING id
            """;

        v.DataCriacao = DateTime.Now;

        using var conexao = await _ctx.AbrirAsync();
        try
        {
            return await conexao.ExecuteScalarAsync<int>(sql, v);
        }
        catch (SqliteException ex) when (ErrosSqlite.EhDuplicidade(ex))
        {
            throw ErrosSqlite.Traduzir(ex);
        }
    }

    public async Task UpdateAsync(VariavelGlobal v)
    {
        using var conexao = await _ctx.AbrirAsync();
        try
        {
            await conexao.ExecuteAsync(
                """
                UPDATE variaveis_globais
                SET nome = @Nome, valor_padrao = @ValorPadrao, descricao = @Descricao
                WHERE id = @Id
                """, v);
        }
        catch (SqliteException ex) when (ErrosSqlite.EhDuplicidade(ex))
        {
            throw ErrosSqlite.Traduzir(ex);
        }
    }

    public async Task DeleteAsync(int id)
    {
        using var conexao = await _ctx.AbrirAsync();
        await conexao.ExecuteAsync("DELETE FROM variaveis_globais WHERE id = @id", new { id });
    }
}
