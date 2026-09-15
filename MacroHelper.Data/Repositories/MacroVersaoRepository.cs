using Dapper;
using MacroHelper.Core.Entities;
using MacroHelper.Data.Context;

namespace MacroHelper.Data.Repositories;

public class MacroVersaoRepository
{
    private readonly SqliteContext _ctx;
    public MacroVersaoRepository(SqliteContext ctx) => _ctx = ctx;

    public async Task RegistrarAsync(int macroId, string titulo, string conteudo)
    {
        using var conexao = await _ctx.AbrirAsync();
        await conexao.ExecuteAsync(
            """
            INSERT INTO macro_versoes (macro_id, titulo, conteudo, data_modificacao)
            VALUES (@macroId, @titulo, @conteudo, @dataModificacao)
            """,
            new { macroId, titulo, conteudo, dataModificacao = DateTime.Now });
    }

    public async Task<IEnumerable<MacroVersao>> GetByMacroAsync(int macroId)
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QueryAsync<MacroVersao>(
            """
            SELECT id, titulo, conteudo,
                   macro_id         AS MacroId,
                   data_modificacao AS DataModificacao
            FROM macro_versoes
            WHERE macro_id = @macroId
            ORDER BY data_modificacao DESC
            """, new { macroId });
    }
}
