using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace MacroHelper.Services;

public class VariavelInfo : INotifyPropertyChanged
{
    private string? _valorPadrao;

    /// <summary>
    /// O texto EXATO que aparece no conteúdo — <c>{data+2:dd/MM}</c>, e não só <c>data</c>.
    ///
    /// É a chave da substituição, e precisa ser o token inteiro desde que a mesma variável
    /// passou a poder aparecer com ajustes diferentes: <c>{data}</c> e <c>{data+7}</c> têm o
    /// mesmo nome e valores obrigatoriamente distintos.
    /// </summary>
    public string  Token         { get; set; } = string.Empty;

    /// <summary>O nome cru, sem ajuste nem formato — <c>data</c>. Usado para reconhecer as embutidas.</summary>
    public string  Nome          { get; set; } = string.Empty;

    /// <summary>Como o campo se apresenta no diálogo: "Data (daqui a 2 dias úteis)".</summary>
    public string  Rotulo        { get; set; } = string.Empty;

    public string  Placeholder   { get; set; } = string.Empty;
    public bool    AutoPreencher { get; set; }

    /// <summary>
    /// As opções de <c>{status:Aberto|Fechado}</c>. Vazia quando o campo é de texto livre —
    /// é o que faz o diálogo desenhar uma lista em vez de uma caixa de texto.
    /// </summary>
    public IReadOnlyList<string> Opcoes { get; set; } = [];

    public bool EhLista => Opcoes.Count > 0;

    public string? ValorPadrao
    {
        get => _valorPadrao;
        set { _valorPadrao = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>
/// Os <c>{...}</c> de dentro de uma macro: quais existem, o que já dá para preencher sozinho
/// e como o texto final fica.
///
/// A gramática, por inteiro:
///
/// <code>
///   {nome}                  campo de texto, perguntado na hora
///   {nome:valor}            idem, já vindo preenchido com "valor"
///   {nome:um|dois|tres}     lista de opções — vira uma seleção no diálogo
///
///   {data} {hora} {datahora} {mes} {usuario} {clipboard}
///                           preenchidas sozinhas, sem perguntar nada
///
///   {data+2}   {data-1}     daqui a 2 dias · ontem
///   {data+3u}               daqui a 3 dias ÚTEIS — pula sábado e domingo
///   {data+2s}               daqui a 2 semanas
///   {hora+90m} {hora+2h}    daqui a 90 minutos · daqui a 2 horas
///   {mes+1}                 mês que vem
///   {data:dd/MM}            formato próprio, no padrão do .NET
/// </code>
///
/// A ambiguidade entre formato e lista de opções tem uma regra só, decidível olhando o token:
/// depois dos dois-pontos, uma barra vertical significa lista. Nas variáveis de data e hora
/// não existe lista — ali o que vem depois dos dois-pontos é sempre formato.
/// </summary>
public static class VariavelService
{
    /// <summary>
    /// nome · ajuste opcional (<c>+2</c>, <c>-1</c>, <c>+3u</c>) · argumento opcional após <c>:</c>.
    ///
    /// O argumento aceita qualquer coisa menos chave, o que mantém <c>{a{b}}</c> fora e deixa
    /// passar barra, ponto e espaço — tudo que um formato de data ou um rótulo de opção usa.
    /// </summary>
    private static readonly Regex _regex = new(@"\{(\w+)([+-]\d+\w?)?(?::([^{}]*))?\}",
        RegexOptions.Compiled);

    /// <summary>Nomes que o app resolve sozinho, sem perguntar.</summary>
    private static readonly HashSet<string> _automaticas =
        new(StringComparer.OrdinalIgnoreCase) { "data", "hora", "datahora", "mes", "usuario", "clipboard" };

    /// <summary>
    /// Nomes que NÃO são variáveis, apesar de caberem na forma <c>{...}</c>.
    ///
    /// <c>cursor</c> é do marcador de posição, resolvido na hora de mandar as teclas;
    /// <c>macro</c> é da referência a outra macro, já expandida antes de chegar aqui. Sem esta
    /// lista, uma macro com <c>{cursor}</c> abriria um diálogo pedindo "valor para cursor".
    /// </summary>
    private static readonly HashSet<string> _reservadas =
        new(StringComparer.OrdinalIgnoreCase) { MarcadorDeCursor.NomeReservado, "macro" };

    private static CultureInfo PtBr => CultureInfo.GetCultureInfo("pt-BR");

    public static List<VariavelInfo> ExtrairVariaveis(string template, string nomeUsuario = "")
        => ExtrairVariaveis(template, nomeUsuario, DateTime.Now);

    /// <summary>
    /// Com o instante por parâmetro — é o que permite afirmar que <c>{data+1}</c> devolve o dia
    /// seguinte sem esperar até amanhã.
    /// </summary>
    public static List<VariavelInfo> ExtrairVariaveis(string template, string nomeUsuario, DateTime agora)
    {
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var lista  = new List<VariavelInfo>();

        foreach (Match m in _regex.Matches(template))
        {
            var nome = m.Groups[1].Value;
            if (_reservadas.Contains(nome)) continue;

            // Dedupe pelo TOKEN e não pelo nome: {data} e {data+7} são dois campos.
            if (!vistos.Add(m.Value)) continue;

            var ajuste    = m.Groups[2].Success ? m.Groups[2].Value : null;
            var argumento = m.Groups[3].Success ? m.Groups[3].Value : null;

            lista.Add(_automaticas.Contains(nome)
                ? Automatica(m.Value, nome, ajuste, argumento, nomeUsuario, agora)
                : Perguntada(m.Value, nome, argumento));
        }

        return lista;
    }

    /// <summary>Data, hora, mês, usuário e clipboard: já vêm com valor, e o diálogo só as mostra.</summary>
    private static VariavelInfo Automatica(string token, string nome, string? ajuste,
        string? formato, string nomeUsuario, DateTime agora)
    {
        var info = new VariavelInfo
        {
            Token         = token,
            Nome          = nome.ToLowerInvariant(),
            AutoPreencher = true,
        };

        switch (info.Nome)
        {
            case "usuario":
                info.ValorPadrao = nomeUsuario;
                info.Rotulo      = "Seu nome";
                info.Placeholder = "Usuário configurado";
                return info;

            case "clipboard":
                info.ValorPadrao = ObterClipboard();
                info.Rotulo      = "Área de transferência";
                info.Placeholder = "O que está copiado agora";
                return info;
        }

        // A unidade padrão do ajuste depende da variável, porque é o que a pessoa quer dizer:
        // {hora+2} são duas horas, {mes+1} é o mês que vem, {data+2} são dois dias.
        var unidadePadrao = info.Nome switch
        {
            "hora" => 'h',
            "mes"  => 'M',
            _      => 'd',
        };

        var momento = Deslocar(agora, ajuste, unidadePadrao);

        var formatoFinal = !string.IsNullOrEmpty(formato) ? formato : info.Nome switch
        {
            "hora"     => "HH:mm",
            "datahora" => "dd/MM/yyyy HH:mm",
            "mes"      => "MMMM/yyyy",
            _          => "dd/MM/yyyy",
        };

        info.ValorPadrao = FormatarSeguro(momento, formatoFinal);
        info.Rotulo      = RotuloDeTempo(info.Nome, ajuste, unidadePadrao);
        info.Placeholder = info.ValorPadrao;
        return info;
    }

    /// <summary>Campo de texto ou lista de opções — o que o app não tem como saber sozinho.</summary>
    private static VariavelInfo Perguntada(string token, string nome, string? argumento)
    {
        var info = new VariavelInfo
        {
            Token         = token,
            Nome          = nome,
            AutoPreencher = false,
            Rotulo        = nome,
        };

        // A barra é o que separa lista de valor padrão. Um argumento sem barra é o valor com
        // que o campo já vem preenchido — {cliente:Fulano}.
        if (argumento != null && argumento.Contains('|'))
        {
            var opcoes = argumento
                .Split('|')
                .Select(o => o.Trim())
                .Where(o => o.Length > 0)
                .ToList();

            info.Opcoes      = opcoes;
            info.ValorPadrao = opcoes.FirstOrDefault() ?? string.Empty;
            info.Placeholder = opcoes.Count == 1 ? "1 opção" : $"{opcoes.Count} opções";
            return info;
        }

        info.ValorPadrao = argumento ?? string.Empty;
        info.Placeholder = $"Valor para {nome}";
        return info;
    }

    // ── Ajuste de tempo ──────────────────────────────────────────────────────

    /// <summary>
    /// Aplica <c>+2</c>, <c>-1</c>, <c>+3u</c> e afins. Unidades: d dias · u dias úteis ·
    /// s semanas · h horas · m minutos · M meses · a anos.
    /// </summary>
    private static DateTime Deslocar(DateTime origem, string? ajuste, char unidadePadrao)
    {
        if (string.IsNullOrEmpty(ajuste)) return origem;

        var (n, letra) = LerAjuste(ajuste, unidadePadrao);
        if (n == null) return origem;

        return letra switch
        {
            'u' or 'U' => SomarDiasUteis(origem, n.Value),
            's' or 'S' => origem.AddDays(n.Value * 7),
            'h' or 'H' => origem.AddHours(n.Value),
            'm'        => origem.AddMinutes(n.Value),
            'M'        => origem.AddMonths(n.Value),
            'a' or 'A' => origem.AddYears(n.Value),
            _          => origem.AddDays(n.Value),
        };
    }

    /// <summary>
    /// Quebra "+3u" em (3, 'u') e "-1" em (-1, unidade padrão). Devolve n nulo quando o
    /// número não é legível — aí o ajuste inteiro é ignorado e a variável vale o instante atual.
    /// </summary>
    private static (int? N, char Letra) LerAjuste(string ajuste, char unidadePadrao)
    {
        var sinal = ajuste[0] == '-' ? -1 : 1;
        var corpo = ajuste[1..];
        if (corpo.Length == 0) return (null, unidadePadrao);

        var temLetra = char.IsLetter(corpo[^1]);
        var letra    = temLetra ? corpo[^1] : unidadePadrao;
        var digitos  = temLetra ? corpo[..^1] : corpo;

        return int.TryParse(digitos, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? (n * sinal, letra)
            : (null, letra);
    }

    /// <summary>
    /// Dias úteis: sábado e domingo não contam.
    ///
    /// Feriado conta como dia útil, e isso é uma escolha — a alternativa seria carregar um
    /// calendário nacional que envelhece sozinho e ainda erraria os municipais. "Retorno em
    /// 3 dias úteis" com um feriado no meio é uma conversa com o cliente, não uma conta.
    /// </summary>
    private static DateTime SomarDiasUteis(DateTime origem, int dias)
    {
        var passo  = dias < 0 ? -1 : 1;
        var faltam = Math.Abs(dias);
        var data   = origem;

        while (faltam > 0)
        {
            data = data.AddDays(passo);
            if (data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            faltam--;
        }

        return data;
    }

    /// <summary>
    /// Um formato inválido não pode derrubar a inserção: <c>ToString</c> lança
    /// <c>FormatException</c> para uma string malformada, e o estrago seria uma macro que
    /// simplesmente não entra. Aqui o valor cai no formato padrão e o texto sai assim mesmo.
    /// </summary>
    private static string FormatarSeguro(DateTime momento, string formato)
    {
        try { return momento.ToString(formato, PtBr); }
        catch (FormatException) { return momento.ToString("dd/MM/yyyy", PtBr); }
    }

    private static string RotuloDeTempo(string nome, string? ajuste, char unidadePadrao)
    {
        var baseNome = nome switch
        {
            "hora"     => "Hora",
            "datahora" => "Data e hora",
            "mes"      => "Mês",
            _          => "Data",
        };

        if (string.IsNullOrEmpty(ajuste)) return $"{baseNome} de agora";

        var (n, letra) = LerAjuste(ajuste, unidadePadrao);
        if (n == null) return baseNome;

        var quantidade = Math.Abs(n.Value);

        var unidade = letra switch
        {
            'u' or 'U' => quantidade == 1 ? "dia útil" : "dias úteis",
            's' or 'S' => quantidade == 1 ? "semana"   : "semanas",
            'h' or 'H' => quantidade == 1 ? "hora"     : "horas",
            'm'        => quantidade == 1 ? "minuto"   : "minutos",
            'M'        => quantidade == 1 ? "mês"      : "meses",
            'a' or 'A' => quantidade == 1 ? "ano"      : "anos",
            _          => quantidade == 1 ? "dia"      : "dias",
        };

        return n.Value < 0
            ? $"{baseNome} ({quantidade} {unidade} atrás)"
            : $"{baseNome} (daqui a {quantidade} {unidade})";
    }

    // ── Substituição ─────────────────────────────────────────────────────────

    /// <summary>
    /// Troca cada token pelo valor correspondente. As chaves do dicionário são os TOKENS
    /// inteiros (<c>{data+2}</c>), como vêm em <see cref="VariavelInfo.Token"/>.
    ///
    /// Token sem valor fica como está, de propósito: é o que faz uma chave escrita por engano
    /// aparecer no texto — visível, corrigível — em vez de sumir em silêncio.
    /// </summary>
    public static string Substituir(string template, IReadOnlyDictionary<string, string> valores)
        => _regex.Replace(template, m =>
            valores.TryGetValue(m.Value, out var v) ? v : m.Value);

    /// <summary>
    /// Se há algo a resolver. <c>{cursor}</c> e <c>{macro:x}</c> não contam — nenhum dos dois
    /// é pergunta para o usuário.
    /// </summary>
    public static bool TemVariaveis(string template)
    {
        foreach (Match m in _regex.Matches(template))
            if (!_reservadas.Contains(m.Groups[1].Value)) return true;

        return false;
    }

    private static string ObterClipboard()
    {
        try { return System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : string.Empty; }
        catch { return string.Empty; }
    }
}
