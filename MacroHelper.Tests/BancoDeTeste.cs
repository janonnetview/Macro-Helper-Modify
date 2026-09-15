using MacroHelper.Data;
using MacroHelper.Data.Context;
using MacroHelper.Data.Repositories;
using MacroHelper.Services;
using Microsoft.Data.Sqlite;
using System.IO;

namespace MacroHelper.Tests;

/// <summary>
/// Um arquivo SQLite descartável por teste, já migrado. É o que torna os testes de repositório
/// baratos: cada um começa de um banco vazio e real, sem mock e sem rede.
/// </summary>
public sealed class BancoDeTeste : IDisposable
{
    private readonly string _caminho;

    public SqliteContext Contexto { get; }

    public BancoDeTeste()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "macrohelper-testes");
        Directory.CreateDirectory(pasta);
        _caminho = Path.Combine(pasta, $"{Guid.NewGuid():N}.db");

        Contexto = new SqliteContext(_caminho);
        DatabaseMigrator.Migrar(Contexto);
    }

    // Repositórios sob demanda: não guardam estado, então criar um por uso sai mais barato
    // que manter campos e deixa cada teste pedir só o que usa.
    public MacroRepository          Macros    => new(Contexto);
    public CategoriaRepository      Categorias => new(Contexto);
    public MacroVersaoRepository    Versoes   => new(Contexto);
    public VariavelGlobalRepository Variaveis => new(Contexto);
    public LogUsoRepository         Logs      => new(Contexto);
    public TarefaRepository         Tarefas   => new(Contexto);
    public CompromissoRepository    Compromissos => new(Contexto);
    public NotaRepository           Notas     => new(Contexto);
    public ClipboardRepository      Clipboard => new(Contexto);

    /// <summary>Consulta crua, para afirmar sobre o que está GRAVADO e não sobre o que o repositório devolve.</summary>
    public T Escalar<T>(string sql)
    {
        using var conexao = Contexto.Abrir();
        using var cmd = conexao.CreateCommand();
        cmd.CommandText = sql;
        var valor = cmd.ExecuteScalar();
        return valor is null or DBNull ? default! : (T)Convert.ChangeType(valor, typeof(T));
    }

    public void Executar(string sql)
    {
        using var conexao = Contexto.Abrir();
        using var cmd = conexao.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        // Sem isto o handle do pool continua segurando o arquivo e o Delete lança no Windows —
        // o teste passa, mas deixa um .db para trás a cada execução.
        SqliteConnection.ClearAllPools();

        foreach (var sufixo in new[] { "", "-wal", "-shm" })
        {
            // Um arquivo temporário que sobreviveu não é motivo para reprovar um teste verde.
            try { File.Delete(_caminho + sufixo); } catch (IOException) { }
        }
    }
}

/// <summary>Relógio de mentira: é o que permite testar "venceu enquanto o app estava fechado".</summary>
public sealed class RelogioFalso : IRelogio
{
    public DateTime Agora { get; set; }
    public RelogioFalso(DateTime inicio) => Agora = inicio;
    public void Avancar(TimeSpan quanto) => Agora += quanto;
}
