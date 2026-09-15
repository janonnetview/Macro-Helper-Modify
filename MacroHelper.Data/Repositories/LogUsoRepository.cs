using Dapper;
using MacroHelper.Core.Entities;
using MacroHelper.Data.Context;

namespace MacroHelper.Data.Repositories;

public class LogUsoRepository
{
    private readonly SqliteContext _ctx;
    public LogUsoRepository(SqliteContext ctx) => _ctx = ctx;

    private const string Colunas = """
        id, aplicativo, caracteres,
        macro_id     AS MacroId,
        macro_titulo AS MacroTitulo,
        macro_atalho AS MacroAtalho,
        data_uso     AS DataUso
        """;

    public async Task RegistrarAsync(LogUso log)
    {
        log.DataUso = DateTime.Now;

        using var conexao = await _ctx.AbrirAsync();
        await conexao.ExecuteAsync(
            """
            INSERT INTO log_uso (macro_id, macro_titulo, macro_atalho, aplicativo, data_uso, caracteres)
            VALUES (@MacroId, @MacroTitulo, @MacroAtalho, @Aplicativo, @DataUso, @Caracteres)
            """, log);
    }

    /// <summary>
    /// A agregação voltou para o SQL: com o Postgrest ela precisava rodar em C# sobre o
    /// período inteiro baixado, porque o cliente não expunha GROUP BY.
    /// </summary>
    public async Task<IEnumerable<(string Titulo, string Atalho, int Total)>> GetTopMacrosAsync(
        DateTime de, DateTime ate, int limite = 10)
    {
        using var conexao = await _ctx.AbrirAsync();
        var linhas = await conexao.QueryAsync<TopMacro>(
            """
            SELECT macro_titulo AS Titulo, macro_atalho AS Atalho, COUNT(*) AS Total
            FROM log_uso
            WHERE data_uso >= @de AND data_uso <= @ate
            GROUP BY macro_titulo, macro_atalho
            ORDER BY Total DESC
            LIMIT @limite
            """, new { de, ate, limite });

        return linhas.Select(l => (l.Titulo, l.Atalho, l.Total));
    }

    /// <summary>O Dapper não materializa ValueTuple — este DTO existe só para a projeção acima.</summary>
    private sealed class TopMacro
    {
        public string Titulo { get; set; } = string.Empty;
        public string Atalho { get; set; } = string.Empty;
        public int    Total  { get; set; }
    }

    public async Task<IEnumerable<LogUso>> GetRecentesAsync(int limite = 200)
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QueryAsync<LogUso>(
            $"SELECT {Colunas} FROM log_uso ORDER BY data_uso DESC LIMIT @limite", new { limite });
    }

    public async Task<IEnumerable<LogUso>> GetByPeriodoAsync(DateTime de, DateTime ate)
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QueryAsync<LogUso>(
            $"""
             SELECT {Colunas} FROM log_uso
             WHERE data_uso >= @de AND data_uso <= @ate
             ORDER BY data_uso DESC
             """, new { de, ate });
    }

    /// <summary>
    /// O parâmetro é comparado como texto contra 'yyyy-MM-dd HH:mm:ss', e nesse formato a
    /// ordem alfabética é a ordem cronológica — por isso o >= usa o índice em vez de
    /// converter linha a linha.
    /// </summary>
    public async Task<int> GetTotalHojeAsync()
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM log_uso WHERE data_uso >= @hoje", new { hoje = DateTime.Today });
    }

    /// <summary>
    /// Retenção de 2 anos, rodada no startup. Substitui as ~40 linhas de arquivamento manual
    /// e a tabela de resumo que existiam para o mesmo fim.
    ///
    /// 'localtime' é obrigatório: datetime('now') do SQLite devolve UTC, e o banco guarda hora
    /// local. Sem ele o corte ficaria 3 horas deslocado — irrelevante em dois anos, mas a
    /// inconsistência de fuso é justamente o tipo de coisa que reaparece em outro cálculo depois.
    /// </summary>
    public async Task<int> LimparAntigosAsync()
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.ExecuteAsync(
            "DELETE FROM log_uso WHERE data_uso < datetime('now', 'localtime', '-2 years')");
    }
}
