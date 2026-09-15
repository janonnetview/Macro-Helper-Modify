using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MacroHelper.UI;

public class HotkeyService : IDisposable
{
    private const int WM_HOTKEY   = 0x0312;
    private const int MOD_ALT     = 0x0001;
    private const int MOD_CONTROL = 0x0002;
    private const int MOD_SHIFT   = 0x0004;
    private const int VK_SPACE    = 0x20;
    private const int VK_F        = 0x46;
    private const int ID_BUSCADOR = 1;
    private const int ID_JANELA   = 2;
    private const int ID_PAINEL   = 3;

    /// <summary>Combinação fixa que traz a janela principal para a frente: Ctrl+Shift+Espaço.</summary>
    public const int ModificadorJanelaPrincipal = MOD_CONTROL | MOD_SHIFT;
    public const int TeclaJanelaPrincipal       = VK_SPACE;

    /// <summary>
    /// Ctrl+Alt+Shift+F, a combinação global do painel que não se anuncia.
    ///
    /// Não é o Ctrl+Shift+F puro de propósito: o RegisterHotKey tira a combinação do Windows
    /// INTEIRO, e Ctrl+Shift+F é "procurar em todos os arquivos" no VS Code, a busca de vários
    /// editores e a formatação de outros tantos. Registrar o atalho puro pagaria a conveniência
    /// daqui quebrando um atalho de todo dia em todos os outros programas. Com o Alt no meio a
    /// combinação está praticamente livre, e o Ctrl+Shift+F continua valendo com o SK
    /// MacroHelper na frente, onde não atropela ninguém (ver MainWindow).
    /// </summary>
    public const int ModificadorPainel = MOD_CONTROL | MOD_SHIFT | MOD_ALT;
    public const int TeclaPainel       = VK_F;

    private HwndSource? _source;
    private IntPtr      _hwnd;
    private bool        _registrado;
    private bool        _registradoJanela;
    private bool        _registradoPainel;

    public event Action? BuscadorRapidoSolicitado;

    /// <summary>Disparado no Ctrl+Shift+Espaço, de qualquer lugar do Windows.</summary>
    public event Action? JanelaPrincipalSolicitada;

    /// <summary>Disparado no Ctrl+Alt+Shift+F, de qualquer lugar do Windows.</summary>
    public event Action? PainelSolicitado;

    public void Iniciar(Window janela, int modificadores = MOD_CONTROL, int vk = VK_SPACE)
    {
        var helper = new WindowInteropHelper(janela);
        _hwnd   = helper.EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source.AddHook(WndProc);

        // A janela principal é registrada primeiro, de propósito: a combinação dela é fixa,
        // enquanto a da busca é remapeável. Se alguém tiver remapeado a busca para
        // Ctrl+Shift+Espaço numa versão anterior, quem perde o registro é a busca — que a
        // pessoa pode reconfigurar em Configurações — e não o atalho que não tem outro jeito
        // de ser mudado.
        _registradoJanela = RegisterHotKey(_hwnd, ID_JANELA,
                                           ModificadorJanelaPrincipal, TeclaJanelaPrincipal);

        _registradoPainel = RegisterHotKey(_hwnd, ID_PAINEL, ModificadorPainel, TeclaPainel);

        _registrado = RegisterHotKey(_hwnd, ID_BUSCADOR, modificadores, vk);
    }

    /// <summary>Troca a combinação do atalho de busca rápida em tempo real. Retorna false se a combinação já estiver em uso por outro app.</summary>
    public bool Reconfigurar(int modificadores, int vk)
    {
        if (_hwnd == IntPtr.Zero) return false;
        if (_registrado) UnregisterHotKey(_hwnd, ID_BUSCADOR);
        _registrado = RegisterHotKey(_hwnd, ID_BUSCADOR, modificadores, vk);
        return _registrado;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_HOTKEY) return IntPtr.Zero;

        switch (wParam.ToInt32())
        {
            case ID_BUSCADOR:
                BuscadorRapidoSolicitado?.Invoke();
                handled = true;
                break;

            case ID_JANELA:
                JanelaPrincipalSolicitada?.Invoke();
                handled = true;
                break;

            case ID_PAINEL:
                PainelSolicitado?.Invoke();
                handled = true;
                break;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registrado)       UnregisterHotKey(_hwnd, ID_BUSCADOR);
        if (_registradoJanela) UnregisterHotKey(_hwnd, ID_JANELA);
        if (_registradoPainel) UnregisterHotKey(_hwnd, ID_PAINEL);
        _source?.RemoveHook(WndProc);
        GC.SuppressFinalize(this);
    }

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
