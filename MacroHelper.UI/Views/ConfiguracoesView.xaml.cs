using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace MacroHelper.UI.Views;

public partial class ConfiguracoesView : UserControl
{
    public ConfiguracoesView()
    {
        InitializeComponent();
    }

    private void ConfiguracoesView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ViewModels.ConfiguracoesViewModel vm || vm.CapturandoAtalho == null) return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return; // espera uma tecla "real", não só o modificador

        e.Handled = true;
        if (key == Key.Escape) { vm.CancelarCapturaAtalho(); return; }

        var vk = KeyInterop.VirtualKeyFromKey(key);
        vm.FinalizarCapturaAtalho(vk, Keyboard.Modifiers);
    }
}
