using MacroHelper.Services;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace MacroHelper.UI;

/// <summary>
/// Fica sabendo de tudo que é copiado no Windows e avisa quem guarda.
///
/// Por escuta e não por sondagem: <c>AddClipboardFormatListener</c> faz o Windows mandar uma
/// mensagem para a janela a cada mudança. A alternativa clássica — um timer comparando o
/// conteúdo a cada meio segundo — perderia duas cópias seguidas do mesmo trecho, acordaria o
/// processo o dia inteiro à toa, e ainda abriria a área de transferência centenas de vezes por
/// hora, justamente a operação que falha quando outro programa a está usando.
///
/// Mora na UI, e não em Services, pela mesma razão do <see cref="HotkeyService"/>: precisa de
/// uma janela de verdade para receber mensagem do Windows. As regras do que fica guardado
/// estão em <c>ClipboardService</c>, que não conhece Win32 nenhum.
/// </summary>
public class ClipboardMonitorService : IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;

    private HwndSource? _source;
    private IntPtr      _hwnd;
    private bool        _registrado;

    /// <summary>
    /// Textos que o PRÓPRIO app acabou de escrever na área de transferência, esperando para
    /// serem ignorados quando a mensagem correspondente chegar.
    ///
    /// Inserir uma macro escreve duas vezes: o texto da macro (para o Ctrl+V) e, meio segundo
    /// depois, a devolução do que a pessoa tinha copiado antes. Sem esta lista, as duas
    /// entrariam no histórico — a macro apareceria como se tivesse sido copiada, e o item
    /// verdadeiro seria empurrado para o topo de novo com a data errada.
    /// </summary>
    private readonly List<(string Texto, DateTime Quando)> _escritasProprias = new();

    /// <summary>
    /// Protege a lista acima, que é tocada por duas threads diferentes.
    ///
    /// Quem anuncia é o serviço de inserção, no meio de uma cadeia de <c>await</c> cuja
    /// continuação não tem como ser garantida na thread de UI; quem consome é o WndProc desta
    /// janela, que roda na thread de UI. Hoje as duas coincidem na prática — e é exatamente o
    /// tipo de coincidência que um <c>ConfigureAwait</c> acrescentado lá adiante desfaz, com o
    /// sintoma sendo uma <c>InvalidOperationException</c> dentro de um handler de mensagem do
    /// Windows.
    /// </summary>
    private readonly object _trava = new();

    /// <summary>
    /// Depois disto, uma escrita anunciada e nunca vista deixa de valer.
    ///
    /// Existe porque nem toda escrita gera mensagem — se o conteúdo for idêntico ao que já
    /// estava lá, alguns caminhos não notificam. Sem o vencimento, essa entrada órfã ficaria
    /// para sempre e engoliria uma cópia legítima igual feita horas depois.
    /// </summary>
    private static readonly TimeSpan JanelaDeSupressao = TimeSpan.FromSeconds(5);

    /// <summary>Desligado, o monitor continua registrado mas não repassa nada.</summary>
    public bool Ativo { get; set; } = true;

    /// <summary>Um texto acabou de ser copiado. Vem com o título da janela de origem.</summary>
    public event Action<string, string?>? TextoCopiado;

    /// <summary>Para onde vai uma falha na leitura — a UI liga no log do app.</summary>
    public Action<Exception>? AoFalhar { get; set; }

    public void Iniciar(Window janela)
    {
        var helper = new WindowInteropHelper(janela);
        _hwnd   = helper.EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);

        _registrado = AddClipboardFormatListener(_hwnd);
    }

    /// <summary>
    /// Avisa que este app escreveu <paramref name="texto"/> na área de transferência, para a
    /// mensagem correspondente ser ignorada.
    /// </summary>
    public void IgnorarProximaEscrita(string texto)
    {
        lock (_trava)
        {
            LimparSupressoesVencidas();
            _escritasProprias.Add((texto, DateTime.Now));
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE) ProcessarMudanca();
        return IntPtr.Zero;
    }

    /// <summary>
    /// Nunca marca <c>handled</c>: a mensagem de mudança da área de transferência é uma
    /// notificação em difusão, e engoli-la impediria qualquer outro gancho da mesma janela de
    /// vê-la.
    /// </summary>
    private void ProcessarMudanca()
    {
        try
        {
            if (!Ativo) return;
            if (ConteudoMarcadoComoSensivel()) return;

            var texto = TextInsertionService.LerClipboardTexto();
            if (string.IsNullOrWhiteSpace(texto)) return;
            if (EhEscritaPropria(texto)) return;

            TextoCopiado?.Invoke(texto, TituloDaJanelaAtiva());
        }
        catch (Exception ex) { AoFalhar?.Invoke(ex); }
    }

    private bool EhEscritaPropria(string texto)
    {
        lock (_trava)
        {
            LimparSupressoesVencidas();

            var indice = _escritasProprias.FindIndex(e => string.Equals(e.Texto, texto, StringComparison.Ordinal));
            if (indice < 0) return false;

            // Consumida: a MESMA macro inserida duas vezes seguidas anuncia duas escritas, e
            // cada anúncio vale por uma mensagem só.
            _escritasProprias.RemoveAt(indice);
            return true;
        }
    }

    /// <summary>Só é chamada de dentro do lock — não trava por conta própria.</summary>
    private void LimparSupressoesVencidas()
    {
        var limite = DateTime.Now - JanelaDeSupressao;
        _escritasProprias.RemoveAll(e => e.Quando < limite);
    }

    /// <summary>
    /// Respeita os dois formatos com que um programa pede para não ser guardado.
    ///
    /// Não é invenção deste app: são os mesmos que o histórico do próprio Windows (Win+V)
    /// obedece, e que os gerenciadores de senha — KeePass, 1Password, Bitwarden — marcam ao
    /// copiar uma senha. Honrá-los é o que impede o histórico de virar o lugar mais fácil da
    /// máquina para achar a senha que alguém copiou hoje de manhã.
    ///
    /// <c>ExcludeClipboardContentFromMonitorProcessing</c> basta existir. Já o
    /// <c>CanIncludeInClipboardHistory</c> carrega um DWORD, e é o VALOR dele que decide —
    /// presente e igual a 1 significa autorizado, e tratá-lo como proibição descartaria cópia
    /// legítima de quem faz a marcação corretamente.
    /// </summary>
    private static bool ConteudoMarcadoComoSensivel()
    {
        var excluir = RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
        if (excluir != 0 && IsClipboardFormatAvailable(excluir)) return true;

        var podeGuardar = RegisterClipboardFormat("CanIncludeInClipboardHistory");
        if (podeGuardar == 0 || !IsClipboardFormatAvailable(podeGuardar)) return false;

        var valor = LerDwordDoClipboard(podeGuardar);
        return valor is 0;
    }

    /// <summary>Lê um formato de área de transferência que contém um DWORD. Null quando não dá para ler.</summary>
    private static int? LerDwordDoClipboard(uint formato)
    {
        for (var tentativa = 0; tentativa < 5; tentativa++)
        {
            if (OpenClipboard(IntPtr.Zero)) break;
            Thread.Sleep(20);
            if (tentativa == 4) return null;
        }

        try
        {
            var handle = GetClipboardData(formato);
            if (handle == IntPtr.Zero) return null;

            var ptr = GlobalLock(handle);
            if (ptr == IntPtr.Zero) return null;

            try   { return Marshal.ReadInt32(ptr); }
            finally { GlobalUnlock(handle); }
        }
        catch { return null; }
        finally { CloseClipboard(); }
    }

    /// <summary>
    /// De onde o texto veio, pelo título da janela em foco.
    ///
    /// É o mesmo dado que o log de uso já guarda, e serve para a mesma coisa: três horas
    /// depois, "Chamado 4412 — Chrome" distingue dois trechos parecidos que sozinhos seriam
    /// indistinguíveis na lista.
    /// </summary>
    private static string? TituloDaJanelaAtiva()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            var buf = new StringBuilder(256);
            GetWindowText(hwnd, buf, buf.Capacity);
            return buf.Length > 0 ? buf.ToString() : null;
        }
        catch { return null; }
    }

    public void Dispose()
    {
        if (_registrado && _hwnd != IntPtr.Zero) RemoveClipboardFormatListener(_hwnd);
        _registrado = false;
        _source?.RemoveHook(WndProc);
        lock (_trava) _escritasProprias.Clear();
        GC.SuppressFinalize(this);
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormat(string lpszFormat);
    [DllImport("user32.dll")] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint uFormat);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr hMem);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder t, int c);
}
