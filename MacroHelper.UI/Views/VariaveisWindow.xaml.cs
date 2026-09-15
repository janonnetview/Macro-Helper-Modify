using MacroHelper.Services;
using MacroHelper.UI.Helpers;
using System.Windows;
using System.Windows.Controls;
using ComboBox = System.Windows.Controls.ComboBox;
using Control = System.Windows.Controls.Control;
using TextBox = System.Windows.Controls.TextBox;
using Orientation = System.Windows.Controls.Orientation;

namespace MacroHelper.UI.Views;

public partial class VariaveisWindow : Window
{
    private readonly List<VariavelInfo> _variaveis;
    private readonly string             _template;

    /// <summary>
    /// O controle de cada variável, na ordem em que aparecem. Guarda a variável junto porque o
    /// foco inicial depende dela: o cursor tem de cair no primeiro campo que a pessoa
    /// realmente precisa preencher, e não num {data} que já veio pronto.
    /// </summary>
    private readonly List<(VariavelInfo Variavel, Control Controle)> _campos = new();

    public string? ConteudoFinal { get; private set; }

    public VariaveisWindow(string template, List<VariavelInfo> variaveis)
    {
        InitializeComponent();

        // Sem sombra nem fio do sistema: aqui o limite da janela é a borda do cartão.
        MolduraNativa.Aplicar(this, comSombra: false);

        _template  = template;
        _variaveis = variaveis;

        ConstruirCampos();
        AtualizarPrevia();
    }

    private void ConstruirCampos()
    {
        foreach (var v in _variaveis)
        {
            var bloco = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            bloco.Children.Add(Cabecalho(v));

            // Control e não var: os dois ramos devolvem tipos irmãos, e é o tipo do destino
            // que dá ao compilador o denominador comum entre ComboBox e TextBox.
            Control controle = v.EhLista ? Lista(v) : Texto(v);

            _campos.Add((v, controle));
            bloco.Children.Add(controle);

            PainelCampos.Children.Add(bloco);
        }

        // Foca no primeiro campo que exige decisão. Uma macro que só tem {data} e {usuario}
        // abre com o foco no primeiro mesmo — mas ali o Enter já insere, que é o certo.
        Loaded += (_, _) =>
        {
            if (_campos.Count == 0) return;

            var indice = _campos.FindIndex(c => !c.Variavel.AutoPreencher);
            var primeiro = _campos[indice < 0 ? 0 : indice].Controle;

            primeiro.Focus();
            if (primeiro is TextBox caixa) caixa.SelectAll();
        };
    }

    /// <summary>O badge com o token exato, o rótulo do que ele significa e a marca de "auto".</summary>
    private Grid Cabecalho(VariavelInfo v)
    {
        var header = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // O TOKEN inteiro, e não só o nome: com {data} e {data+2} na mesma macro, dois badges
        // escritos "{data}" deixariam os dois campos indistinguíveis.
        var badge = new Border
        {
            CornerRadius = new CornerRadius(5),
            Padding      = new Thickness(7, 2, 7, 2),
            Background   = FindResource("AccentLightBrush") as System.Windows.Media.Brush,
        };
        var badgeTxt = new TextBlock
        {
            Text       = v.Token,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize   = 11,
            FontWeight = FontWeights.Bold,
        };
        badgeTxt.SetResourceReference(ForegroundProperty, "AccentTextBrush");
        badge.Child = badgeTxt;

        var labelPanel = new StackPanel { Orientation = Orientation.Horizontal };
        labelPanel.Children.Add(badge);

        var labelTxt = new TextBlock
        {
            Text              = $"  {v.Rotulo}",
            FontSize          = 12,
            FontWeight        = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        labelTxt.SetResourceReference(ForegroundProperty, "TextSecondaryBrush");
        labelPanel.Children.Add(labelTxt);

        Grid.SetColumn(labelPanel, 0);
        header.Children.Add(labelPanel);

        if (v.AutoPreencher)
        {
            var autoBadge = new Border
            {
                CornerRadius      = new CornerRadius(5),
                Padding           = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center,
            };
            autoBadge.SetResourceReference(Border.BackgroundProperty, "SuccessBgBrush");

            var autoTxt = new TextBlock { Text = "auto", FontSize = 10, FontWeight = FontWeights.SemiBold };
            autoTxt.SetResourceReference(ForegroundProperty, "SuccessBrush");
            autoBadge.Child = autoTxt;

            Grid.SetColumn(autoBadge, 1);
            header.Children.Add(autoBadge);
        }

        return header;
    }

    private TextBox Texto(VariavelInfo v)
    {
        var input = new TextBox
        {
            Tag    = v.Token,
            Text   = v.ValorPadrao ?? string.Empty,
            Height = 40,
        };
        input.SetResourceReference(StyleProperty, "InputStyle");
        input.TextChanged += (_, _) =>
        {
            v.ValorPadrao = input.Text;
            AtualizarPrevia();
        };
        return input;
    }

    /// <summary>
    /// O campo de <c>{status:Aberto|Em análise|Concluído}</c>.
    ///
    /// Uma lista e não uma caixa de texto porque o ganho está justamente aí: os valores
    /// possíveis são conhecidos, e digitá-los à mão é onde entram "Em analise" sem acento e
    /// "concluido" em minúscula — num texto que vai para o cliente.
    /// </summary>
    private ComboBox Lista(VariavelInfo v)
    {
        var combo = new ComboBox
        {
            Tag           = v.Token,
            ItemsSource   = v.Opcoes,
            SelectedIndex = 0,
            Height        = 40,
        };

        combo.SelectionChanged += (_, _) =>
        {
            v.ValorPadrao = combo.SelectedItem as string ?? string.Empty;
            AtualizarPrevia();
        };

        return combo;
    }

    private void AtualizarPrevia()
    {
        var preview = VariavelService.Substituir(_template, ValoresAtuais());
        TxtPrevia.Text = preview.Length > 150 ? preview[..150] + "…" : preview;
    }

    /// <summary>
    /// Um lugar só monta o dicionário — a prévia e o texto que sai daqui não podem divergir.
    /// A chave é o token inteiro, como espera <see cref="VariavelService.Substituir"/>.
    /// </summary>
    private Dictionary<string, string> ValoresAtuais() =>
        _variaveis.ToDictionary(v => v.Token, v => v.ValorPadrao ?? string.Empty);

    private void BtnInserir_Click(object sender, RoutedEventArgs e)
    {
        ConteudoFinal = VariavelService.Substituir(_template, ValoresAtuais());
        DialogResult  = true;
        Close();
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
