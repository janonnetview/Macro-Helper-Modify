using Dapper;
using MacroHelper.Core;
using MacroHelper.Core.Entities;
using MacroHelper.Data.Context;
using Microsoft.Data.Sqlite;

namespace MacroHelper.Data.Repositories;

public class MacroRepository
{
    private readonly SqliteContext _ctx;
    public MacroRepository(SqliteContext ctx) => _ctx = ctx;

    /// <summary>
    /// Colunas de listagem — exclui imagem_base64, que pode ter centenas de KB por macro.
    ///
    /// Só as colunas snake_case levam alias: o Dapper casa <c>titulo</c> com <c>Titulo</c>
    /// sozinho, mas NÃO casa <c>data_criacao</c> com <c>DataCriacao</c> — sem o alias a
    /// propriedade fica no valor padrão, silenciosamente. Por isso nunca usar SELECT *.
    /// </summary>
    private const string Colunas = """
        id, atalho, titulo, conteudo, categoria, ativo, favorito,
        categoria_id     AS CategoriaId,
        atalho_tecla     AS AtalhoTecla,
        data_criacao     AS DataCriacao,
        data_atualizacao AS DataAtualizacao
        """;

    /// <summary>Inclui a imagem. Só para abrir uma macro isolada — nunca para listas.</summary>
    private const string ColunasCompletas = Colunas + ", imagem_base64 AS ImagemBase64";

    /// <summary>
    /// NULLS LAST explícito: no SQLite o NULL ordena PRIMEIRO, no Postgres ordena por último.
    /// Sem o <c>categoria IS NULL</c> à frente, as macros sem categoria pulariam do fim para o
    /// topo da lista só por causa da troca de banco.
    /// </summary>
    private const string OrdemPadrao =
        "ORDER BY categoria IS NULL, categoria COLLATE NOCASE, titulo COLLATE NOCASE";

    public async Task<IEnumerable<Macro>> GetAllAsync()
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QueryAsync<Macro>($"SELECT {Colunas} FROM macros {OrdemPadrao}");
    }

    /// <summary>
    /// Filtra e ranqueia em memória, com os mesmos 6 níveis de relevância de sempre.
    ///
    /// O filtro saiu do SQL porque o LIKE do SQLite só é insensível a maiúsculas para ASCII —
    /// "ACAO" acharia "acao", mas "AÇÃO" não acharia "ação". Em C# a comparação também não
    /// resolvia sozinha: <c>OrdinalIgnoreCase</c> dobra maiúscula e minúscula, inclusive das
    /// acentuadas, mas NÃO equipara "a" a "á" — procurar "acao" simplesmente não achava
    /// "Ação", enquanto a mesma busca na aba de Notas achava, porque lá o texto passa por
    /// <see cref="TextoNormalizado"/>. Duas buscas na mesma janela, com regras diferentes.
    ///
    /// Aqui os dois lados passam pela mesma normalização das notas e das tarefas: minúsculo e
    /// sem acento, dos dois lados da comparação. Com o texto já achatado, Ordinal basta —
    /// e é mais rápido que qualquer comparação cultural.
    /// </summary>
    public async Task<IEnumerable<Macro>> SearchAsync(string termo)
    {
        using var conexao = await _ctx.AbrirAsync();
        var ativas = await conexao.QueryAsync<Macro>($"SELECT {Colunas} FROM macros WHERE ativo = 1");

        var alvo = TextoNormalizado.Para(termo);
        if (string.IsNullOrEmpty(alvo)) return ativas.ToList();

        const StringComparison ord = StringComparison.Ordinal;

        // Normalizado UMA vez por macro. Fazer isso dentro do Where e de novo dentro do
        // OrderBy custaria quatro achatamentos de string por macro a cada tecla digitada.
        var candidatos = ativas.Select(m => new
        {
            Macro     = m,
            Atalho    = TextoNormalizado.Para(m.Atalho),
            Titulo    = TextoNormalizado.Para(m.Titulo),
            Conteudo  = TextoNormalizado.Para(m.Conteudo),
            Categoria = TextoNormalizado.Para(m.Categoria),
        });

        return candidatos
            .Where(c => c.Atalho.Contains(alvo, ord) || c.Titulo.Contains(alvo, ord) ||
                        c.Conteudo.Contains(alvo, ord) || c.Categoria.Contains(alvo, ord))
            .OrderBy(c =>
                c.Atalho == alvo                  ? 0 :
                c.Atalho.StartsWith(alvo, ord)    ? 1 :
                c.Titulo.StartsWith(alvo, ord)    ? 2 :
                c.Titulo.Contains(alvo, ord)      ? 3 :
                c.Categoria.Contains(alvo, ord)   ? 4 :
                c.Conteudo.Contains(alvo, ord)    ? 5 : 6)
            .ThenByDescending(c => c.Macro.Favorito)
            .ThenBy(c => c.Titulo, StringComparer.Ordinal)
            .Select(c => c.Macro)
            .ToList();
    }

    public async Task<IEnumerable<Macro>> GetByCategoriaAsync(string categoria)
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QueryAsync<Macro>(
            $"""
             SELECT {Colunas} FROM macros
             WHERE categoria = @categoria AND ativo = 1
             ORDER BY favorito DESC, titulo COLLATE NOCASE
             """, new { categoria });
    }

    /// <summary>Única leitura que traz a imagem — é o que o formulário de edição usa.</summary>
    public async Task<Macro?> GetByIdAsync(int id)
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QuerySingleOrDefaultAsync<Macro>(
            $"SELECT {ColunasCompletas} FROM macros WHERE id = @id", new { id });
    }

    /// <summary>A coluna atalho é COLLATE NOCASE, então a comparação já ignora maiúsculas.</summary>
    public async Task<Macro?> GetByAtalhoAsync(string atalho)
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QuerySingleOrDefaultAsync<Macro>(
            $"SELECT {Colunas} FROM macros WHERE atalho = @atalho AND ativo = 1", new { atalho });
    }

    public async Task<Macro?> GetByAtalhoTeclaAsync(string digito)
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QuerySingleOrDefaultAsync<Macro>(
            $"SELECT {Colunas} FROM macros WHERE atalho_tecla = @digito AND ativo = 1", new { digito });
    }

    public async Task<int> InsertAsync(Macro macro)
    {
        using var conexao = await _ctx.AbrirAsync();
        return await InserirAsync(conexao, macro, null);
    }

    /// <summary>
    /// Uma transação para o lote inteiro. Sem ela o SQLite abriria uma transação implícita por
    /// linha, e cada uma custaria um fsync — a importação de um CSV de 200 macros levaria
    /// segundos em vez de milissegundos.
    /// </summary>
    public async Task BatchInsertAsync(IEnumerable<Macro> macros)
    {
        var lista = macros.ToList();
        if (lista.Count == 0) return;

        using var conexao = await _ctx.AbrirAsync();
        using var tx = conexao.BeginTransaction();
        foreach (var macro in lista) await InserirAsync(conexao, macro, tx);
        tx.Commit();
    }

    private static async Task<int> InserirAsync(SqliteConnection conexao, Macro macro, SqliteTransaction? tx)
    {
        const string sql = """
            INSERT INTO macros
                (atalho, titulo, conteudo, categoria, categoria_id, ativo, favorito,
                 atalho_tecla, imagem_base64, data_criacao, data_atualizacao)
            VALUES
                (@Atalho, @Titulo, @Conteudo, @Categoria, @CategoriaId, @Ativo, @Favorito,
                 @AtalhoTecla, @ImagemBase64, @DataCriacao, @DataAtualizacao)
            RETURNING id
            """;

        macro.DataCriacao = DateTime.Now;

        try
        {
            return await conexao.ExecuteScalarAsync<int>(sql, macro, tx);
        }
        catch (SqliteException ex) when (ErrosSqlite.EhDuplicidade(ex))
        {
            throw ErrosSqlite.Traduzir(ex);
        }
    }

    public async Task UpdateAsync(Macro macro)
    {
        const string sql = """
            UPDATE macros SET
                atalho           = @Atalho,
                titulo           = @Titulo,
                conteudo         = @Conteudo,
                categoria        = @Categoria,
                categoria_id     = @CategoriaId,
                ativo            = @Ativo,
                favorito         = @Favorito,
                atalho_tecla     = @AtalhoTecla,
                imagem_base64    = @ImagemBase64,
                data_atualizacao = @DataAtualizacao
            WHERE id = @Id
            """;

        macro.DataAtualizacao = DateTime.Now;

        using var conexao = await _ctx.AbrirAsync();
        try
        {
            await conexao.ExecuteAsync(sql, macro);
        }
        catch (SqliteException ex) when (ErrosSqlite.EhDuplicidade(ex))
        {
            throw ErrosSqlite.Traduzir(ex);
        }
    }

    // Os toggles gravam só a coluna que muda. Antes eram duas viagens (ler a linha inteira,
    // devolver a linha inteira), o que sobrescrevia com dados possivelmente velhos e mandava
    // o conteúdo completo da macro pela rede a cada clique em "favoritar".

    public async Task ToggleFavoritoAsync(int id, bool favorito)
    {
        using var conexao = await _ctx.AbrirAsync();
        await conexao.ExecuteAsync("UPDATE macros SET favorito = @favorito WHERE id = @id",
            new { id, favorito });
    }

    public async Task ToggleAtivoAsync(int id, bool ativo)
    {
        using var conexao = await _ctx.AbrirAsync();
        await conexao.ExecuteAsync("UPDATE macros SET ativo = @ativo WHERE id = @id",
            new { id, ativo });
    }

    public async Task DeleteAsync(int id)
    {
        using var conexao = await _ctx.AbrirAsync();
        await conexao.ExecuteAsync("DELETE FROM macros WHERE id = @id", new { id });
    }

    /// <summary>
    /// Lê a coluna desnormalizada, não a tabela categorias: é o que alimenta o filtro da
    /// listagem, e macros importadas podem ter um nome de categoria que nunca virou registro.
    /// </summary>
    public async Task<IEnumerable<string>> GetCategoriasAsync()
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QueryAsync<string>(
            """
            SELECT DISTINCT categoria FROM macros
            WHERE categoria IS NOT NULL AND categoria <> ''
            ORDER BY categoria COLLATE NOCASE
            """);
    }
}
