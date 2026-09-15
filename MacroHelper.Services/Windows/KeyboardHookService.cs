using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MacroHelper.Services;

public class KeyboardHookService : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN     = 0x0100;
    private const int WM_SYSKEYDOWN  = 0x0104;

    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethod.LowLevelKeyboardProc _proc;
    private readonly StringBuilder _buffer     = new();
    private readonly StringBuilder _textoLivre = new();
    private readonly Dictionary<string, int> _frasesVistas = new();
    private const int FRASE_MIN_LEN      = 25;
    private const int FRASE_MAX_LEN      = 400;
    private const int FRASE_LIMIAR       = 3;
    private const int FRASE_MIN_PALAVRAS = 3;

    /// <summary>
    /// Em qual janela o texto livre está sendo acumulado. Trocar de janela zera o acúmulo —
    /// ver <see cref="EsquecerSeTrocouDeJanela"/>.
    /// </summary>
    private IntPtr _janelaDoTextoLivre = IntPtr.Zero;

    /// <summary>Caractere que inicia a detecção de gatilho. Padrão: '/'</summary>
    public char Prefixo { get; set; } = '/';

    /// <summary>Tecla final de Ctrl+Alt+&lt;tecla&gt; para repetir a última macro. Padrão: '0' (0x30).</summary>
    public int VkRepetirUltima { get; set; } = 0x30;

    /// <summary>Quando false, desliga a detecção de frases repetidas.</summary>
    public bool DeteccaoFraseAtiva { get; set; } = true;

    public event EventHandler<string>? GatilhoDetectado;
    public event EventHandler?         GatilhoCancelado;
    public event EventHandler?         UndoSolicitado;
    public event EventHandler<string>? AtalhoTecladoPressionado;
    public event EventHandler<string>? FraseRepetidaDetectada;
    public event EventHandler?         RepetirUltimaSolicitado;

    public KeyboardHookService() => _proc = HookCallback;

    public void Iniciar()  => _hookId = SetHook(_proc);

    public void Parar()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethod.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        // Pausar o monitoramento também esquece o que já foi digitado. "Pausado" com meia
        // frase ainda guardada na memória do processo não é o que a palavra promete.
        EsquecerTudo();
    }

    /// <summary>
    /// Joga fora todo texto acumulado — o gatilho em curso, a frase em construção e o histórico
    /// de frases repetidas. Chamado ao pausar o hook e ao desligar a sugestão proativa.
    /// </summary>
    public void EsquecerTudo()
    {
        _buffer.Clear();
        _textoLivre.Clear();
        _frasesVistas.Clear();
        _janelaDoTextoLivre = IntPtr.Zero;
    }

    private IntPtr SetHook(NativeMethod.LowLevelKeyboardProc proc)
    {
        using var p = Process.GetCurrentProcess();
        using var m = p.MainModule!;
        return NativeMethod.SetWindowsHookEx(WH_KEYBOARD_LL, proc,
            NativeMethod.GetModuleHandle(m.ModuleName!), 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            var vkCode = Marshal.ReadInt32(lParam);
            ProcessKey(vkCode);
        }
        return NativeMethod.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void ProcessKey(int vkCode)
    {
        EsquecerSeTrocouDeJanela();

        var ctrl  = (NativeMethod.GetAsyncKeyState(0x11) & 0x8000) != 0;
        var alt   = (NativeMethod.GetAsyncKeyState(0x12) & 0x8000) != 0;
        var shift = (NativeMethod.GetAsyncKeyState(0x10) & 0x8000) != 0;

        // Atalho com Ctrl ou Alt corta a frase em curso. Não é sobre privacidade: é que
        // Ctrl+A seguido de digitação é "apaguei tudo e recomecei", e emendar os dois lados
        // produziria uma frase que nunca existiu na tela de ninguém.
        if (ctrl || alt) _textoLivre.Clear();

        // Ctrl+Shift+Z — desfazer última inserção
        if (ctrl && shift && vkCode == 0x5A)
        {
            UndoSolicitado?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Ctrl+Alt+1..9 — atalho de teclado dedicado por macro
        if (ctrl && alt && vkCode is >= 0x31 and <= 0x39)
        {
            var digito = ((char)vkCode).ToString();
            AtalhoTecladoPressionado?.Invoke(this, digito);
            return;
        }

        // Ctrl+Alt+<tecla> — repete a última macro inserida (tecla remapeável)
        if (ctrl && alt && vkCode == VkRepetirUltima)
        {
            RepetirUltimaSolicitado?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Escape — cancela
        if (vkCode == 0x1B)
        {
            _buffer.Clear();
            _textoLivre.Clear();
            GatilhoCancelado?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Backspace
        if (vkCode == 0x08)
        {
            if (_buffer.Length > 0)
                _buffer.Remove(_buffer.Length - 1, 1);
            if (_textoLivre.Length > 0)
                _textoLivre.Remove(_textoLivre.Length - 1, 1);

            if (_buffer.Length >= 1 && _buffer[0] == Prefixo)
                GatilhoDetectado?.Invoke(this, _buffer.ToString());
            else if (_buffer.Length == 0)
                GatilhoCancelado?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Tab — cancela gatilho, mas não conta como fim de frase
        if (vkCode == 0x09)
        {
            _buffer.Clear();
            GatilhoCancelado?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Enter / Space — cancela gatilho; Enter também fecha a "frase" para detecção de repetição
        if (vkCode is 0x0D or 0x20)
        {
            _buffer.Clear();
            GatilhoCancelado?.Invoke(this, EventArgs.Empty);
            if (vkCode == 0x0D) FecharFraseLivre();
            return;
        }

        // Ignora teclas modificadoras e de controle
        if (IsModifierOrControl(vkCode)) return;

        var c = VkToChar(vkCode);
        if (c == '\0') return;

        _buffer.Append(c);
        AcumularTextoLivre(c);

        if (_buffer.Length > 80)
        {
            _buffer.Clear();
            GatilhoCancelado?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_buffer.Length >= 2 && _buffer[0] == Prefixo)
            GatilhoDetectado?.Invoke(this, _buffer.ToString());
        else if (_buffer.Length == 0 || _buffer[0] != Prefixo)
            _buffer.Clear();
    }

    /// <summary>
    /// Descarta o texto acumulado quando o foco muda de janela.
    ///
    /// O acumulador existe para achar frases que se repetem e sugerir virá-las macro, e uma
    /// "frase" costurada entre duas janelas diferentes nunca foi digitada em lugar nenhum.
    ///
    /// A razão mais séria é a outra: este hook lê TUDO, inclusive campo de senha — para o
    /// Windows não há diferença, o caractere chega igual. Zerar na troca de janela encurta ao
    /// máximo a vida do que foi digitado num campo desses, e some junto com a janela em vez de
    /// ficar num StringBuilder até completar 400 caracteres.
    ///
    /// O buffer do GATILHO não é tocado aqui de propósito. Ele guarda poucos caracteres a
    /// partir de "/", e o popup de sugestões pode, em algumas situações, virar a janela em
    /// foco por um instante — zerar o gatilho junto cancelaria a sugestão que acabou de
    /// aparecer, que é a funcionalidade principal do app.
    /// </summary>
    private void EsquecerSeTrocouDeJanela()
    {
        var janela = NativeMethod.GetForegroundWindow();
        if (janela == _janelaDoTextoLivre) return;

        _janelaDoTextoLivre = janela;
        _textoLivre.Clear();
    }

    private void AcumularTextoLivre(char c)
    {
        if (!DeteccaoFraseAtiva) return;
        _textoLivre.Append(c);
        if (_textoLivre.Length > FRASE_MAX_LEN)
            _textoLivre.Remove(0, _textoLivre.Length - FRASE_MAX_LEN);
    }

    /// <summary>
    /// O que NÃO vira sugestão de macro, por mais que se repita.
    ///
    /// Uma senha, um token ou uma chave de API são um bloco só, sem espaço, e são exatamente
    /// o tipo de texto que alguém digita várias vezes por dia. Sugerir "que tal criar uma
    /// macro?" para uma senha é ruim das duas pontas: a notificação denuncia que o texto foi
    /// reconhecido, e aceitar a sugestão gravaria a senha em claro no banco.
    ///
    /// O critério é a forma, não o conteúdo: frase de trabalho tem palavras separadas. Exigir
    /// três já deixa de fora todo segredo de uma palavra só, sem precisar adivinhar o que é
    /// segredo.
    /// </summary>
    private static bool PareceFraseDeTrabalho(string frase) =>
        frase.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= FRASE_MIN_PALAVRAS;

    private void FecharFraseLivre()
    {
        if (!DeteccaoFraseAtiva) return;
        var frase = _textoLivre.ToString().Trim();
        _textoLivre.Clear();
        if (frase.Length < FRASE_MIN_LEN) return;
        if (!PareceFraseDeTrabalho(frase)) return;

        _frasesVistas.TryGetValue(frase, out var qtd);
        qtd++;
        _frasesVistas[frase] = qtd;

        if (qtd == FRASE_LIMIAR)
        {
            FraseRepetidaDetectada?.Invoke(this, frase);
            _frasesVistas.Remove(frase);
        }

        // Evita crescimento ilimitado do dicionário em sessões longas
        if (_frasesVistas.Count > 200) _frasesVistas.Clear();
    }

    private static bool IsModifierOrControl(int vk) =>
        vk is 0x10 or 0x11 or 0x12   // Shift, Ctrl, Alt
           or 0x5B or 0x5C            // Win keys
           or 0x14 or 0x90 or 0x91   // CapsLock, NumLock, ScrollLock
           or >= 0x21 and <= 0x2E     // Page Up/Down, End, Home, arrows, Insert, Delete
           or >= 0x70 and <= 0x87;    // F1-F24

    /// <summary>
    /// Converte virtual key para char usando o layout de teclado ATUAL do sistema
    /// (suporta ABNT2, PT-BR, US, etc.)
    /// </summary>
    private static char VkToChar(int vkCode)
    {
        // GetAsyncKeyState lê o estado físico real (hardware), independente do thread.
        // GetKeyboardState no thread do hook NÃO reflete Win key pressionada — por isso usamos GetAsyncKeyState aqui.
        if ((NativeMethod.GetAsyncKeyState(0x11) & 0x8000) != 0 || // Ctrl
            (NativeMethod.GetAsyncKeyState(0x12) & 0x8000) != 0 || // Alt
            (NativeMethod.GetAsyncKeyState(0x5B) & 0x8000) != 0 || // Win esq
            (NativeMethod.GetAsyncKeyState(0x5C) & 0x8000) != 0)   // Win dir
            return '\0';

        var state = new byte[256];
        NativeMethod.GetKeyboardState(state);

        var hwndFocus = NativeMethod.GetForegroundWindow();
        var threadId  = NativeMethod.GetWindowThreadProcessId(hwndFocus, out _);
        var hkl       = NativeMethod.GetKeyboardLayout(threadId);

        var scanCode = NativeMethod.MapVirtualKeyEx((uint)vkCode, 0, hkl);

        var buf = new char[4];
        // wFlags = 4: modo peek — lê o char sem modificar o estado interno do teclado.
        // Sem isso, ToUnicodeEx consome dead keys e interfere com atalhos do sistema (Win+V etc.)
        var result = NativeMethod.ToUnicodeEx(
            (uint)vkCode, scanCode, state, buf, buf.Length, 4, hkl);

        if (result is 1 or 2) return buf[0];
        return '\0';
    }

    public void Dispose() { Parar(); GC.SuppressFinalize(this); }
}

internal static class NativeMethod
{
    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc fn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] public static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll")] public static extern bool GetKeyboardState(byte[] lpKeyState);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(uint idThread);
    [DllImport("user32.dll")] public static extern uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl);
    [DllImport("user32.dll")] public static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState, char[] pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);
}
