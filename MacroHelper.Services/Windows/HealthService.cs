using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MacroHelper.Services;

public record InfoSaude(
    int     TotalMacros,
    long    MemoriaProcessoBytes,
    string  VersaoApp,
    double  CpuUsoPercent,
    long    MemoriaSistemaTotalBytes,
    long    MemoriaSistemaUsadaBytes,
    long    DiscoTotalBytes,
    long    DiscoLivreBytes);

/// <summary>Reúne indicadores simples de saúde do app para o painel de Configurações.</summary>
public class HealthService
{
    private readonly MacroService _macroService;

    public HealthService(MacroService macroService) => _macroService = macroService;

    public async Task<InfoSaude> ObterAsync()
    {
        var totalMacros = (await _macroService.ObterTodosAsync()).Count();
        var memoria     = Process.GetCurrentProcess().WorkingSet64;
        var versao      = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "—";

        var cpuUso = await ObterUsoCpuAsync();
        var (memSistemaTotal, memSistemaUsada) = ObterUsoMemoriaSistema();
        var (discoTotal, discoLivre) = ObterUsoDisco();

        return new InfoSaude(totalMacros, memoria, versao,
            cpuUso, memSistemaTotal, memSistemaUsada, discoTotal, discoLivre);
    }

    // ── Métricas da máquina local ──────────────────────────────────────────

    private static async Task<double> ObterUsoCpuAsync()
    {
        try
        {
            if (!GetSystemTimes(out var idle1, out var kernel1, out var user1)) return 0;
            await Task.Delay(300);
            if (!GetSystemTimes(out var idle2, out var kernel2, out var user2)) return 0;

            var idleDelta   = ParaLong(idle2)   - ParaLong(idle1);
            var kernelDelta = ParaLong(kernel2) - ParaLong(kernel1);
            var userDelta   = ParaLong(user2)   - ParaLong(user1);
            var totalDelta  = kernelDelta + userDelta;
            if (totalDelta <= 0) return 0;

            var uso = 1.0 - (double)idleDelta / totalDelta;
            return Math.Clamp(uso * 100.0, 0, 100);
        }
        catch { return 0; }

        static long ParaLong(FILETIME ft) => ((long)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
    }

    private static (long total, long usada) ObterUsoMemoriaSistema()
    {
        try
        {
            var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (!GlobalMemoryStatusEx(ref status)) return (0, 0);
            var total = (long)status.ullTotalPhys;
            var usada = total - (long)status.ullAvailPhys;
            return (total, usada);
        }
        catch { return (0, 0); }
    }

    private static (long total, long livre) ObterUsoDisco()
    {
        try
        {
            var raiz  = Path.GetPathRoot(Environment.SystemDirectory)!;
            var drive = new DriveInfo(raiz);
            return (drive.TotalSize, drive.AvailableFreeSpace);
        }
        catch { return (0, 0); }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}
