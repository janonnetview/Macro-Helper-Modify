using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Brush = System.Windows.Media.Brush;

namespace MacroHelper.UI.Helpers;

/// <summary>
/// Uma linha da prévia: o número da gaveta à esquerda e o texto que vai nela.
///
/// A prévia é uma LISTA de linhas, e não um bloco único de texto com quebras, porque só assim
/// a numeração fica alinhada quando uma linha longa quebra em duas: o número pertence à linha
/// inteira, não à primeira metade dela.
/// </summary>
public sealed class LinhaDePreview
{
    public LinhaDePreview(int numero, string texto, bool formatar)
    {
        Numero   = numero;
        Texto    = texto;
        Formatar = formatar;
    }

    public int    Numero   { get; }
    public string Texto    { get; }

    /// <summary>Macro passa por **negrito** e {{placeholder}}; texto solto (uma nota, uma
    /// observação) vai como está — asterisco ali é asterisco mesmo.</summary>
    public bool   Formatar { get; }
}

/// <summary>Renderiza um preview formatado leve: **negrito**, *itálico* e {{placeholders}} destacados.</summary>
public static class PreviewFormatador
{
    private static readonly Regex _tokenRegex = new(@"(\*\*[^*]+\*\*|\*[^*]+\*|\{\{[^}]*\}\})", RegexOptions.Compiled);

    /// <summary>Quebra o conteúdo em linhas numeradas a partir de 1.</summary>
    public static IReadOnlyList<LinhaDePreview> EmLinhas(string conteudo, bool formatar = true)
    {
        var linhas = conteudo.Replace("\r\n", "\n").Split('\n');
        var lista  = new List<LinhaDePreview>(linhas.Length);

        for (int i = 0; i < linhas.Length; i++)
            lista.Add(new LinhaDePreview(i + 1, linhas[i], formatar));

        return lista;
    }

    public static void Renderizar(TextBlock destino, string conteudo, Brush corDestaque)
    {
        destino.Inlines.Clear();
        var linhas = conteudo.Replace("\r\n", "\n").Split('\n');

        for (int i = 0; i < linhas.Length; i++)
        {
            RenderizarLinha(destino, linhas[i], corDestaque);
            if (i < linhas.Length - 1) destino.Inlines.Add(new LineBreak());
        }
    }

    /// <summary>
    /// Propriedade anexada que põe UMA <see cref="LinhaDePreview"/> dentro de um TextBlock já
    /// formatada.
    ///
    /// Existe porque um DataTemplate não sabe montar Inlines: negrito, itálico e placeholder
    /// são objetos, não texto, e nenhum Binding os produz. Com ela o XAML continua declarando
    /// o desenho da linha e o C# continua sendo o único lugar que entende a marcação.
    /// </summary>
    public static readonly DependencyProperty LinhaProperty =
        DependencyProperty.RegisterAttached(
            "Linha", typeof(LinhaDePreview), typeof(PreviewFormatador),
            new PropertyMetadata(null, AoTrocarALinha));

    public static void SetLinha(DependencyObject alvo, LinhaDePreview? valor) => alvo.SetValue(LinhaProperty, valor);
    public static LinhaDePreview? GetLinha(DependencyObject alvo) => (LinhaDePreview?)alvo.GetValue(LinhaProperty);

    private static void AoTrocarALinha(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock destino) return;

        destino.Inlines.Clear();
        if (e.NewValue is not LinhaDePreview linha) return;

        if (!linha.Formatar)
        {
            destino.Text = linha.Texto;
            return;
        }

        // A cor do destaque sai do dicionário de recursos pela própria árvore: assim a linha
        // acompanha o tema (e a cor de destaque escolhida nas Configurações) sem que quem
        // chama precise buscar a escova.
        var corDestaque = destino.TryFindResource("AccentTextBrush") as Brush
                          ?? System.Windows.Media.Brushes.YellowGreen;

        RenderizarLinha(destino, linha.Texto, corDestaque);
    }

    private static void RenderizarLinha(TextBlock destino, string linha, Brush corDestaque)
    {
        var pos = 0;
        foreach (Match m in _tokenRegex.Matches(linha))
        {
            if (m.Index > pos)
                destino.Inlines.Add(new Run(linha[pos..m.Index]));

            var token = m.Value;
            if (token.StartsWith("**"))
                destino.Inlines.Add(new Bold(new Run(token[2..^2])));
            else if (token.StartsWith("{{"))
                destino.Inlines.Add(new Run(token) { Foreground = corDestaque, FontWeight = System.Windows.FontWeights.SemiBold });
            else
                destino.Inlines.Add(new Italic(new Run(token[1..^1])));

            pos = m.Index + m.Length;
        }

        if (pos < linha.Length)
            destino.Inlines.Add(new Run(linha[pos..]));
    }
}
