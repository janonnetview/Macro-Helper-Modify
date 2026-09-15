using MacroHelper.Core.Texto;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
// WinForms traz os mesmos nomes do WPF; aqui e sempre WPF.
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using CheckBox = System.Windows.Controls.CheckBox;
using Cursors = System.Windows.Input.Cursors;
using FontFamily = System.Windows.Media.FontFamily;
using Panel = System.Windows.Controls.Panel;

namespace MacroHelper.UI.Helpers;

/// <summary>
/// Desenha o Markdown de uma nota dentro de um painel.
///
/// É uma propriedade anexada pela mesma razão do <see cref="PreviewFormatador"/>: um
/// DataTemplate não sabe montar isto. Título, item de lista e caixa de marcar são elementos
/// diferentes, com margens diferentes, e nenhum Binding produz elementos.
///
/// O desenho sai como painel de controles, e não como FlowDocument, porque a caixa de marcar
/// precisa ser clicável de verdade e o resto precisa obedecer ao sistema de tokens do app. Um
/// FlowDocumentScrollViewer traria a tipografia e a barra de zoom dele junto.
/// </summary>
public static class MarkdownVisual
{
    /// <summary>O texto da nota. Trocar isto redesenha o painel inteiro.</summary>
    public static readonly DependencyProperty ConteudoProperty =
        DependencyProperty.RegisterAttached(
            "Conteudo", typeof(string), typeof(MarkdownVisual),
            new PropertyMetadata(null, AoTrocarOConteudo));

    public static void SetConteudo(DependencyObject alvo, string? valor) => alvo.SetValue(ConteudoProperty, valor);
    public static string? GetConteudo(DependencyObject alvo) => (string?)alvo.GetValue(ConteudoProperty);

    /// <summary>
    /// O comando que recebe o NÚMERO DA LINHA quando alguém clica numa caixa de marcar.
    ///
    /// Quem reescreve o texto é o ViewModel, e não este desenho: a marca faz parte do conteúdo
    /// da nota, então alterá-la é editar a nota — não é estado de tela.
    /// </summary>
    public static readonly DependencyProperty AoMarcarProperty =
        DependencyProperty.RegisterAttached(
            "AoMarcar", typeof(ICommand), typeof(MarkdownVisual),
            new PropertyMetadata(null, AoTrocarOConteudo));

    public static void SetAoMarcar(DependencyObject alvo, ICommand? valor) => alvo.SetValue(AoMarcarProperty, valor);
    public static ICommand? GetAoMarcar(DependencyObject alvo) => (ICommand?)alvo.GetValue(AoMarcarProperty);

    private static void AoTrocarOConteudo(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Panel painel) return;

        painel.Children.Clear();

        var conteudo = GetConteudo(painel);
        if (string.IsNullOrWhiteSpace(conteudo))
        {
            painel.Children.Add(Vazio(painel));
            return;
        }

        var comando = GetAoMarcar(painel);
        foreach (var bloco in Markdown.Analisar(conteudo))
            painel.Children.Add(Desenhar(painel, bloco, comando));
    }

    private static UIElement Vazio(FrameworkElement dono) =>
        new TextBlock
        {
            Text       = "Nada escrito ainda. Volte para Escrever e comece a nota.",
            FontSize   = 12,
            Foreground = Pincel(dono, "TextMutedBrush"),
            Margin     = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };

    private static UIElement Desenhar(FrameworkElement dono, BlocoMarkdown bloco, ICommand? aoMarcar) =>
        bloco.Tipo switch
        {
            TipoDeBloco.Titulo1 => Titulo(dono, bloco, 19, 16),
            TipoDeBloco.Titulo2 => Titulo(dono, bloco, 16, 14),
            TipoDeBloco.Titulo3 => Titulo(dono, bloco, 13.5, 12),
            TipoDeBloco.Divisor => Divisor(dono),
            TipoDeBloco.Codigo  => Codigo(dono, bloco),
            TipoDeBloco.Citacao => Citacao(dono, bloco),
            TipoDeBloco.Tarefa  => Tarefa(dono, bloco, aoMarcar),
            TipoDeBloco.Item    => ComMarcador(dono, bloco, "•"),
            TipoDeBloco.ItemNumerado => ComMarcador(dono, bloco, bloco.Marcador + "."),
            _ => Paragrafo(dono, bloco),
        };

    private static UIElement Titulo(FrameworkElement dono, BlocoMarkdown bloco, double corpo, double espacoAcima)
    {
        var texto = Linha(dono, bloco);
        texto.FontSize   = corpo;
        texto.FontWeight = FontWeights.Bold;
        texto.Foreground = Pincel(dono, "TextPrimaryBrush");
        texto.Margin     = new Thickness(0, espacoAcima, 0, 4);
        return texto;
    }

    private static UIElement Paragrafo(FrameworkElement dono, BlocoMarkdown bloco)
    {
        // Linha em branco no meio do texto vira espaço, e não um TextBlock vazio de altura
        // zero: no editor ela separa dois assuntos, e a prévia tem de separar também.
        if (string.IsNullOrWhiteSpace(bloco.Texto))
            return new Border { Height = 8 };

        var texto = Linha(dono, bloco);
        texto.Margin = new Thickness(0, 0, 0, 3);
        return texto;
    }

    private static UIElement Divisor(FrameworkElement dono) =>
        new Border
        {
            Height     = 1,
            Background = Pincel(dono, "BorderSubtleBrush"),
            Margin     = new Thickness(0, 12, 0, 12),
        };

    private static UIElement Codigo(FrameworkElement dono, BlocoMarkdown bloco) =>
        new Border
        {
            Background      = Pincel(dono, "SurfaceInsetBrush"),
            BorderBrush     = Pincel(dono, "BorderSubtleBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius    = Raio(dono, "RadiusSm"),
            Padding         = new Thickness(12, 9, 12, 9),
            Margin          = new Thickness(0, 6, 0, 6),
            Child = new TextBlock
            {
                Text         = bloco.Texto,
                FontFamily   = new FontFamily("Consolas"),
                FontSize     = 12,
                Foreground   = Pincel(dono, "TextSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap,
            },
        };

    private static UIElement Citacao(FrameworkElement dono, BlocoMarkdown bloco)
    {
        var texto = Linha(dono, bloco);
        texto.Foreground = Pincel(dono, "TextSecondaryBrush");
        texto.FontStyle  = FontStyles.Italic;

        return new Border
        {
            BorderBrush     = Pincel(dono, "AccentBrush"),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding         = new Thickness(11, 2, 0, 2),
            Margin          = new Thickness(0, 4, 0, 4),
            Child           = texto,
        };
    }

    private static UIElement ComMarcador(FrameworkElement dono, BlocoMarkdown bloco, string marcador)
    {
        var grade = NovaLinhaDeItem(bloco);

        var ponto = new TextBlock
        {
            Text                = marcador,
            FontSize            = 12.5,
            Foreground          = Pincel(dono, "TextMutedBrush"),
            TextAlignment       = TextAlignment.Right,
            Margin              = new Thickness(0, 0, 8, 0),
            VerticalAlignment   = VerticalAlignment.Top,
        };
        Grid.SetColumn(ponto, 0);

        var texto = Linha(dono, bloco);
        Grid.SetColumn(texto, 1);

        grade.Children.Add(ponto);
        grade.Children.Add(texto);
        return grade;
    }

    private static UIElement Tarefa(FrameworkElement dono, BlocoMarkdown bloco, ICommand? aoMarcar)
    {
        var grade = NovaLinhaDeItem(bloco);

        var caixa = new CheckBox
        {
            IsChecked         = bloco.Marcada,
            VerticalAlignment = VerticalAlignment.Top,
            Margin            = new Thickness(0, 1, 8, 0),
            Cursor            = Cursors.Hand,
            IsEnabled         = aoMarcar != null,
        };
        Grid.SetColumn(caixa, 0);

        // Click, e não Checked/Unchecked: o ViewModel reescreve o texto e o painel é redesenhado
        // inteiro logo em seguida. Com os dois eventos, a caixa nova nasceria disparando outro
        // e a marca piscaria de volta.
        if (aoMarcar != null)
            caixa.Click += (_, _) =>
            {
                if (aoMarcar.CanExecute(bloco.Linha)) aoMarcar.Execute(bloco.Linha);
            };

        var texto = Linha(dono, bloco);
        if (bloco.Marcada)
        {
            texto.Foreground      = Pincel(dono, "TextMutedBrush");
            texto.TextDecorations = TextDecorations.Strikethrough;
        }
        Grid.SetColumn(texto, 1);

        grade.Children.Add(caixa);
        grade.Children.Add(texto);
        return grade;
    }

    /// <summary>
    /// A linha de um item: uma grade de duas colunas, com o marcador na primeira.
    ///
    /// Grade, e não StackPanel horizontal: assim o texto que quebra em três linhas continua
    /// alinhado com ele mesmo, em vez de voltar para baixo da bolinha.
    /// </summary>
    private static Grid NovaLinhaDeItem(BlocoMarkdown bloco)
    {
        const double RecuoPorNivel = 22;

        var grade = new Grid { Margin = new Thickness(bloco.Nivel * RecuoPorNivel, 0, 0, 3) };
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        return grade;
    }

    private static TextBlock Linha(FrameworkElement dono, BlocoMarkdown bloco)
    {
        var texto = new TextBlock
        {
            FontSize     = 12.5,
            Foreground   = Pincel(dono, "TextPrimaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight   = 19,
        };

        foreach (var trecho in bloco.Trechos)
            texto.Inlines.Add(Montar(dono, trecho));

        return texto;
    }

    private static Inline Montar(FrameworkElement dono, TrechoMarkdown trecho)
    {
        if (trecho.Link is { } endereco)
        {
            var link = new Hyperlink(new Run(trecho.Texto))
            {
                Foreground = Pincel(dono, "AccentTextBrush"),
                ToolTip    = endereco,
            };
            link.Click += (_, _) => Abrir(endereco);
            return link;
        }

        var corrida = new Run(trecho.Texto);

        if (trecho.Negrito) corrida.FontWeight = FontWeights.Bold;
        if (trecho.Italico) corrida.FontStyle  = FontStyles.Italic;
        if (trecho.Riscado) corrida.TextDecorations = TextDecorations.Strikethrough;

        if (trecho.Codigo)
        {
            corrida.FontFamily = new FontFamily("Consolas");
            corrida.Foreground = Pincel(dono, "AccentTextBrush");
        }

        return corrida;
    }

    /// <summary>
    /// Abre o endereço no navegador padrão. <c>UseShellExecute</c> é obrigatório: sem ele o
    /// .NET tenta EXECUTAR a URL como programa e o clique morre numa exceção.
    /// </summary>
    private static void Abrir(string endereco)
    {
        try
        {
            if (!Uri.TryCreate(endereco, UriKind.Absolute, out var uri)) return;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps &&
                uri.Scheme != Uri.UriSchemeMailto) return;

            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) { App.LogErro(ex); }
    }

    /// <summary>
    /// O pincel vem da árvore, e não de um dicionário fixo: assim a prévia acompanha o tema e
    /// a cor de destaque escolhida sem que este arquivo conheça nenhuma das duas.
    /// </summary>
    private static Brush Pincel(FrameworkElement dono, string chave) =>
        dono.TryFindResource(chave) as Brush ?? Brushes.Gray;

    private static CornerRadius Raio(FrameworkElement dono, string chave) =>
        dono.TryFindResource(chave) is CornerRadius raio ? raio : new CornerRadius(10);
}
