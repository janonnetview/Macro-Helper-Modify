using MacroHelper.UI.Helpers;
using System.Windows;
using System.Windows.Controls;
// WinForms tem UserControl e SelectionChangedEventArgs com os mesmos nomes; aqui é sempre WPF.
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace MacroHelper.UI.Controls;

/// <summary>
/// Campo de data do app: o texto com máscara mais um calendário no botão ao lado.
///
/// É um controle só, e não XAML repetido em cada tela, porque "campo de data" precisa ser a
/// mesma coisa em todo lugar — o dia que o calendário abre marcado é lido pelo mesmo
/// <see cref="DataDigitada"/> que valida o que foi digitado à mão.
/// </summary>
public partial class SeletorDeData : UserControl
{
    /// <summary>
    /// O valor vai e volta como texto, e não como DateTime?, de propósito: o campo aceita data
    /// pela metade enquanto se digita, e é o formulário que decide o que fazer com "24/08".
    /// </summary>
    public static readonly DependencyProperty TextoProperty =
        DependencyProperty.Register(
            nameof(Texto), typeof(string), typeof(SeletorDeData),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string Texto
    {
        get => (string)GetValue(TextoProperty);
        set => SetValue(TextoProperty, value);
    }

    /// <summary>Marcar o dia atual no calendário dispara a mudança de seleção; não é escolha de ninguém.</summary>
    private bool _abrindo;

    public SeletorDeData() => InitializeComponent();

    private void AbrirCalendario(object sender, RoutedEventArgs e)
    {
        _abrindo = true;
        try
        {
            var data = DataDigitada.Interpretar(Texto, DateTime.Today);
            Calendario.SelectedDate = data;
            Calendario.DisplayDate  = data ?? DateTime.Today;
        }
        finally { _abrindo = false; }

        PopupCalendario.IsOpen = true;
    }

    private void AoEscolherData(object sender, SelectionChangedEventArgs e)
    {
        if (_abrindo || Calendario.SelectedDate is not DateTime data) return;

        Texto = data.ToString("dd/MM/yyyy");
        PopupCalendario.IsOpen = false;

        // Devolve o cursor para o campo: quem escolheu o dia costuma ir direto para a hora.
        Campo.Focus();
        Campo.CaretIndex = Campo.Text.Length;
    }
}
