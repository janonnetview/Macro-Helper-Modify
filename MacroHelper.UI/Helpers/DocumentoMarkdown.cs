using MacroHelper.Core.Texto;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
// WinForms traz os mesmos nomes do WPF; aqui é sempre WPF.
using FontFamily = System.Windows.Media.FontFamily;

namespace MacroHelper.UI.Helpers;

/// <summary>
/// A ponte entre o Markdown gravado e o documento que se edita na tela.
///
/// A nota continua sendo TEXTO no banco: é o que a busca lê, o que o resumo do cartão recorta
/// e o que vai para o outro programa quando se manda inserir. O que mudou é que ninguém mais
/// digita a marcação à mão — ela é montada aqui ao abrir e lida de volta daqui ao gravar.
///
/// Por que ler de volta em vez de guardar o documento pronto: um FlowDocument serializado (XAML
/// ou RTF) não é procurável, não cabe num cartão e não pode ser colado num chamado. O Markdown
/// é o formato em que a nota é útil fora daqui; o documento é só como ela aparece enquanto se
/// escreve.
///
/// As duas direções se espelham: o que <see cref="Montar"/> desenha, <see cref="Ler"/> tem de
/// reconhecer. Por isso cada tipo de bloco tem uma marca própria e VISÍVEL no documento (o
/// tamanho do título, a borda da citação, a fonte do código), em vez de um rótulo escondido
/// numa Tag: rótulo escondido se perde no primeiro Enter, porque o WPF cria o parágrafo novo
/// copiando a FORMATAÇÃO do anterior, não o que estiver pendurado nele.
/// </summary>
public static class DocumentoMarkdown
{
    public const double TamanhoDoTexto   = 13;
    public const double TamanhoDeTitulo1 = 19;
    public const double TamanhoDeTitulo2 = 16;
    public const double TamanhoDeTitulo3 = 13.5;

    /// <summary>A caixa de marcar de uma tarefa, como caractere. Clicar nela troca uma pela outra.</summary>
    public const string CaixaVazia  = "☐ ";
    public const string CaixaMarcada = "☑ ";

    /// <summary>Recuo de um nível de lista, em unidades do WPF.</summary>
    public const double RecuoPorNivel = 22;

    public static readonly FontFamily FonteDeCodigo = new("Consolas");

    // ── Markdown → documento ─────────────────────────────────────────────────

    public static FlowDocument Montar(string? markdown)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontSize    = TamanhoDoTexto,
            LineHeight  = 19,
        };

        foreach (var bloco in Markdown.Analisar(markdown ?? string.Empty))
            doc.Blocks.Add(Desenhar(bloco));

        // Documento sem nenhum bloco não tem onde pôr o cursor.
        if (doc.Blocks.Count == 0) doc.Blocks.Add(NovoParagrafo());

        return doc;
    }

    private static Block Desenhar(BlocoMarkdown bloco) => bloco.Tipo switch
    {
        TipoDeBloco.Titulo1 => Titulo(bloco, TamanhoDeTitulo1),
        TipoDeBloco.Titulo2 => Titulo(bloco, TamanhoDeTitulo2),
        TipoDeBloco.Titulo3 => Titulo(bloco, TamanhoDeTitulo3),
        TipoDeBloco.Divisor => Divisor(),
        TipoDeBloco.Codigo  => Codigo(bloco),
        TipoDeBloco.Citacao => Citacao(bloco),
        TipoDeBloco.Tarefa  => Tarefa(bloco),
        TipoDeBloco.Item         => ComMarcador(bloco, "• "),
        TipoDeBloco.ItemNumerado => ComMarcador(bloco, bloco.Marcador + ". "),
        _ => Paragrafo(bloco),
    };

    private static Paragraph NovoParagrafo() => new() { Margin = new Thickness(0) };

    private static Paragraph Paragrafo(BlocoMarkdown bloco)
    {
        var p = NovoParagrafo();
        Preencher(p, bloco);
        return p;
    }

    private static Paragraph Titulo(BlocoMarkdown bloco, double tamanho)
    {
        var p = NovoParagrafo();
        p.FontSize   = tamanho;
        p.FontWeight = FontWeights.Bold;
        p.Margin     = new Thickness(0, tamanho == TamanhoDeTitulo1 ? 14 : 10, 0, 2);

        Preencher(p, bloco);
        return p;
    }

    /// <summary>
    /// Item de lista e tarefa são PARÁGRAFOS com o marcador escrito no começo, e não um
    /// <c>List</c> do WPF.
    ///
    /// O List desenha mais bonito e atrapalha mais: o marcador dele não é texto, então a
    /// caixa de marcar de uma tarefa não caberia lá dentro, e o Enter no fim de um item gera
    /// uma estrutura que o Markdown de uma linha por bloco não sabe representar. Com o
    /// marcador como texto, o que se vê é o que vira linha ao gravar.
    /// </summary>
    private static Paragraph ComMarcador(BlocoMarkdown bloco, string marcador)
    {
        var p = NovoParagrafo();
        p.Margin = new Thickness(bloco.Nivel * RecuoPorNivel, 0, 0, 0);
        p.Inlines.Add(new Run(marcador));

        Preencher(p, bloco);
        return p;
    }

    private static Paragraph Tarefa(BlocoMarkdown bloco)
    {
        var p = NovoParagrafo();
        p.Margin = new Thickness(bloco.Nivel * RecuoPorNivel, 0, 0, 0);
        p.Inlines.Add(new Run(bloco.Marcada ? CaixaMarcada : CaixaVazia));

        Preencher(p, bloco);

        if (bloco.Marcada)
        {
            p.TextDecorations = TextDecorations.Strikethrough;
            p.SetResourceReference(TextElement.ForegroundProperty, "TextMutedBrush");
        }

        return p;
    }

    private static Paragraph Citacao(BlocoMarkdown bloco)
    {
        var p = NovoParagrafo();
        p.BorderThickness = new Thickness(3, 0, 0, 0);
        p.Padding         = new Thickness(11, 2, 0, 2);
        p.Margin          = new Thickness(0, 4, 0, 4);
        p.FontStyle       = FontStyles.Italic;
        p.SetResourceReference(Block.BorderBrushProperty, "AccentBrush");
        p.SetResourceReference(TextElement.ForegroundProperty, "TextSecondaryBrush");

        Preencher(p, bloco);
        return p;
    }

    private static Paragraph Codigo(BlocoMarkdown bloco)
    {
        var p = NovoParagrafo();
        p.FontFamily = FonteDeCodigo;
        p.FontSize   = 12;
        p.Padding    = new Thickness(12, 9, 12, 9);
        p.Margin     = new Thickness(0, 6, 0, 6);
        p.SetResourceReference(TextElement.BackgroundProperty, "SurfaceInsetBrush");
        p.SetResourceReference(TextElement.ForegroundProperty, "TextSecondaryBrush");

        var linhas = bloco.Texto.Split('\n');
        for (var i = 0; i < linhas.Length; i++)
        {
            if (i > 0) p.Inlines.Add(new LineBreak());
            p.Inlines.Add(new Run(linhas[i]));
        }

        return p;
    }

    /// <summary>
    /// O divisor é um parágrafo VAZIO com fio embaixo. Vazio é o que o distingue na leitura de
    /// volta: um parágrafo com fio e com texto é outra coisa (e ninguém escreve isso aqui).
    /// </summary>
    private static Paragraph Divisor()
    {
        var p = NovoParagrafo();
        p.BorderThickness = new Thickness(0, 0, 0, 1);
        p.Margin          = new Thickness(0, 10, 0, 10);
        p.SetResourceReference(Block.BorderBrushProperty, "BorderSubtleBrush");
        return p;
    }

    private static void Preencher(Paragraph p, BlocoMarkdown bloco)
    {
        foreach (var trecho in bloco.Trechos)
            p.Inlines.Add(Montar(trecho));
    }

    private static Inline Montar(TrechoMarkdown trecho)
    {
        if (trecho.Link is { } endereco)
        {
            var link = new Hyperlink(new Run(trecho.Texto)) { ToolTip = endereco };
            link.SetResourceReference(TextElement.ForegroundProperty, "AccentTextBrush");

            // NavigateUri só aceita URI válida; endereço pela metade fica só no rótulo.
            if (Uri.TryCreate(endereco, UriKind.Absolute, out var uri)) link.NavigateUri = uri;

            return link;
        }

        var corrida = new Run(trecho.Texto);

        if (trecho.Negrito) corrida.FontWeight      = FontWeights.Bold;
        if (trecho.Italico) corrida.FontStyle       = FontStyles.Italic;
        if (trecho.Riscado) corrida.TextDecorations = TextDecorations.Strikethrough;

        if (trecho.Codigo)
        {
            corrida.FontFamily = FonteDeCodigo;
            corrida.SetResourceReference(TextElement.ForegroundProperty, "AccentTextBrush");
        }

        return corrida;
    }

    // ── Documento → Markdown ─────────────────────────────────────────────────

    /// <summary>
    /// Lê o documento de volta como texto com marcação.
    ///
    /// O que decide o tipo de cada linha é a FORMATAÇÃO dela, e não um rótulo guardado: é o
    /// que sobrevive ao Enter, ao Backspace e ao texto colado de fora.
    /// </summary>
    public static string Ler(FlowDocument? doc)
    {
        if (doc == null) return string.Empty;

        var linhas = new List<string>();

        foreach (var bloco in doc.Blocks)
            linhas.AddRange(LerBloco(bloco));

        // Uma linha em branco no fim é o parágrafo vazio que todo documento carrega; guardá-la
        // faria a nota crescer uma linha a cada abertura.
        while (linhas.Count > 0 && linhas[^1].Length == 0) linhas.RemoveAt(linhas.Count - 1);

        // Quebra de linha do Windows, que é a que as notas já têm no banco: daqui a nota vai
        // para o chamado, para o Bloco de Notas e para o campo de outro programa, e ainda há
        // caixa de texto por aí que mostra uma quebra solta como quadradinho.
        return string.Join("\r\n", linhas);
    }

    private static IEnumerable<string> LerBloco(Block bloco)
    {
        // Lista do WPF: pode aparecer por colagem de fora, já que ninguém a cria por aqui.
        if (bloco is List lista)
        {
            foreach (var item in lista.ListItems)
                foreach (var interno in item.Blocks)
                    foreach (var linha in LerBloco(interno))
                        yield return linha.StartsWith("- ", StringComparison.Ordinal) ? linha : "- " + linha;

            yield break;
        }

        if (bloco is not Paragraph p)
        {
            yield return string.Empty;
            yield break;
        }

        var corpo = LerInlines(p);

        // Bloco de código: fica entre cercas, e cada quebra de linha de dentro vira uma linha.
        if (EhCodigo(p))
        {
            yield return "```";
            foreach (var linha in corpo.Split('\n')) yield return linha;
            yield return "```";
            yield break;
        }

        if (corpo.Length == 0 && p.BorderThickness.Bottom > 0)
        {
            yield return "---";
            yield break;
        }

        var recuo = new string(' ', (int)Math.Round(Math.Max(0, p.Margin.Left) / RecuoPorNivel) * 2);

        if (p.BorderThickness.Left > 0)             { yield return recuo + "> " + corpo; yield break; }
        if (p.FontSize >= TamanhoDeTitulo1)         { yield return "# "   + corpo; yield break; }
        if (p.FontSize >= TamanhoDeTitulo2)         { yield return "## "  + corpo; yield break; }
        if (p.FontSize > TamanhoDoTexto)            { yield return "### " + corpo; yield break; }

        yield return recuo + corpo;
    }

    private static bool EhCodigo(Paragraph p) =>
        p.FontFamily?.Source == FonteDeCodigo.Source && p.Padding.Left > 0;

    /// <summary>
    /// O texto do parágrafo com a marcação de dentro da linha.
    ///
    /// A formatação do PARÁGRAFO não entra: num título tudo é negrito, e escrever
    /// <c>## **assim**</c> seria repetir na marcação o que o tipo do bloco já diz.
    /// </summary>
    private static string LerInlines(Paragraph p)
    {
        var texto = new StringBuilder();

        foreach (var inline in p.Inlines)
            Escrever(texto, inline, p);

        var linha = texto.ToString();

        // A caixa de marcar é caractere na tela e marcação no texto.
        if (linha.StartsWith(CaixaMarcada, StringComparison.Ordinal))
            return "- [x] " + linha[CaixaMarcada.Length..];

        if (linha.StartsWith(CaixaVazia, StringComparison.Ordinal))
            return "- [ ] " + linha[CaixaVazia.Length..];

        // E o marcador de lista também: a bolinha é texto dentro do documento, e no Markdown
        // ela é um traço. O "1. " da numerada já é o que o Markdown escreve, e passa direto.
        if (linha.StartsWith("• ", StringComparison.Ordinal))
            return "- " + linha[2..];

        return linha;
    }

    private static void Escrever(StringBuilder texto, Inline inline, Paragraph p)
    {
        switch (inline)
        {
            case LineBreak:
                texto.Append('\n');
                return;

            case Hyperlink link:
                var rotulo = new StringBuilder();
                foreach (var dentro in link.Inlines) Escrever(rotulo, dentro, p);

                var endereco = link.NavigateUri?.ToString() ?? link.ToolTip as string ?? rotulo.ToString();
                texto.Append('[').Append(rotulo).Append("](").Append(endereco).Append(')');
                return;

            case Span span:
                foreach (var dentro in span.Inlines) Escrever(texto, dentro, p);
                return;

            case Run corrida:
                texto.Append(Marcar(corrida, p));
                return;
        }
    }

    private static string Marcar(Run corrida, Paragraph p)
    {
        var texto = corrida.Text;
        if (texto.Length == 0) return texto;

        // Espaço marcado não produz marcação: "**negrito** " com o espaço dentro das marcas é
        // negrito que o Markdown não reconhece.
        var esquerda = texto.Length - texto.TrimStart().Length;
        var direita  = texto.Length - texto.TrimEnd().Length;

        if (esquerda + direita >= texto.Length) return texto;

        var nucleo = texto[esquerda..^direita];

        if (corrida.FontFamily?.Source == FonteDeCodigo.Source && !EhCodigo(p))
            nucleo = "`" + nucleo + "`";

        if (corrida.TextDecorations?.Count > 0 && p.TextDecorations?.Count is null or 0)
            nucleo = "~~" + nucleo + "~~";

        if (corrida.FontStyle == FontStyles.Italic && p.FontStyle != FontStyles.Italic)
            nucleo = "*" + nucleo + "*";

        if (corrida.FontWeight == FontWeights.Bold && p.FontWeight != FontWeights.Bold)
            nucleo = "**" + nucleo + "**";

        return texto[..esquerda] + nucleo + texto[^direita..];
    }
}
