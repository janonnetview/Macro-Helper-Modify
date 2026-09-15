using System.Windows.Media;

namespace MacroHelper.Services;

/// <summary>
/// Uma cor de destaque, nas duas versões dela.
///
/// São duas e não uma porque a mesma cor não serve aos dois temas: um tom que tem contraste
/// suficiente sobre o fundo claro fica apagado sobre o escuro, e o inverso brilha demais. A
/// versão escura de cada cor é a mesma matiz, mais clara e menos saturada — é ela que aparece
/// quando o app está no tema escuro.
/// </summary>
/// <param name="Nome">O que a pessoa lê nas Configurações. É também o que fica salvo.</param>
public sealed record CorDeDestaque(string Nome, string Claro, string Escuro)
{
    public string ParaTema(bool escuro) => escuro ? Escuro : Claro;
}

/// <summary>
/// As doze cores de destaque que as Configurações oferecem.
///
/// A lista mora aqui, e não no ViewModel, porque o <see cref="ThemeService"/> também precisa
/// dela: o que fica salvo é o NOME da cor, e é preciso traduzi-lo para o hexadecimal do tema
/// em uso toda vez que o tema muda.
/// </summary>
public static class PaletaDeDestaque
{
    /// <summary>O verde da identidade. É a cor de fábrica do app.</summary>
    public const string NomePadrao = "Verde sálvia";

    public static IReadOnlyList<CorDeDestaque> Cores { get; } =
    [
        new(NomePadrao,          "#7FA33D", "#9BC653"),
        new("Azul",              "#477DBA", "#69A3E0"),
        new("Roxo",              "#8068B5", "#A38BDB"),
        new("Turquesa",          "#2D958A", "#4BC1B5"),
        new("Coral",             "#C86B58", "#E58470"),
        new("Rosa",              "#B95E83", "#DC7DA2"),
        new("Âmbar",             "#C18A2D", "#E7B455"),
        new("Vermelho",          "#C95757", "#E47777"),
        new("Azul acinzentado",  "#5E7F94", "#82A4B8"),
        new("Terracota",         "#B86F52", "#D99073"),
        new("Lilás",             "#806FA7", "#A796CB"),
        new("Verde-azulado",     "#438A76", "#64B49D"),
    ];

    public static CorDeDestaque Padrao { get; } = Cores[0];

    /// <summary>
    /// A cor guardada nas configurações, seja qual for o formato dela.
    ///
    /// Aceita o nome (o formato de hoje) e o hexadecimal solto (o de ontem, quando a paleta
    /// tinha quinze cores fixas e nenhuma versão por tema). Um hexadecimal que não é mais da
    /// paleta vai para a cor MAIS PARECIDA em vez de cair no padrão: quem tinha escolhido o
    /// roxo antigo continua com um roxo, e não acorda com o verde da casa.
    /// </summary>
    public static CorDeDestaque Resolver(string? salvo)
    {
        if (string.IsNullOrWhiteSpace(salvo)) return Padrao;

        var texto = salvo.Trim();

        var porNome = Cores.FirstOrDefault(c => string.Equals(c.Nome, texto, StringComparison.OrdinalIgnoreCase));
        if (porNome != null) return porNome;

        var porHex = Cores.FirstOrDefault(c =>
            string.Equals(c.Claro,  texto, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.Escuro, texto, StringComparison.OrdinalIgnoreCase));
        if (porHex != null) return porHex;

        return MaisParecidaCom(texto) ?? Padrao;
    }

    private static CorDeDestaque? MaisParecidaCom(string hex)
    {
        if (!TentarLer(hex, out var alvo)) return null;

        return Cores.MinBy(c => Distancia(alvo, Ler(c.Claro)) + Distancia(alvo, Ler(c.Escuro)));
    }

    /// <summary>
    /// Distância entre duas cores dando peso ao MATIZ, que é o que a pessoa reconhece como
    /// "a minha cor".
    ///
    /// Em RGB puro o cinza da paleta antiga caía no verde-azulado, e o roxo caía no azul: duas
    /// cores com o mesmo matiz e saturações diferentes ficam longe em RGB, enquanto duas de
    /// matizes distintos e brilhos parecidos ficam perto. Por isso a conta é em HSV, com o
    /// matiz pesando dez vezes mais que saturação e brilho.
    ///
    /// O matiz ainda é multiplicado pela MENOR das duas saturações: cor quase cinza não tem
    /// matiz confiável (o do #636E72 é um azul acidental), e sem esse cuidado ela seria
    /// comparada por um ângulo que ninguém enxerga.
    /// </summary>
    private static double Distancia((double H, double S, double V) a, (double H, double S, double V) b)
    {
        const double PesoDoMatiz = 10;

        var dh = Math.Abs(a.H - b.H);
        dh = Math.Min(dh, 1 - dh) * 2;

        var peso = Math.Min(a.S, b.S);
        return (PesoDoMatiz * dh * dh * peso * peso)
             + ((a.S - b.S) * (a.S - b.S))
             + ((a.V - b.V) * (a.V - b.V));
    }

    private static (double H, double S, double V) Ler(string hex) =>
        TentarLer(hex, out var hsv) ? hsv : default;

    private static bool TentarLer(string hex, out (double H, double S, double V) hsv)
    {
        hsv = default;

        Color c;
        try { c = (Color)ColorConverter.ConvertFromString(hex); }
        catch { return false; }

        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        var maior = Math.Max(r, Math.Max(g, b));
        var menor = Math.Min(r, Math.Min(g, b));
        var amplitude = maior - menor;

        // O matiz vai de 0 a 1 (e não de 0 a 360) para que os três eixos entrem na conta na
        // mesma escala.
        var h = 0.0;
        if (amplitude > 0)
        {
            if (maior == r)      h = ((g - b) / amplitude % 6 + 6) % 6;
            else if (maior == g) h = ((b - r) / amplitude) + 2;
            else                 h = ((r - g) / amplitude) + 4;
            h /= 6;
        }

        hsv = (h, maior == 0 ? 0 : amplitude / maior, maior);
        return true;
    }
}
