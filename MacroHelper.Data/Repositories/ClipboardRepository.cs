using Dapper;
using MacroHelper.Core;
using MacroHelper.Core.Entities;
using MacroHelper.Data.Context;

namespace MacroHelper.Data.Repositories;

public class ClipboardRepository
{
    private readonly SqliteContext _ctx;
    public ClipboardRepository(SqliteContext ctx) => _ctx = ctx;

    private const string Colunas = """
        id, conteudo, fixado, origem,
        data_copia AS DataCopia
        """;

    /// <summary>Fixados no topo; o resto pela cópia mais recente.</summary>
    private const string OrdemPadrao = "ORDER BY fixado DESC, data_copia DESC";

    public async Task<IEnumerable<ItemClipboard>> GetAllAsync()
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QueryAsync<ItemClipboard>($"SELECT {Colunas} FROM clipboard {OrdemPadrao}");
    }

    /// <summary>Busca sobre a coluna normalizada, igual à das notas: "acao" acha "Ação".</summary>
    public async Task<IEnumerable<ItemClipboard>> SearchAsync(string termo)
    {
        var normalizado = TextoNormalizado.Para(termo);
        if (string.IsNullOrWhiteSpace(normalizado)) return await GetAllAsync();

        // % e _ são curingas do LIKE: sem escapar, procurar "50%" listaria tudo.
        var padrao = "%" + normalizado
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_") + "%";

        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QueryAsync<ItemClipboard>(
            $"""
             SELECT {Colunas} FROM clipboard
             WHERE busca LIKE @padrao ESCAPE '\'
             {OrdemPadrao}
             """, new { padrao });
    }

    /// <summary>
    /// Registra uma cópia. Texto que já está no histórico não vira linha nova: sobe a data e
    /// volta para o topo, preservando o fixado — é o que o Win+V e o Ditto fazem, e é o que
    /// evita uma lista com o mesmo trecho oito vezes depois de uma tarde recopiando o mesmo
    /// número de chamado.
    /// </summary>
    /// <returns>O item gravado, com o id que ficou.</returns>
    public async Task<ItemClipboard> RegistrarAsync(string conteudo, string? origem)
    {
        using var conexao = await _ctx.AbrirAsync();

        var agora = DateTime.Now;
        var busca = TextoNormalizado.Para(conteudo);

        // UPDATE primeiro, INSERT só se não achou: o índice único em conteudo garante que a
        // segunda metade não possa criar duplicata mesmo se duas cópias chegarem coladas.
        var existente = await conexao.QuerySingleOrDefaultAsync<ItemClipboard>(
            $"SELECT {Colunas} FROM clipboard WHERE conteudo = @conteudo", new { conteudo });

        if (existente != null)
        {
            await conexao.ExecuteAsync(
                "UPDATE clipboard SET data_copia = @agora, origem = @origem, busca = @busca WHERE id = @id",
                new { id = existente.Id, agora, origem, busca });

            existente.DataCopia = agora;
            existente.Origem    = origem;
            return existente;
        }

        var id = await conexao.ExecuteScalarAsync<int>(
            """
            INSERT INTO clipboard (conteudo, fixado, origem, busca, data_copia)
            VALUES (@conteudo, 0, @origem, @busca, @agora)
            RETURNING id
            """, new { conteudo, origem, busca, agora });

        return new ItemClipboard { Id = id, Conteudo = conteudo, Origem = origem, DataCopia = agora };
    }

    public async Task ToggleFixadoAsync(int id, bool fixado)
    {
        using var conexao = await _ctx.AbrirAsync();
        await conexao.ExecuteAsync("UPDATE clipboard SET fixado = @fixado WHERE id = @id",
            new { id, fixado });
    }

    public async Task DeleteAsync(int id)
    {
        using var conexao = await _ctx.AbrirAsync();
        await conexao.ExecuteAsync("DELETE FROM clipboard WHERE id = @id", new { id });
    }

    /// <summary>Esvazia o histórico. O que está fixado fica — é conteúdo escolhido a dedo.</summary>
    /// <returns>Quantos itens saíram.</returns>
    public async Task<int> LimparNaoFixadosAsync()
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.ExecuteAsync("DELETE FROM clipboard WHERE fixado = 0");
    }

    /// <summary>
    /// Mantém só os <paramref name="limite"/> não-fixados mais recentes.
    ///
    /// A poda é por CONTAGEM e não por idade: o histórico serve para voltar algumas cópias
    /// atrás, e quantas cabem é o que a pessoa consegue percorrer numa lista — não quantos
    /// dias se passaram. Fixado nunca entra na conta.
    /// </summary>
    /// <returns>Quantos itens foram removidos.</returns>
    public async Task<int> PodarAsync(int limite)
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.ExecuteAsync(
            """
            DELETE FROM clipboard
            WHERE fixado = 0
              AND id NOT IN (
                  SELECT id FROM clipboard WHERE fixado = 0
                  ORDER BY data_copia DESC
                  LIMIT @limite
              )
            """, new { limite });
    }
}
