using MacroHelper.Core.Entities;
using MacroHelper.Data.Repositories;
using System.Runtime.InteropServices;
using System.Text;

namespace MacroHelper.Services;

public class LogUsoService
{
    private readonly LogUsoRepository _repo;
    public LogUsoService(LogUsoRepository repo)
    {
        _repo = repo;
    }

    // Estimativa: usuário digitaria a uma média de 200 caracteres/minuto; a inserção da macro leva ~2s.
    private const double CHARS_POR_MINUTO_DIGITANDO = 200.0;
    private const double SEGUNDOS_POR_INSERCAO       = 2.0;

    public async Task RegistrarAsync(int? macroId, string titulo, string atalho, int caracteres = 0)
    {
        var app = ObterAplicativoAtivo();
        await _repo.RegistrarAsync(new LogUso
        {
            MacroId = macroId, MacroTitulo = titulo,
            MacroAtalho = atalho, Aplicativo = app,
            Caracteres = caracteres
        });
    }

    public async Task<IEnumerable<LogUso>> ObterRecentesAsync(int limite = 200)
        => await _repo.GetRecentesAsync(limite);

    public async Task<int> TotalHojeAsync() => await _repo.GetTotalHojeAsync();

    public async Task<IEnumerable<(string Titulo, string Atalho, int Total)>> ObterTopMacrosAsync(DateTime de, DateTime ate, int limite = 10)
        => await _repo.GetTopMacrosAsync(de, ate, limite);

    /// <summary>Apaga registros com mais de 2 anos. Chamado uma vez no startup.</summary>
    public async Task<int> LimparHistoricoAntigoAsync() => await _repo.LimparAntigosAsync();

    /// <summary>Estima o tempo economizado (em minutos) com base nos caracteres inseridos via macro no período.</summary>
    public async Task<double> EstimarMinutosEconomizadosAsync(DateTime de, DateTime ate)
    {
        var registros = (await _repo.GetByPeriodoAsync(de, ate)).ToList();
        var totalChars = registros.Sum(r => r.Caracteres);
        var minutosDigitacao = totalChars / CHARS_POR_MINUTO_DIGITANDO;
        var minutosGastos    = registros.Count * (SEGUNDOS_POR_INSERCAO / 60.0);
        return Math.Max(0, minutosDigitacao - minutosGastos);
    }

    private static string ObterAplicativoAtivo()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            var buf  = new StringBuilder(256);
            GetWindowText(hwnd, buf, 256);
            return buf.Length > 0 ? buf.ToString() : "Desconhecido";
        }
        catch { return "Desconhecido"; }
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr h, StringBuilder t, int c);
}
