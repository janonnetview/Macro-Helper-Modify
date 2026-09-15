using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MacroHelper.UI.Helpers;

/// <summary>
/// A moldura que o Windows desenha FORA da janela e que não passa pelo WPF: a sombra da área
/// não-cliente, o fio de 1px da borda e o canto arredondado do sistema.
///
/// Os três acompanham o RETÂNGULO da janela, e não a curva que o Border desenha por dentro —
/// então numa janela flutuante sobrava um borrão cinza de cantos quadrados em volta do cartão,
/// bem visível no tema claro. Nas flutuantes a moldura nativa é desligada e quem separa a
/// janela do que está atrás passa a ser a borda do próprio cartão (FloatBorderBrush, nos temas).
/// A janela principal é a única que ainda pede a sombra do sistema.
/// </summary>
internal static class MolduraNativa
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int atributo, ref int valor, int tamanho);

    private const int CantoPreferido = 33; // DWMWA_WINDOW_CORNER_PREFERENCE
    private const int CorDaBorda     = 34; // DWMWA_BORDER_COLOR
    private const int RenderizacaoNC = 2;  // DWMWA_NCRENDERING_POLICY

    private const int NaoArredondar   = 1;                          // DWMWCP_DONOTROUND
    private const int SemCor          = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
    private const int SombraLigada    = 2;                          // DWMNCRP_ENABLED
    private const int SombraDesligada = 1;                          // DWMNCRP_DISABLED

    /// <summary>
    /// Ajusta a moldura nativa da janela. Pode ser chamada no construtor: sem handle ainda, o
    /// pedido fica pendurado no <c>SourceInitialized</c> e se repete quando o handle existir —
    /// que é também o motivo de nada acontecer numa janela que nunca chega a ser mostrada.
    /// </summary>
    public static void Aplicar(Window janela, bool comSombra)
    {
        var hwnd = new WindowInteropHelper(janela).Handle;
        if (hwnd == IntPtr.Zero)
        {
            janela.SourceInitialized += AoGanharHandle;
            return;
        }

        try
        {
            // A janela desenha o próprio canto arredondado. O do DWM tem outro raio e só somaria
            // uma segunda curva por cima da nossa.
            int canto = NaoArredondar;
            DwmSetWindowAttribute(hwnd, CantoPreferido, ref canto, 4);

            // O fio de 1px que o Windows 11 põe em volta de toda janela — retangular, ele corta
            // os cantos arredondados.
            int semBorda = SemCor;
            DwmSetWindowAttribute(hwnd, CorDaBorda, ref semBorda, 4);

            int sombra = comSombra ? SombraLigada : SombraDesligada;
            DwmSetWindowAttribute(hwnd, RenderizacaoNC, ref sombra, 4);
        }
        catch (Exception ex) { App.LogErro(ex); }

        void AoGanharHandle(object? remetente, EventArgs e)
        {
            janela.SourceInitialized -= AoGanharHandle;
            Aplicar(janela, comSombra);
        }
    }
}
