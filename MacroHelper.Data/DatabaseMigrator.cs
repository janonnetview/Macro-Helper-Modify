using MacroHelper.Data.Context;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MacroHelper.Data;

/// <summary>
/// Aplica os scripts de <c>Sql/NNN_*.sql</c> que ainda não rodaram, controlando a versão
/// pelo <c>PRAGMA user_version</c> do próprio arquivo do banco.
///
/// Não existe tabela de controle de migração: o user_version é um inteiro que já vem no
/// cabeçalho de todo banco SQLite, custa zero e não pode ficar fora de sincronia com o
/// arquivo que ele descreve.
/// </summary>
public static class DatabaseMigrator
{
    private const string PrefixoRecurso = "MacroHelper.Data.Sql.";
    private static readonly Regex _numeroDoScript = new(@"^(\d+)_", RegexOptions.Compiled);

    /// <summary>Cria o banco se não existir e aplica as migrações pendentes. Idempotente.</summary>
    /// <returns>Quantos scripts foram aplicados nesta chamada.</returns>
    public static int Migrar(SqliteContext ctx)
    {
        DataSqliteHandler.Registrar();

        using var conexao = ctx.Abrir();

        // WAL fica gravado no arquivo e vale para sempre — só precisa ser aplicado uma vez,
        // mas repetir é barato. Precisa rodar FORA de transação: dentro de uma, o SQLite
        // ignora a troca e devolve o modo antigo, sem erro.
        Executar(conexao, "PRAGMA journal_mode = WAL;");

        var versaoAtual = LerVersao(conexao);

        var pendentes = ScriptsEmbutidos()
            .Where(s => s.Versao > versaoAtual)
            .OrderBy(s => s.Versao)
            .ToList();

        foreach (var script in pendentes)
        {
            using var tx = conexao.BeginTransaction();

            // DDL é transacional no SQLite: se o script falhar no meio, nem as tabelas nem o
            // bump de versão são gravados. O banco nunca fica meio-migrado.
            Executar(conexao, script.Sql, tx);

            // PRAGMA não aceita parâmetro — precisa ser interpolado. O valor vem do nome de um
            // recurso embutido no próprio assembly e já passou por int.Parse, então não há
            // entrada externa nesta string.
            Executar(conexao, $"PRAGMA user_version = {script.Versao};", tx);

            tx.Commit();
        }

        return pendentes.Count;
    }

    private static int LerVersao(SqliteConnection conexao)
    {
        using var cmd = conexao.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void Executar(SqliteConnection conexao, string sql, SqliteTransaction? tx = null)
    {
        using var cmd = conexao.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = tx;
        cmd.ExecuteNonQuery();
    }

    private static IEnumerable<(int Versao, string Sql)> ScriptsEmbutidos()
    {
        var assembly = typeof(DatabaseMigrator).Assembly;

        foreach (var recurso in assembly.GetManifestResourceNames())
        {
            if (!recurso.StartsWith(PrefixoRecurso, StringComparison.Ordinal)) continue;

            var arquivo = recurso[PrefixoRecurso.Length..];
            var match   = _numeroDoScript.Match(arquivo);
            if (!match.Success) continue;

            yield return (int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                          LerRecurso(assembly, recurso));
        }
    }

    private static string LerRecurso(Assembly assembly, string nome)
    {
        using var stream = assembly.GetManifestResourceStream(nome)
            ?? throw new InvalidOperationException($"Script de migração não encontrado: {nome}");
        using var leitor = new StreamReader(stream);
        return leitor.ReadToEnd();
    }
}
