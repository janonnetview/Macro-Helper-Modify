using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace MacroHelper.Services;

public class TextInsertionService
{
    [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint uFormat);
    [DllImport("user32.dll")] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr hMem);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr hMem);
    [DllImport("user32.dll")] private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr h, StringBuilder t, int c);

    private const byte   VK_BACK           = 0x08;
    private const byte   VK_CONTROL        = 0x11;
    private const byte   VK_V              = 0x56;
    private const ushort VK_RETURN         = 0x0D;
    private const ushort VK_LEFT           = 0x25;
    private const ushort VK_RIGHT          = 0x27;
    private const uint   KEYEVENTF_KEYUP   = 0x0002;
    private const uint   KEYEVENTF_UNICODE = 0x0004;
    private const uint   CF_UNICODETEXT    = 13;
    private const uint   GMEM_MOVEABLE     = 0x0002;
    private const uint   INPUT_KEYBOARD    = 1;

    // Estado para desfazer a última inserção (Ctrl+Shift+Z)
    private string? _ultimoConteudoInserido;
    private string? _ultimoGatilhoRemovido;

    // Quantas setas o cursor andou para trás por causa de um {|}. O desfazer precisa saber:
    // os backspaces apagam para a ESQUERDA do cursor, e com ele parado no meio do texto eles
    // comeriam a metade errada. Ver DesfazerUltimaInsercaoAsync.
    private int     _ultimoDeslocamentoCursor;

    public bool TemUndoDisponivel => _ultimoConteudoInserido != null;

    // Última macro inserida (independente do undo) — usada para repetir com Ctrl+Alt+0
    private string? _ultimoConteudoParaRepetir;
    public bool TemUltimaParaRepetir => _ultimoConteudoParaRepetir != null;

    // Identifica a inserção em curso. A restauração do clipboard só acontece se nenhuma outra
    // inserção tiver começado no meio-tempo — ver RestaurarClipboardAsync.
    private int _sequenciaInsercao;

    // Apps (substring do título da janela) que devem usar digitação em vez de colar
    public List<string> AppsModoDigitacao { get; set; } = new();

    /// <summary>
    /// Avisa que ESTE app acabou de escrever na área de transferência — tanto o texto da macro
    /// quanto a devolução do que estava lá antes.
    ///
    /// Existe para o histórico da área de transferência não gravar as próprias pegadas: sem
    /// isto, inserir uma macro apareceria no histórico como se a pessoa a tivesse copiado, e o
    /// item de verdade que ela tinha copiado seria empurrado para baixo por uma cópia que
    /// nunca houve.
    /// </summary>
    public event Action<string>? EscreveuNoClipboard;

    /// <param name="interpretarMarcador">
    /// Se <c>{|}</c> no conteúdo posiciona o cursor ou vale como texto.
    ///
    /// Verdadeiro para macro, que é o conteúdo escrito PARA ser inserido. Falso para nota e
    /// para item da área de transferência, que entram no outro programa exatamente como estão
    /// — é a mesma regra que faz um <c>{campo}</c> dentro de uma nota ser chave e colchete, e
    /// não variável a preencher. Sem esta separação, colar um trecho copiado que por acaso
    /// contivesse <c>{cursor}</c> comeria a palavra e moveria o cursor.
    /// </param>
    public async Task InserirTextoAsync(string textoParaRemover, string conteudo,
        bool forcarDigitacao = false, bool interpretarMarcador = true)
    {
        // O {|} sai do texto ANTES de qualquer coisa: o que vai para o app de destino é o
        // conteúdo limpo, e o marcador vira só um número de setas a mandar no fim.
        var (texto, deslocamentoCursor) = interpretarMarcador
            ? MarcadorDeCursor.Extrair(conteudo)
            : (conteudo, 0);

        conteudo = texto;

        var sequencia = Interlocked.Increment(ref _sequenciaInsercao);

        await Task.Delay(60);

        // Um por lote com 12ms entre eles: o gatilho tem poucos caracteres e este apagamento
        // acontece no campo do outro app logo depois do hook disparar, quando o app de destino
        // ainda está processando as teclas que o usuário acabou de digitar. Aqui a folga vale
        // mais do que a velocidade.
        await EnviarBackspacesAsync(textoParaRemover.Length, porLote: 1, msEntreLotes: 12);

        await Task.Delay(40);

        var usarDigitacao = forcarDigitacao || AppAtualPrefereDigitacao();
        string? clipboardAnterior = null;

        if (usarDigitacao)
        {
            DigitarTexto(conteudo);
        }
        else
        {
            clipboardAnterior = LerClipboardTexto();

            if (!EscreverNoClipboard(conteudo))
            {
                // Clipboard indisponível — cai para digitação direta como último recurso
                clipboardAnterior = null;
                DigitarTexto(conteudo);
            }
            else
            {
                await Task.Delay(60);
                // keybd_event aqui (e não SendInput) porque o finally garante que o Ctrl é
                // solto mesmo se algo falhar no meio — um Ctrl preso deixa o teclado do
                // usuário inutilizável até ele pressionar e soltar a tecla de novo.
                try
                {
                    keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_V,       0, 0, UIntPtr.Zero);
                    keybd_event(VK_V,       0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
                finally
                {
                    keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
            }
        }

        await PosicionarCursorAsync(deslocamentoCursor, usarDigitacao);

        _ultimoConteudoInserido    = conteudo;
        _ultimoGatilhoRemovido     = textoParaRemover;
        _ultimoDeslocamentoCursor  = deslocamentoCursor;

        // Guardado COM marcador: repetir a macro tem de posicionar o cursor de novo, e o
        // Extrair lá em cima refaz a conta a partir do texto original.
        _ultimoConteudoParaRepetir = MarcadorDeCursor.Tem(texto) ? conteudo : texto;

        if (clipboardAnterior != null)
            _ = RestaurarClipboardAsync(clipboardAnterior, sequencia);
    }

    /// <summary>
    /// Leva o cursor de volta ao ponto marcado por <c>{|}</c>, uma seta para a esquerda por
    /// caractere que ficou depois dele.
    ///
    /// A espera só existe no caminho da colagem: o Ctrl+V é assíncrono daqui: quem processa é
    /// o app de destino, e as setas chegando antes do texto moveriam um cursor que ainda está
    /// onde estava. Digitando não há corrida — o SendInput já entregou tudo em ordem.
    ///
    /// Um lote só: aqui não há o problema dos backspaces em campo web, porque seta não muda o
    /// conteúdo e nenhum evento de input é disparado por ela.
    /// </summary>
    private static async Task PosicionarCursorAsync(int deslocamento, bool foiDigitado)
    {
        if (deslocamento <= 0) return;

        if (!foiDigitado) await Task.Delay(120);

        EnviarTeclaVirtual(VK_LEFT, deslocamento);
    }

    /// <summary>
    /// Devolve ao usuário o que ele tinha copiado antes da macro passar por cima.
    ///
    /// Sem isto, inserir uma macro apaga a área de transferência — você copia um número de
    /// chamado, insere um texto pronto, e o número sumiu. Só texto é preservado: uma imagem
    /// copiada continua se perdendo, como já acontecia.
    ///
    /// A espera existe porque o Ctrl+V é assíncrono do ponto de vista daqui: quem lê o
    /// clipboard é o app de destino, e restaurar cedo demais faria ele colar o texto errado.
    /// Meio segundo é folgado para um Ctrl+V comum.
    /// </summary>
    private async Task RestaurarClipboardAsync(string conteudoAnterior, int sequencia)
    {
        await Task.Delay(500);

        // Outra macro começou a ser inserida nesse meio tempo — ela é a dona do clipboard
        // agora, e restaurar aqui atropelaria a colagem dela.
        if (Volatile.Read(ref _sequenciaInsercao) != sequencia) return;

        // Falhar aqui é invisível e inofensivo: o usuário fica com o texto da macro no
        // clipboard, exatamente como era antes desta funcionalidade existir.
        try { EscreverNoClipboard(conteudoAnterior); }
        catch { }
    }

    /// <summary>Reinsere o último conteúdo de macro inserido (Ctrl+Alt+0), sem remover nenhum gatilho.</summary>
    public async Task RepetirUltimaInsercaoAsync()
    {
        if (_ultimoConteudoParaRepetir == null) return;
        await InserirTextoAsync(string.Empty, _ultimoConteudoParaRepetir);
    }

    /// <summary>Desfaz a última macro inserida: remove o texto colado e devolve o gatilho original digitado.</summary>
    public async Task DesfazerUltimaInsercaoAsync()
    {
        if (_ultimoConteudoInserido == null) return;

        var conteudo     = _ultimoConteudoInserido;
        var gatilho      = _ultimoGatilhoRemovido ?? string.Empty;
        var deslocamento = _ultimoDeslocamentoCursor;
        _ultimoConteudoInserido   = null;
        _ultimoGatilhoRemovido    = null;
        _ultimoDeslocamentoCursor = 0;

        await Task.Delay(40);

        // Com um {|} na macro o cursor ficou PARADO NO MEIO do texto inserido. Backspace apaga
        // para a esquerda, então daqui ele comeria a primeira metade e deixaria a segunda na
        // tela. Devolver o cursor ao fim primeiro, com o mesmo número de setas em sentido
        // contrário, faz o desfazer voltar a apagar exatamente o que foi inserido.
        EnviarTeclaVirtual(VK_RIGHT, deslocamento);

        // Aqui o volume é outro: desfazer uma macro de 2.000 caracteres mandava 2.000 pares de
        // teclas com 4ms entre cada um — 8 segundos de app travado apagando letra por letra.
        // Em lotes de 64 numa chamada só de SendInput, a mesma macro sai em cerca de 0,2s.
        await EnviarBackspacesAsync(conteudo.Length, porLote: 64, msEntreLotes: 5);

        if (!string.IsNullOrEmpty(gatilho))
        {
            await Task.Delay(30);
            DigitarTexto(gatilho);
        }
    }

    private bool AppAtualPrefereDigitacao()
    {
        if (AppsModoDigitacao.Count == 0) return false;
        try
        {
            var hwnd = GetForegroundWindow();
            var buf  = new StringBuilder(256);
            GetWindowText(hwnd, buf, 256);
            var titulo = buf.ToString();
            return AppsModoDigitacao.Any(app =>
                !string.IsNullOrWhiteSpace(app) &&
                titulo.Contains(app, StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    /// <summary>Digita texto caractere a caractere via SendInput (Unicode), sem depender da área de transferência.</summary>
    private static void DigitarTexto(string texto)
    {
        foreach (var ch in texto)
        {
            if (ch == '\r') continue;

            if (ch == '\n')
            {
                EnviarTeclaVirtual(VK_RETURN);
                continue;
            }

            EnviarCharUnicode(ch);
        }
    }

    private static void EnviarCharUnicode(char c)
    {
        var down = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion { ki = new KEYBDINPUT { wVk = 0, wScan = c, dwFlags = KEYEVENTF_UNICODE, time = 0, dwExtraInfo = IntPtr.Zero } }
        };
        var up = down;
        up.U.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
        SendInput(2, new[] { down, up }, Marshal.SizeOf<INPUT>());
    }

    /// <summary>Pressiona e solta uma tecla virtual, <paramref name="vezes"/> vezes numa chamada só.</summary>
    private static void EnviarTeclaVirtual(ushort vk, int vezes = 1)
    {
        if (vezes <= 0) return;

        var teclas = new INPUT[vezes * 2];
        for (var i = 0; i < vezes; i++)
        {
            teclas[i * 2] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, wScan = 0, dwFlags = 0, time = 0, dwExtraInfo = IntPtr.Zero } }
            };
            teclas[i * 2 + 1] = teclas[i * 2];
            teclas[i * 2 + 1].U.ki.dwFlags = KEYEVENTF_KEYUP;
        }

        SendInput((uint)teclas.Length, teclas, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Manda <paramref name="quantidade"/> backspaces em lotes.
    ///
    /// O SendInput aceita um array de eventos numa chamada só, então um lote inteiro custa uma
    /// transição para o kernel em vez de N. Os lotes não viram um bloco único de propósito:
    /// campos de texto na web — o destino mais comum aqui — processam entrada num laço de
    /// eventos e descartam o excedente se ele chegar todo de uma vez.
    /// </summary>
    private static async Task EnviarBackspacesAsync(int quantidade, int porLote, int msEntreLotes)
    {
        for (var enviados = 0; enviados < quantidade; enviados += porLote)
        {
            var lote   = Math.Min(porLote, quantidade - enviados);
            var teclas = new INPUT[lote * 2];

            for (var i = 0; i < lote; i++)
            {
                teclas[i * 2] = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    U = new InputUnion { ki = new KEYBDINPUT { wVk = VK_BACK, wScan = 0, dwFlags = 0, time = 0, dwExtraInfo = IntPtr.Zero } }
                };
                teclas[i * 2 + 1] = teclas[i * 2];
                teclas[i * 2 + 1].U.ki.dwFlags = KEYEVENTF_KEYUP;
            }

            SendInput((uint)teclas.Length, teclas, Marshal.SizeOf<INPUT>());

            if (enviados + lote < quantidade) await Task.Delay(msEntreLotes);
        }
    }

    /// <summary>
    /// Lê o texto da área de transferência via Win32, sem depender do STA do WPF.
    ///
    /// Pública porque o monitor do histórico lê pelo mesmo caminho. É a única leitura de
    /// clipboard do app que sabe esperar: <c>OpenClipboard</c> falha enquanto outro processo
    /// mantém a área aberta, e o app que acabou de copiar costuma ainda estar segurando.
    /// Duas implementações disso divergiriam justamente nessa espera.
    /// </summary>
    public static string? LerClipboardTexto()
    {
        if (!IsClipboardFormatAvailable(CF_UNICODETEXT)) return null;

        for (var tentativa = 0; tentativa < 5; tentativa++)
        {
            if (OpenClipboard(IntPtr.Zero)) break;
            Thread.Sleep(20);
            if (tentativa == 4) return null;
        }

        try
        {
            var handle = GetClipboardData(CF_UNICODETEXT);
            if (handle == IntPtr.Zero) return null;

            var ptr = GlobalLock(handle);
            if (ptr == IntPtr.Zero) return null;

            try   { return Marshal.PtrToStringUni(ptr); }
            finally { GlobalUnlock(handle); }
        }
        catch { return null; }
        finally { CloseClipboard(); }
    }

    /// <summary>
    /// Escreve na área de transferência anunciando a escrita.
    ///
    /// Todo caminho deste serviço que mexe no clipboard passa por aqui — o texto da macro e a
    /// devolução do conteúdo anterior. É o que permite ao histórico distinguir uma cópia da
    /// pessoa de uma escrita do próprio app.
    /// </summary>
    private bool EscreverNoClipboard(string texto)
    {
        // O aviso vem ANTES da escrita: a mensagem WM_CLIPBOARDUPDATE do Windows chega assim
        // que SetClipboardData retorna, e o monitor precisa já saber o que ignorar.
        EscreveuNoClipboard?.Invoke(texto);
        return SetClipboardWin32(texto);
    }

    private static bool SetClipboardWin32(string text)
    {
        // Retry até 5x — OpenClipboard falha se outro processo tem o clipboard aberto
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero)) break;
            Thread.Sleep(20);
            if (attempt == 4) return false;
        }

        var hGlobal = IntPtr.Zero;
        try
        {
            EmptyClipboard();

            var bytes = (text.Length + 1) * 2;
            hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
            if (hGlobal == IntPtr.Zero) return false;

            var ptr = GlobalLock(hGlobal);
            if (ptr == IntPtr.Zero)
            {
                GlobalFree(hGlobal);
                hGlobal = IntPtr.Zero;
                return false;
            }

            Marshal.Copy(text.ToCharArray(), 0, ptr, text.Length);
            Marshal.WriteInt16(ptr, text.Length * 2, 0);
            GlobalUnlock(hGlobal);

            var result = SetClipboardData(CF_UNICODETEXT, hGlobal);
            if (result != IntPtr.Zero)
            {
                // SetClipboardData com sucesso — clipboard assume ownership da memória
                hGlobal = IntPtr.Zero;
                return true;
            }
            return false;
        }
        finally
        {
            // CloseClipboard sempre chamado — libera para outros processos
            CloseClipboard();
            // Libera memória só se SetClipboardData falhou (não assumiu ownership)
            if (hGlobal != IntPtr.Zero)
                GlobalFree(hGlobal);
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint   dwFlags;
        public uint   time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }
}
