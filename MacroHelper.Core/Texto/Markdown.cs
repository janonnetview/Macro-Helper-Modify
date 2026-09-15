using System.Text;
using System.Text.RegularExpressions;

namespace MacroHelper.Core.Texto;

public enum TipoDeBloco
{
    Paragrafo,
    Titulo1,
    Titulo2,
    Titulo3,
    Item,
    ItemNumerado,
    Tarefa,
    Citacao,
    Codigo,
    Divisor,
}

/// <summary>Um pedaço de texto de uma linha, com o que ele carrega de formatação.</summary>
/// <param name="Link">O endereço, quando o trecho veio de <c>[texto](endereço)</c>.</param>
public sealed record TrechoMarkdown(
    string Texto,
    bool Negrito = false,
    bool Italico = false,
    bool Codigo  = false,
    bool Riscado = false,
    string? Link = null);

/// <summary>Uma linha já entendida: o que ela é e o que está escrito nela.</summary>
public sealed class BlocoMarkdown
{
    public required TipoDeBloco Tipo { get; init; }
    public required IReadOnlyList<TrechoMarkdown> Trechos { get; init; }

    /// <summary>O texto sem a marcação de bloco. É o que o bloco de código mostra.</summary>
    public required string Texto { get; init; }

    /// <summary>
    /// A linha do conteúdo original de onde este bloco saiu, contada de zero.
    ///
    /// Existe por causa da caixa de marcar: clicar nela na prévia precisa reescrever a linha
    /// certa do texto, e o único jeito de saber qual é guardando o número desde a leitura.
    /// </summary>
    public required int Linha { get; init; }

    /// <summary>Só para <see cref="TipoDeBloco.Tarefa"/>: se a caixa está marcada.</summary>
    public bool Marcada { get; init; }

    /// <summary>Só para <see cref="TipoDeBloco.ItemNumerado"/>: o número que a pessoa escreveu.</summary>
    public string Marcador { get; init; } = string.Empty;

    /// <summary>Recuo do item, em níveis. Dois espaços (ou um tab) valem um nível, até três.</summary>
    public int Nivel { get; init; }
}

/// <summary>
/// O pedaço de Markdown que as notas entendem.
///
/// É um subconjunto deliberado, e não uma implementação da especificação: títulos, listas,
/// caixas de marcar, citação, código, divisor e a formatação de dentro da linha. O que está
/// fora daqui continua sendo texto, porque quem escreve uma nota de reunião não deve descobrir
/// que um caractere solto mudou o desenho do parágrafo.
///
/// Duas escolhas se afastam do Markdown de verdade, e de propósito:
///
/// 1. Cada linha é um bloco. No Markdown clássico duas linhas seguidas viram um parágrafo só,
///    e a quebra que a pessoa digitou some. Numa caixa de anotação isso se lê como defeito.
/// 2. A formatação não aninha: "**negrito com *itálico* dentro**" sai como negrito com os
///    asteriscos à mostra. Aninhar pediria uma árvore, e o ganho não paga a leitura do código.
/// </summary>
public static class Markdown
{
    private static readonly Regex _titulo   = new(@"^(#{1,3})\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex _tarefa   = new(@"^[-*+]\s+\[([ xX])\]\s*(.*)$", RegexOptions.Compiled);
    private static readonly Regex _item     = new(@"^[-*+]\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex _numerado = new(@"^(\d{1,3})[.)]\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex _citacao  = new(@"^>\s?(.*)$", RegexOptions.Compiled);
    private static readonly Regex _divisor  = new(@"^(-{3,}|\*{3,}|_{3,})$", RegexOptions.Compiled);

    private static readonly Regex _trechos = new(
        @"(\*\*[^*]+\*\*|__[^_]+__|~~[^~]+~~|`[^`]+`|\*[^*\n]+\*|\[[^\]\n]+\]\([^)\s]+\))",
        RegexOptions.Compiled);

    private const string Cerca = "```";

    public static IReadOnlyList<BlocoMarkdown> Analisar(string conteudo)
    {
        var blocos = new List<BlocoMarkdown>();
        if (string.IsNullOrEmpty(conteudo)) return blocos;

        var linhas = conteudo.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        for (var i = 0; i < linhas.Length; i++)
        {
            var linha = linhas[i];

            if (linha.TrimStart().StartsWith(Cerca, StringComparison.Ordinal))
            {
                var inicio = i;
                var codigo = new List<string>();

                // Cerca sem fechamento vale até o fim do texto: mostrar o resto como código é
                // menos ruim do que engolir tudo o que veio depois dela.
                for (i++; i < linhas.Length && !linhas[i].TrimStart().StartsWith(Cerca, StringComparison.Ordinal); i++)
                    codigo.Add(linhas[i]);

                var texto = string.Join("\n", codigo);
                blocos.Add(new BlocoMarkdown
                {
                    Tipo    = TipoDeBloco.Codigo,
                    Texto   = texto,
                    Trechos = [new TrechoMarkdown(texto, Codigo: true)],
                    Linha   = inicio,
                });
                continue;
            }

            blocos.Add(Ler(linha, i));
        }

        return blocos;
    }

    private static BlocoMarkdown Ler(string linha, int numero)
    {
        var nivel = ContarRecuo(linha, out var corpo);

        if (_divisor.IsMatch(corpo))
            return Montar(TipoDeBloco.Divisor, string.Empty, numero);

        var titulo = _titulo.Match(corpo);
        if (titulo.Success)
        {
            var tipo = titulo.Groups[1].Value.Length switch
            {
                1 => TipoDeBloco.Titulo1,
                2 => TipoDeBloco.Titulo2,
                _ => TipoDeBloco.Titulo3,
            };
            return Montar(tipo, titulo.Groups[2].Value, numero);
        }

        var tarefa = _tarefa.Match(corpo);
        if (tarefa.Success)
            return Montar(TipoDeBloco.Tarefa, tarefa.Groups[2].Value, numero, nivel,
                          marcada: !tarefa.Groups[1].Value.Equals(" ", StringComparison.Ordinal));

        var item = _item.Match(corpo);
        if (item.Success)
            return Montar(TipoDeBloco.Item, item.Groups[1].Value, numero, nivel);

        var numerado = _numerado.Match(corpo);
        if (numerado.Success)
            return Montar(TipoDeBloco.ItemNumerado, numerado.Groups[2].Value, numero, nivel,
                          marcador: numerado.Groups[1].Value);

        var citacao = _citacao.Match(corpo);
        if (citacao.Success)
            return Montar(TipoDeBloco.Citacao, citacao.Groups[1].Value, numero, nivel);

        // O parágrafo guarda a linha inteira, com o recuo: em texto solto o espaço da frente é
        // do autor, e não marcação de nada.
        return Montar(TipoDeBloco.Paragrafo, linha, numero);
    }

    private static BlocoMarkdown Montar(TipoDeBloco tipo, string texto, int linha,
                                        int nivel = 0, bool marcada = false, string marcador = "") =>
        new()
        {
            Tipo     = tipo,
            Texto    = texto,
            Trechos  = tipo == TipoDeBloco.Divisor ? [] : Trechos(texto),
            Linha    = linha,
            Nivel    = nivel,
            Marcada  = marcada,
            Marcador = marcador,
        };

    private static int ContarRecuo(string linha, out string corpo)
    {
        var espacos = 0;
        var i = 0;

        for (; i < linha.Length; i++)
        {
            if (linha[i] == ' ')       espacos += 1;
            else if (linha[i] == '\t') espacos += 2;
            else break;
        }

        corpo = linha[i..];
        return Math.Min(espacos / 2, 3);
    }

    /// <summary>Quebra uma linha nos pedaços formatados dela. Texto sem marcação sai num trecho só.</summary>
    public static IReadOnlyList<TrechoMarkdown> Trechos(string linha)
    {
        var trechos = new List<TrechoMarkdown>();
        var pos = 0;

        foreach (Match m in _trechos.Matches(linha))
        {
            if (m.Index > pos) trechos.Add(new TrechoMarkdown(linha[pos..m.Index]));
            trechos.Add(Interpretar(m.Value));
            pos = m.Index + m.Length;
        }

        if (pos < linha.Length) trechos.Add(new TrechoMarkdown(linha[pos..]));

        return trechos;
    }

    private static TrechoMarkdown Interpretar(string token)
    {
        if (token.StartsWith("**", StringComparison.Ordinal) || token.StartsWith("__", StringComparison.Ordinal))
            return new TrechoMarkdown(token[2..^2], Negrito: true);

        if (token.StartsWith("~~", StringComparison.Ordinal))
            return new TrechoMarkdown(token[2..^2], Riscado: true);

        if (token.StartsWith("`", StringComparison.Ordinal))
            return new TrechoMarkdown(token[1..^1], Codigo: true);

        if (token.StartsWith("[", StringComparison.Ordinal))
        {
            var corte = token.IndexOf("](", StringComparison.Ordinal);
            return new TrechoMarkdown(token[1..corte], Link: token[(corte + 2)..^1]);
        }

        return new TrechoMarkdown(token[1..^1], Italico: true);
    }

    /// <summary>
    /// O texto sem nenhuma marcação, numa linha só.
    ///
    /// É o que o cartão da lista mostra e o que vira título quando a pessoa não escreveu um:
    /// sem isto o resumo de uma nota de reunião começa com "## " e o título dela nasce com um
    /// asterisco no meio.
    /// </summary>
    public static string ParaTextoSimples(string conteudo)
    {
        var texto = new StringBuilder();

        foreach (var bloco in Analisar(conteudo))
        {
            // A caixa marcada vira um "visto" de verdade: no resumo do cartão, "[x] ligar para
            // o cliente" e "ligar para o cliente" precisam continuar sendo coisas diferentes.
            var linha = bloco.Tipo switch
            {
                TipoDeBloco.Divisor => string.Empty,
                TipoDeBloco.Tarefa  => (bloco.Marcada ? "✓ " : string.Empty) + Juntar(bloco),
                _                   => Juntar(bloco),
            };

            if (string.IsNullOrWhiteSpace(linha)) continue;

            if (texto.Length > 0) texto.Append(' ');
            texto.Append(linha.Trim());
        }

        return texto.ToString();
    }

    private static string Juntar(BlocoMarkdown bloco) =>
        string.Concat(bloco.Trechos.Select(t => t.Texto));

    /// <summary>
    /// Marca ou desmarca a caixa da linha dada, devolvendo o conteúdo inteiro atualizado.
    ///
    /// Reescreve o TEXTO, e não um estado à parte, porque a nota é o texto: marca que não volta
    /// para o conteúdo some ao salvar. Linha que não é caixa de marcar volta intacta.
    /// </summary>
    public static string AlternarTarefa(string conteudo, int linha)
    {
        // A quebra de linha do arquivo é preservada: reescrever um texto que veio com CRLF em
        // LF puro mudaria toda linha da nota num diff de uma marca só.
        var quebra = conteudo.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var linhas = conteudo.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        if (linha < 0 || linha >= linhas.Length) return conteudo;

        var recuo = linhas[linha].Length - linhas[linha].TrimStart().Length;
        var alvo  = _tarefa.Match(linhas[linha][recuo..]);
        if (!alvo.Success) return conteudo;

        var marca = alvo.Groups[1].Value.Equals(" ", StringComparison.Ordinal) ? "x" : " ";

        linhas[linha] = $"{linhas[linha][..recuo]}- [{marca}] {alvo.Groups[2].Value}";
        return string.Join(quebra, linhas);
    }
}
