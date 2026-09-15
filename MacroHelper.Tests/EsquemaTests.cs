using MacroHelper.Data;
using MacroHelper.Data.Context;
using Microsoft.Data.Sqlite;
using System.IO;

namespace MacroHelper.Tests;

/// <summary>
/// Garantias do banco em si — o que o <c>DatabaseMigrator</c> promete e o que os PRAGMAs
/// precisam estar valendo para o resto do esquema funcionar.
/// </summary>
public class EsquemaTests
{
    [Fact]
    public void Migrar_BancoNovo_AplicaTodosOsScripts()
    {
        using var banco = new BancoDeTeste();

        // O construtor do fixture já migrou; a versão é a do último script de Sql/.
        Assert.Equal(5, banco.Escalar<int>("PRAGMA user_version"));
        Assert.Equal("ok", banco.Escalar<string>("PRAGMA integrity_check"));
    }

    [Fact]
    public void Migrar_SegundaChamada_NaoAplicaNada()
    {
        using var banco = new BancoDeTeste();

        // Idempotência importa porque o migrator roda em TODA abertura do app.
        Assert.Equal(0, DatabaseMigrator.Migrar(banco.Contexto));
        Assert.Equal(5, banco.Escalar<int>("PRAGMA user_version"));
    }

    [Fact]
    public void Migrar_CriaTodasAsTabelasEsperadas()
    {
        using var banco = new BancoDeTeste();

        using var conexao = banco.Contexto.Abrir();
        using var cmd = conexao.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
        using var leitor = cmd.ExecuteReader();

        var tabelas = new List<string>();
        while (leitor.Read()) tabelas.Add(leitor.GetString(0));

        Assert.Equal(
            ["categorias", "clipboard", "compromissos", "log_uso", "macro_versoes", "macros", "notas",
             "tarefas", "variaveis_globais"],
            tabelas.Order().ToArray());
    }

    /// <summary>
    /// O PRAGMA foreign_keys é POR CONEXÃO e vem DESLIGADO por padrão. Se a connection string
    /// perder o ForeignKeys=True, todo ON DELETE SET NULL/CASCADE do esquema vira decoração —
    /// sem erro nenhum, só integridade que para de ser aplicada.
    /// </summary>
    [Fact]
    public void ChavesEstrangeiras_EstaoLigadasEmTodaConexao()
    {
        using var banco = new BancoDeTeste();

        for (var i = 0; i < 3; i++)
            Assert.Equal(1, banco.Escalar<int>("PRAGMA foreign_keys"));
    }

    /// <summary>
    /// O WAL precisa ser aplicado FORA de transação: dentro de uma, o SQLite ignora a troca e
    /// devolve o modo antigo sem lançar nada. Esta asserção é o que detecta essa regressão.
    /// </summary>
    [Fact]
    public void JournalMode_EhWal()
    {
        using var banco = new BancoDeTeste();
        Assert.Equal("wal", banco.Escalar<string>("PRAGMA journal_mode"));
    }

    /// <summary>Sem AUTOINCREMENT o SQLite reaproveita id de linha excluída, e um log_uso órfão passaria a apontar para a macro errada.</summary>
    [Fact]
    public async Task IdDeMacroExcluida_NaoEhReaproveitado()
    {
        using var banco = new BancoDeTeste();
        var repo = banco.Macros;

        var primeiro = await repo.InsertAsync(NovaMacro("primeira"));
        await repo.DeleteAsync(primeiro);
        var segundo = await repo.InsertAsync(NovaMacro("segunda"));

        Assert.True(segundo > primeiro);
    }

    [Fact]
    public void Migrar_CriaAPastaDoArquivoSeNaoExistir()
    {
        var pasta = Path.Combine(Path.GetTempPath(), $"mh-pasta-{Guid.NewGuid():N}", "sub");
        var caminho = Path.Combine(pasta, "banco.db");

        try
        {
            // Na primeira execução do app a pasta %AppData%\MacroHelper ainda não existe.
            DatabaseMigrator.Migrar(new SqliteContext(caminho));
            Assert.True(File.Exists(caminho));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(Path.GetDirectoryName(pasta)!, recursive: true); } catch (IOException) { }
        }
    }

    internal static Core.Entities.Macro NovaMacro(string atalho, string? conteudo = null) => new()
    {
        Atalho   = atalho,
        Titulo   = atalho,
        Conteudo = conteudo ?? $"conteúdo de {atalho}",
    };
}
