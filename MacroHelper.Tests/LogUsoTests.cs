using MacroHelper.Core.Entities;
using MacroHelper.Services;

namespace MacroHelper.Tests;

public class LogUsoTests
{
    private static async Task Registrar(BancoDeTeste banco, string titulo, int vezes, int caracteres = 100)
    {
        for (var i = 0; i < vezes; i++)
            await banco.Logs.RegistrarAsync(new LogUso
            {
                MacroTitulo = titulo, MacroAtalho = titulo, Caracteres = caracteres,
            });
    }

    /// <summary>
    /// A agregação voltou para o SQL na Onda 2: com o PostgREST ela precisava rodar em C# sobre
    /// o período inteiro baixado, porque o cliente não expunha GROUP BY.
    /// </summary>
    [Fact]
    public async Task TopMacros_AgrupaEOrdenaPorUso()
    {
        using var banco = new BancoDeTeste();
        await Registrar(banco, "muito-usada", 5);
        await Registrar(banco, "pouco-usada", 2);
        await Registrar(banco, "media", 3);

        var top = (await banco.Logs.GetTopMacrosAsync(DateTime.Today, DateTime.Now.AddMinutes(1))).ToList();

        Assert.Equal(("muito-usada", "muito-usada", 5), top[0]);
        Assert.Equal(3, top[1].Total);
        Assert.Equal(2, top[2].Total);
    }

    [Fact]
    public async Task TopMacros_RespeitaOLimite()
    {
        using var banco = new BancoDeTeste();
        for (var i = 0; i < 8; i++) await Registrar(banco, $"macro-{i}", i + 1);

        var top = await banco.Logs.GetTopMacrosAsync(DateTime.Today, DateTime.Now.AddMinutes(1), limite: 3);
        Assert.Equal(3, top.Count());
    }

    [Fact]
    public async Task TopMacros_IgnoraOQueEstaForaDoPeriodo()
    {
        using var banco = new BancoDeTeste();
        await Registrar(banco, "de-hoje", 2);

        banco.Executar("""
            INSERT INTO log_uso (macro_titulo, macro_atalho, data_uso, caracteres)
            VALUES ('do-ano-passado', 'do-ano-passado', '2025-01-15 10:00:00', 100)
            """);

        var top = await banco.Logs.GetTopMacrosAsync(DateTime.Today, DateTime.Now.AddMinutes(1));
        Assert.Equal("de-hoje", top.Single().Titulo);
    }

    [Fact]
    public async Task TotalHoje_NaoContaOsDiasAnteriores()
    {
        using var banco = new BancoDeTeste();
        await Registrar(banco, "hoje", 3);

        banco.Executar("""
            INSERT INTO log_uso (macro_titulo, macro_atalho, data_uso, caracteres)
            VALUES ('ontem', 'ontem', '2026-01-01 10:00:00', 50)
            """);

        Assert.Equal(3, await banco.Logs.GetTotalHojeAsync());
    }

    /// <summary>
    /// Retenção de 2 anos, rodada no startup. O 'localtime' no SQL é obrigatório porque
    /// datetime('now') do SQLite devolve UTC e o banco guarda hora local.
    /// </summary>
    [Fact]
    public async Task LimparAntigos_ApagaOQuePassouDeDoisAnosEMantemORestante()
    {
        using var banco = new BancoDeTeste();
        await Registrar(banco, "recente", 2);

        var antigo = DateTime.Now.AddYears(-3).ToString("yyyy-MM-dd HH:mm:ss");
        var limite = DateTime.Now.AddYears(-2).AddDays(1).ToString("yyyy-MM-dd HH:mm:ss");
        banco.Executar($"""
            INSERT INTO log_uso (macro_titulo, macro_atalho, data_uso, caracteres)
            VALUES ('velho', 'velho', '{antigo}', 10),
                   ('quase', 'quase', '{limite}', 10)
            """);

        var apagados = await banco.Logs.LimparAntigosAsync();

        Assert.Equal(1, apagados);
        Assert.Equal(3, banco.Escalar<int>("SELECT COUNT(*) FROM log_uso"));
        Assert.Equal(0, banco.Escalar<int>("SELECT COUNT(*) FROM log_uso WHERE macro_titulo = 'velho'"));
    }

    [Fact]
    public async Task Recentes_VemDoMaisNovoParaOMaisVelho()
    {
        using var banco = new BancoDeTeste();

        banco.Executar("""
            INSERT INTO log_uso (macro_titulo, macro_atalho, data_uso, caracteres) VALUES
                ('primeira', 'primeira', '2026-08-20 08:00:00', 10),
                ('ultima',   'ultima',   '2026-08-20 17:00:00', 10),
                ('meio',     'meio',     '2026-08-20 12:00:00', 10)
            """);

        var ordem = (await banco.Logs.GetRecentesAsync()).Select(l => l.MacroTitulo).ToList();
        Assert.Equal(["ultima", "meio", "primeira"], ordem);
    }

    /// <summary>Estimativa: 200 caracteres/minuto digitando, menos 2s por inserção da macro.</summary>
    [Fact]
    public async Task MinutosEconomizados_DescontamOTempoDaPropriaInsercao()
    {
        using var banco = new BancoDeTeste();
        var servico = new LogUsoService(banco.Logs);

        // 10 inserções de 200 caracteres = 10 minutos digitando, menos 10 × 2s = 20s.
        await Registrar(banco, "longa", vezes: 10, caracteres: 200);

        var minutos = await servico.EstimarMinutosEconomizadosAsync(DateTime.Today, DateTime.Now.AddMinutes(1));

        Assert.Equal(10 - (20 / 60.0), minutos, precision: 3);
    }

    [Fact]
    public async Task MinutosEconomizados_NuncaFicamNegativos()
    {
        using var banco = new BancoDeTeste();
        var servico = new LogUsoService(banco.Logs);

        // Macros minúsculas: o tempo de inserção supera o de digitação.
        await Registrar(banco, "curta", vezes: 20, caracteres: 1);

        Assert.Equal(0, await servico.EstimarMinutosEconomizadosAsync(DateTime.Today, DateTime.Now.AddMinutes(1)));
    }
}
