using Microsoft.Data.Sqlite;
using System.IO;

namespace MacroHelper.Data.Context;

/// <summary>
/// Aponta para o arquivo SQLite do app e abre conexões para os repositórios.
/// Substitui o SupabaseContext: não há rede, servidor nem autenticação envolvidos.
/// </summary>
public sealed class SqliteContext
{
    /// <summary>%AppData%\MacroHelper\macrohelper.db — mesma pasta de erros.log e das configurações.</summary>
    public static string CaminhoPadrao => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MacroHelper", "macrohelper.db");

    public string Caminho         { get; }
    public string ConnectionString { get; }

    public SqliteContext() : this(CaminhoPadrao) { }

    public SqliteContext(string caminho)
    {
        Caminho = caminho;

        var pasta = Path.GetDirectoryName(caminho);
        if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = caminho,
            Mode       = SqliteOpenMode.ReadWriteCreate,

            // O PRAGMA foreign_keys é POR CONEXÃO e vem desligado por padrão. Declarar aqui
            // faz o provider ligá-lo em toda conexão aberta — inclusive as que o pool reutiliza.
            // Sem isso, ON DELETE SET NULL e ON DELETE CASCADE do schema seriam decorativos.
            ForeignKeys = true,

            // Falhar em 3s é melhor do que congelar a UI esperando um lock. Num app de um
            // processo só, contenção acima disso significa bug, e um erro visível é o que
            // permite descobrir isso — o padrão de 30s apenas esconderia o problema.
            DefaultTimeout = 3,
        }.ToString();
    }

    /// <summary>
    /// Abre uma conexão nova. Os repositórios abrem e descartam uma por chamada: com o pool
    /// ligado (padrão do Microsoft.Data.Sqlite) o handle do arquivo continua aberto por baixo,
    /// então o custo é desprezível e some qualquer dúvida sobre tempo de vida de conexão.
    /// </summary>
    public SqliteConnection Abrir()
    {
        var conexao = new SqliteConnection(ConnectionString);
        conexao.Open();
        return conexao;
    }

    public async Task<SqliteConnection> AbrirAsync()
    {
        var conexao = new SqliteConnection(ConnectionString);
        await conexao.OpenAsync();
        return conexao;
    }
}
