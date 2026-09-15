using MacroHelper.Core.Entities;
using MacroHelper.Services;
using MacroHelper.UI.Helpers;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace MacroHelper.UI.Views;

public partial class MacroPopupWindow : Window
{
    private readonly InsercaoDeMacroService _insercao;
    private string _gatilhoAtual  = string.Empty;
    private IntPtr _janelaOrigem  = IntPtr.Zero;

    public MacroPopupWindow(InsercaoDeMacroService insercao)
    {
        InitializeComponent();

        // Sem sombra nem fio do sistema: aqui o limite da janela é a borda do cartão.
        MolduraNativa.Aplicar(this, comSombra: false);

        _insercao = insercao;
    }

    public void AtualizarSugestoes(string gatilho, IEnumerable<Macro> macros)
    {
        _gatilhoAtual = gatilho;
        // Salva a janela ativa ANTES do popup aparecer
        if (!IsVisible) _janelaOrigem = GetForegroundWindow();
        TxtGatilho.Text = gatilho;
        ListMacros.ItemsSource = macros.ToList();
        if (ListMacros.Items.Count > 0) ListMacros.SelectedIndex = 0;
        PositionarNoCursor();
        if (!IsVisible) Show();
    }

    public void Fechar() => Hide();

    private void PositionarNoCursor()
    {
        GetCursorPos(out var pt);
        double left = pt.X + 4;
        double top  = pt.Y + 24;
        if (left + 380 > SystemParameters.VirtualScreenWidth)  left = pt.X - 384;
        if (top  + 320 > SystemParameters.VirtualScreenHeight) top  = pt.Y - 320;
        Left = left; Top = top;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void ListMacros_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Return or Key.Enter) { InserirSelecionada(); e.Handled = true; }
        else if (e.Key == Key.Escape)         { Fechar(); e.Handled = true; }
    }

    private void ListMacros_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ListMacros.SelectedItem is Macro) InserirSelecionada();
    }

    private async void InserirSelecionada()
    {
        if (ListMacros.SelectedItem is not Macro macro) return;
        Hide();

        try
        {
            await _insercao.InserirAsync(macro, _gatilhoAtual, antesDeInserir: () =>
            {
                if (_janelaOrigem != IntPtr.Zero) SetForegroundWindow(_janelaOrigem);
            });
        }
        catch (Exception ex) { App.LogErro(ex); }
    }

    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }
}
