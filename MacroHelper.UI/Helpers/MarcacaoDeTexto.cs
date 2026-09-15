using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
// WinForms traz os mesmos nomes do WPF; aqui é sempre WPF.
using Brush = System.Windows.Media.Brush;
using FontFamily = System.Windows.Media.FontFamily;
using FontStyle = System.Windows.FontStyle;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using RichTextBox = System.Windows.Controls.RichTextBox;

namespace MacroHelper.UI.Helpers;

/// <summary>
/// Aplica a formatação no documento que está sendo editado.
///
/// A nota é escrita com a formatação à vista, e não com a marcação: clicar em B deixa o trecho
/// NEGRITO, sem asterisco nenhum aparecendo. A marcação existe só no texto gravado, e quem
/// converte de um lado para o outro é o <see cref="DocumentoMarkdown"/>.
///
/// Mora aqui, e não no code-behind de uma tela, porque os MESMOS botões e as MESMAS teclas
/// precisam valer nos dois lugares onde se escreve uma nota: o editor da tela de Notas e a
/// janela flutuante que abre ao lado da busca rápida.
///
/// Tudo aqui mexe na SELEÇÃO por propriedade, e não pelos comandos de edição do WPF
/// (EditingCommands.ToggleBold e parentes). Os comandos exigem que a caixa esteja com o foco
/// do teclado, e a barra flutuante é outra janela: o clique nela chega com o foco em trânsito,
/// e o botão não faria nada de vez em quando — que é o pior tipo de defeito de barra.
/// </summary>
public static class MarcacaoDeTexto
{
    /// <summary>
    /// O marcador de item de lista, como texto.
    ///
    /// Bolinha e ESPAÇO, e não bolinha e tab: o tab do WPF salta para a próxima parada fixa, e
    /// o item ficava com um vão de quatro caracteres enquanto a caixa de marcar da tarefa, que
    /// é um caractere e um espaço, ficava colada. Lado a lado, as duas listas discordavam.
    /// </summary>
    public const string MarcadorDeItem = "• ";

    /// <summary>
    /// Aplica a formatação de nome <paramref name="marcacao"/> — o mesmo nome que vai na Tag
    /// dos botões da barra. Nome desconhecido não faz nada: botão calado é melhor do que botão
    /// que estraga o parágrafo.
    /// </summary>
    public static void Aplicar(RichTextBox caixa, string marcacao)
    {
        switch (marcacao)
        {
            case "negrito": AlternarNegrito(caixa); break;
            case "italico": AlternarItalico(caixa); break;
            case "riscado": AlternarRiscado(caixa); break;
            case "codigo":  AlternarCodigo(caixa);  break;

            case "titulo":   AlternarTitulo(caixa); break;
            case "lista":    AlternarMarcador(caixa, MarcadorDeItem); break;
            case "numerada": Numerar(caixa); break;
            case "tarefa":   AlternarMarcador(caixa, DocumentoMarkdown.CaixaVazia); break;
            case "citacao":  AlternarCitacao(caixa); break;
            case "divisor":  InserirDivisor(caixa); break;

            default: return;
        }

        caixa.Focus();
    }

    /// <summary>Escreve um caractere (ou qualquer texto curto) no lugar do que está selecionado.</summary>
    public static void Inserir(RichTextBox caixa, string texto)
    {
        var selecao = caixa.Selection;
        selecao.Text = texto;

        // O cursor fica DEPOIS do que entrou, e não com ele selecionado: quem escolheu um sinal
        // vai continuar escrevendo, não trocá-lo.
        caixa.CaretPosition = selecao.End;
        caixa.Focus();
    }

    /// <summary>
    /// Transforma o trecho selecionado em link.
    ///
    /// Sem seleção, o próprio endereço vira o texto do link: é o caso de quem copiou um
    /// endereço e quer só largá-lo na nota.
    /// </summary>
    public static void Ligar(RichTextBox caixa, string endereco)
    {
        if (string.IsNullOrWhiteSpace(endereco)) return;

        if (caixa.Selection.IsEmpty) caixa.Selection.Text = endereco;

        var link = new Hyperlink(caixa.Selection.Start, caixa.Selection.End) { ToolTip = endereco };
        link.SetResourceReference(TextElement.ForegroundProperty, "AccentTextBrush");

        if (Uri.TryCreate(endereco, UriKind.Absolute, out var uri)) link.NavigateUri = uri;

        caixa.CaretPosition = link.ElementEnd;
        caixa.Focus();
    }

    /// <summary>
    /// Os atalhos de teclado da formatação, para quem não quer tirar a mão do teclado.
    ///
    /// São os do Word e os de todo editor com Markdown (Ctrl+B, Ctrl+I), mais dois de casa para
    /// riscado e código. Devolve <c>true</c> quando tratou a tecla — quem chama marca o evento
    /// como tratado. Ctrl+K fica de fora daqui: link precisa de endereço, e pedi-lo é da barra.
    /// </summary>
    public static bool Atalho(RichTextBox caixa, KeyEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return false;

        var comShift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        var marcacao = e.Key switch
        {
            Key.X when comShift => "riscado",
            Key.E when comShift => "codigo",
            _                   => null,
        };

        if (marcacao == null) return false;

        Aplicar(caixa, marcacao);
        return true;
    }

    // ── Formatação de trecho ─────────────────────────────────────────────────

    private static void AlternarNegrito(RichTextBox caixa)
    {
        var negrito = caixa.Selection.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight peso
                      && peso == FontWeights.Bold;

        caixa.Selection.ApplyPropertyValue(
            TextElement.FontWeightProperty, negrito ? FontWeights.Normal : FontWeights.Bold);
    }

    private static void AlternarItalico(RichTextBox caixa)
    {
        var italico = caixa.Selection.GetPropertyValue(TextElement.FontStyleProperty) is FontStyle estilo
                      && estilo == FontStyles.Italic;

        caixa.Selection.ApplyPropertyValue(
            TextElement.FontStyleProperty, italico ? FontStyles.Normal : FontStyles.Italic);
    }

    private static void AlternarRiscado(RichTextBox caixa)
    {
        var atual = caixa.Selection.GetPropertyValue(Inline.TextDecorationsProperty);
        var riscado = atual is TextDecorationCollection { Count: > 0 };

        caixa.Selection.ApplyPropertyValue(
            Inline.TextDecorationsProperty,
            riscado ? null : TextDecorations.Strikethrough);
    }

    private static void AlternarCodigo(RichTextBox caixa)
    {
        var atual = caixa.Selection.GetPropertyValue(TextElement.FontFamilyProperty) as FontFamily;
        var codigo = atual?.Source == DocumentoMarkdown.FonteDeCodigo.Source;

        if (codigo)
        {
            // Volta a herdar a fonte e a cor do documento em vez de fixar as do tema aqui: o
            // tema pode mudar com a nota aberta.
            caixa.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, caixa.FontFamily);
            caixa.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, caixa.Foreground);
            return;
        }

        caixa.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, DocumentoMarkdown.FonteDeCodigo);

        if (caixa.TryFindResource("AccentTextBrush") is Brush destaque)
            caixa.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, destaque);
    }

    // ── Formatação de linha ──────────────────────────────────────────────────

    private static void AlternarTitulo(RichTextBox caixa)
    {
        var paragrafos = Paragrafos(caixa).ToList();
        if (paragrafos.Count == 0) return;

        // Um clique põe título, o outro tira, como todo botão desta barra. Manda o primeiro
        // parágrafo: com vários selecionados, o que importa é ter todos iguais no fim.
        var tirar = paragrafos[0].FontSize >= DocumentoMarkdown.TamanhoDeTitulo3;

        foreach (var p in paragrafos)
        {
            if (tirar)
            {
                p.ClearValue(TextElement.FontSizeProperty);
                p.ClearValue(TextElement.FontWeightProperty);
                p.Margin = new Thickness(p.Margin.Left, 0, 0, 0);
                continue;
            }

            p.FontSize   = DocumentoMarkdown.TamanhoDeTitulo2;
            p.FontWeight = FontWeights.Bold;
            p.Margin     = new Thickness(p.Margin.Left, 10, 0, 2);
        }
    }

    private static void AlternarCitacao(RichTextBox caixa)
    {
        var paragrafos = Paragrafos(caixa).ToList();
        if (paragrafos.Count == 0) return;

        var tirar = paragrafos[0].BorderThickness.Left > 0;

        foreach (var p in paragrafos)
        {
            if (tirar)
            {
                p.ClearValue(Block.BorderThicknessProperty);
                p.ClearValue(Block.BorderBrushProperty);
                p.ClearValue(Block.PaddingProperty);
                p.ClearValue(TextElement.FontStyleProperty);
                p.ClearValue(TextElement.ForegroundProperty);
                continue;
            }

            p.BorderThickness = new Thickness(3, 0, 0, 0);
            p.Padding         = new Thickness(11, 2, 0, 2);
            p.FontStyle       = FontStyles.Italic;
            p.SetResourceReference(Block.BorderBrushProperty, "AccentBrush");
            p.SetResourceReference(TextElement.ForegroundProperty, "TextSecondaryBrush");
        }
    }

    /// <summary>
    /// Põe (ou tira) o marcador no começo de cada linha tocada pela seleção.
    ///
    /// Marcar cinco linhas de uma vez é o caso comum de virar lista: elas já estão escritas
    /// quando alguém decide isso. Uma linha que já tem OUTRO marcador troca de marcador em vez
    /// de ganhar dois — item que vira tarefa é a mesma linha, não uma linha nova.
    /// </summary>
    private static void AlternarMarcador(RichTextBox caixa, string marcador)
    {
        var paragrafos = Paragrafos(caixa).ToList();
        if (paragrafos.Count == 0) return;

        var tirar = paragrafos.All(p => MarcadorDe(p) == marcador);

        foreach (var p in paragrafos)
        {
            TirarMarcador(p);
            if (!tirar) PorMarcador(p, marcador);
        }
    }

    private static void Numerar(RichTextBox caixa)
    {
        var paragrafos = Paragrafos(caixa).ToList();
        if (paragrafos.Count == 0) return;

        var tirar = paragrafos.All(p => EhNumero(MarcadorDe(p)));

        var numero = 1;
        foreach (var p in paragrafos)
        {
            TirarMarcador(p);
            if (!tirar) PorMarcador(p, $"{numero++}. ");
        }
    }

    private static void InserirDivisor(RichTextBox caixa)
    {
        if (caixa.CaretPosition.Paragraph is not { } atual) return;

        var fio = new Paragraph
        {
            Margin          = new Thickness(0, 10, 0, 10),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        fio.SetResourceReference(Block.BorderBrushProperty, "BorderSubtleBrush");

        var depois = new Paragraph { Margin = new Thickness(0) };

        caixa.Document.Blocks.InsertAfter(atual, fio);
        caixa.Document.Blocks.InsertAfter(fio, depois);

        caixa.CaretPosition = depois.ContentStart;
    }

    // ── Marcadores ───────────────────────────────────────────────────────────

    /// <summary>O marcador escrito no começo do parágrafo, ou vazio se não houver nenhum.</summary>
    public static string MarcadorDe(Paragraph p)
    {
        var texto = PrimeiroTexto(p);

        if (texto.StartsWith(DocumentoMarkdown.CaixaVazia,   StringComparison.Ordinal)) return DocumentoMarkdown.CaixaVazia;
        if (texto.StartsWith(DocumentoMarkdown.CaixaMarcada, StringComparison.Ordinal)) return DocumentoMarkdown.CaixaMarcada;
        if (texto.StartsWith(MarcadorDeItem, StringComparison.Ordinal)) return MarcadorDeItem;

        // "12. " e companhia: número, ponto e espaço é o marcador da lista numerada, que é o
        // mesmo que o Markdown escreve.
        var ponto = texto.IndexOf(". ", StringComparison.Ordinal);
        if (ponto is > 0 and <= 3 && texto[..ponto].All(char.IsDigit)) return texto[..(ponto + 2)];

        return string.Empty;
    }

    private static bool EhNumero(string marcador) =>
        marcador.Length > 2 && marcador.EndsWith(". ", StringComparison.Ordinal);

    /// <summary>Escreve o marcador no começo do parágrafo, como texto comum.</summary>
    public static void PorMarcador(Paragraph p, string marcador)
    {
        if (p.Inlines.FirstInline is { } primeiro) p.Inlines.InsertBefore(primeiro, new Run(marcador));
        else p.Inlines.Add(new Run(marcador));
    }

    /// <summary>Tira o marcador do começo do parágrafo, se houver, junto com o risco da tarefa feita.</summary>
    public static void TirarMarcador(Paragraph p)
    {
        var marcador = MarcadorDe(p);
        if (marcador.Length == 0) return;

        if (p.Inlines.FirstInline is Run corrida)
            corrida.Text = corrida.Text[Math.Min(marcador.Length, corrida.Text.Length)..];

        if (marcador == DocumentoMarkdown.CaixaMarcada)
        {
            p.ClearValue(Paragraph.TextDecorationsProperty);
            p.ClearValue(TextElement.ForegroundProperty);
        }
    }

    /// <summary>
    /// Marca ou desmarca a tarefa do parágrafo: troca a caixinha e risca (ou desrisca) a linha.
    ///
    /// O risco fica no PARÁGRAFO, e não em cada trecho: assim ele não briga com o negrito de
    /// uma palavra do meio da frase, e sai inteiro quando a tarefa é desmarcada.
    /// </summary>
    public static void AlternarTarefa(Paragraph p)
    {
        var marcador = MarcadorDe(p);
        var feita    = marcador == DocumentoMarkdown.CaixaMarcada;

        if (!feita && marcador != DocumentoMarkdown.CaixaVazia) return;

        if (p.Inlines.FirstInline is Run corrida)
            corrida.Text = (feita ? DocumentoMarkdown.CaixaVazia : DocumentoMarkdown.CaixaMarcada)
                           + corrida.Text[Math.Min(marcador.Length, corrida.Text.Length)..];

        if (feita)
        {
            p.ClearValue(Paragraph.TextDecorationsProperty);
            p.ClearValue(TextElement.ForegroundProperty);
            return;
        }

        p.TextDecorations = TextDecorations.Strikethrough;
        p.SetResourceReference(TextElement.ForegroundProperty, "TextMutedBrush");
    }

    private static string PrimeiroTexto(Paragraph p) =>
        p.Inlines.FirstInline is Run corrida ? corrida.Text : string.Empty;

    /// <summary>Os parágrafos que a seleção encosta, do primeiro ao último.</summary>
    public static IEnumerable<Paragraph> Paragrafos(RichTextBox caixa)
    {
        var primeiro = caixa.Selection.Start.Paragraph;
        var ultimo   = caixa.Selection.End.Paragraph;

        if (primeiro == null) yield break;

        for (Block? atual = primeiro; atual != null; atual = atual.NextBlock)
        {
            if (atual is Paragraph p) yield return p;
            if (ReferenceEquals(atual, ultimo)) yield break;
        }
    }
}
