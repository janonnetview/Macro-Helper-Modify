using Dapper;
using MacroHelper.Core.Entities;
using MacroHelper.Data.Context;
using Microsoft.Data.Sqlite;

namespace MacroHelper.Data.Repositories;

public class CategoriaRepository
{
    private readonly SqliteContext _ctx;
    public CategoriaRepository(SqliteContext ctx) => _ctx = ctx;

    private const string Colunas = """
        c.id, c.nome, c.icone, c.cor, c.ordem,
        c.pai_id       AS PaiId,
        c.data_criacao AS DataCriacao
        """;

    /// <summary>Raízes primeiro (equivale ao NULLS FIRST de antes), depois ordem e nome.</summary>
    private const string OrdemPadrao =
        "ORDER BY c.pai_id IS NOT NULL, c.ordem, c.nome COLLATE NOCASE";

    /// <summary>O LEFT JOIN preenche NomePai numa consulta só — antes era um dicionário montado em C#.</summary>
    public async Task<IEnumerable<Categoria>> GetAllAsync()
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QueryAsync<Categoria>(
            $"""
             SELECT {Colunas}, p.nome AS NomePai
             FROM categorias c
             LEFT JOIN categorias p ON p.id = c.pai_id
             {OrdemPadrao}
             """);
    }

    public async Task<IEnumerable<Categoria>> GetRaizAsync()
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QueryAsync<Categoria>(
            $"SELECT {Colunas} FROM categorias c WHERE c.pai_id IS NULL {OrdemPadrao}");
    }

    public async Task<int> InsertAsync(Categoria cat)
    {
        const string sql = """
            INSERT INTO categorias (nome, icone, cor, pai_id, ordem, data_criacao)
            VALUES (@Nome, @Icone, @Cor, @PaiId, @Ordem, @DataCriacao)
            RETURNING id
            """;

        cat.DataCriacao = DateTime.Now;

        using var conexao = await _ctx.AbrirAsync();
        try
        {
            return await conexao.ExecuteScalarAsync<int>(sql, cat);
        }
        catch (SqliteException ex) when (ErrosSqlite.EhDuplicidade(ex))
        {
            throw ErrosSqlite.Traduzir(ex);
        }
    }

    /// <summary>
    /// Renomear a categoria também reescreve a cópia desnormalizada em <c>macros.categoria</c>,
    /// na mesma transação. Sem isso o filtro da listagem continuaria oferecendo o nome antigo
    /// indefinidamente — era o comportamento anterior, e a divergência não tinha conserto
    /// automático nenhum.
    /// </summary>
    public async Task UpdateAsync(Categoria cat)
    {
        using var conexao = await _ctx.AbrirAsync();
        using var tx = conexao.BeginTransaction();
        try
        {
            await conexao.ExecuteAsync(
                """
                UPDATE categorias SET nome = @Nome, icone = @Icone, cor = @Cor,
                                      pai_id = @PaiId, ordem = @Ordem
                WHERE id = @Id
                """, cat, tx);

            await conexao.ExecuteAsync(
                "UPDATE macros SET categoria = @Nome WHERE categoria_id = @Id",
                new { cat.Nome, cat.Id }, tx);

            tx.Commit();
        }
        catch (SqliteException ex) when (ErrosSqlite.EhDuplicidade(ex))
        {
            throw ErrosSqlite.Traduzir(ex);
        }
    }

    /// <summary>
    /// As subcategorias viram raiz e as macros perdem o vínculo por conta do
    /// ON DELETE SET NULL do schema — o laço manual de antes deixou de existir.
    ///
    /// O texto em <c>macros.categoria</c> é preservado de propósito: a macro continua rotulada
    /// e aparecendo no filtro. Excluir a categoria não deve apagar a informação de qual era.
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        using var conexao = await _ctx.AbrirAsync();
        await conexao.ExecuteAsync("DELETE FROM categorias WHERE id = @id", new { id });
    }
}
