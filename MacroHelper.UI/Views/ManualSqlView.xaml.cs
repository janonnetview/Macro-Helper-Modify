using System.IO;
using System.Windows;

namespace MacroHelper.UI.Views;

public partial class ManualSqlView : System.Windows.Controls.UserControl
{
    public ManualSqlView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "manual_sql_max.html");

        if (File.Exists(htmlPath))
        {
            WebView.Source = new Uri(htmlPath);
            return;
        }

        // Rodando do código-fonte o arquivo sempre está lá (CopyToOutputDirectory=Always). Já
        // no app instalado ele dependia de o setup.iss copiá-lo — e por várias versões o setup
        // enumerava só .exe/.dll/.json, então esta tela abria em branco, sem erro nenhum.
        WebView.Visibility = Visibility.Collapsed;
        AvisoArquivoAusente.Visibility = Visibility.Visible;
        AvisoCaminho.Text =
            $"Esperado em {htmlPath}\n\n" +
            "Se você instalou pelo setup, reinstale com um pacote gerado depois desta versão.";
    }
}
