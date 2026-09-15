using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Markup;
using System.Text;
using System.Text.RegularExpressions;

namespace MacroHelper.Tests;

/// <summary>
/// Leitura dos arquivos XAML como texto, para os testes que verificam bindings e chaves de
/// recurso. Não instancia nada do WPF: o objetivo é justamente checar antes de rodar.
/// </summary>
internal static class Xaml
{
    /// <summary>
    /// Sobe a partir da pasta de saída dos testes até achar a raiz do repositório. Falhar aqui
    /// precisa ser alto e claro: um "não encontrei os arquivos" que virasse teste verde
    /// esvaziaria toda esta suíte sem ninguém perceber.
    /// </summary>
    public static string RaizDoRepositorio { get; } = AcharRaiz();

    public static string PastaDaUI => Path.Combine(RaizDoRepositorio, "MacroHelper.UI");

    private static string AcharRaiz()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta != null)
        {
            if (File.Exists(Path.Combine(pasta.FullName, "MacroHelper.sln"))) return pasta.FullName;
            pasta = pasta.Parent;
        }

        throw new InvalidOperationException(
            $"Não achei MacroHelper.sln subindo a partir de {AppContext.BaseDirectory}. " +
            "Os testes de XAML leem os arquivos do código-fonte e precisam rodar dentro do repositório.");
    }

    public static string Ler(string caminhoRelativo) =>
        File.ReadAllText(Path.Combine(PastaDaUI, caminhoRelativo), Encoding.UTF8);

    /// <summary>Todo .xaml da UI: App.xaml, os temas e as telas.</summary>
    public static IEnumerable<string> TodosOsArquivos() =>
        Directory.EnumerateFiles(PastaDaUI, "*.xaml", SearchOption.AllDirectories)
            .Where(c => !c.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(c => !c.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Order();

    /// <summary>
    /// Os recursos do App.xaml montados na mão, para os testes que precisam construir uma tela
    /// de verdade. LoadComponent não serve aqui: o host de teste já fixou a ResourceAssembly
    /// dele, e o App.xaml seria procurado dentro do assembly errado. Então o dicionário de
    /// dentro do arquivo é lido como texto, com os xmlns que moravam no elemento Application e
    /// com o tema apontado por URI absoluta.
    /// </summary>
    public static ResourceDictionary RecursosDoApp(string tema = "Dark")
    {
        var texto = Ler("App.xaml");

        var inicio = texto.IndexOf("<ResourceDictionary>", StringComparison.Ordinal);
        var fim    = texto.LastIndexOf("</ResourceDictionary>", StringComparison.Ordinal)
                     + "</ResourceDictionary>".Length;

        if (inicio < 0)
            throw new InvalidOperationException(
                "Não achei o <ResourceDictionary> do App.xaml. Se a estrutura do arquivo mudou, " +
                "este helper precisa acompanhar — sem ele nenhuma tela monta nos testes.");

        var abertura =
            """<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" """ +
            """xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" """ +
            """xmlns:conv="clr-namespace:MacroHelper.UI.Converters;assembly=MacroHelper" """ +
            """xmlns:prim="clr-namespace:System.Windows.Controls.Primitives;assembly=PresentationFramework">""";

        // Os dois dicionários mesclados viram URI absoluta: sem BaseUri, um Source relativo
        // ("/Styles/Tokens.xaml") é procurado dentro do assembly do host de teste, e o que se
        // ganha é um IOException no meio da suíte inteira.
        var dicionario = texto[inicio..fim]
            .Replace("<ResourceDictionary>", abertura)
            .Replace("Source=\"/Themes/Light.xaml\"",
                     $"Source=\"pack://application:,,,/MacroHelper;component/Themes/{tema}.xaml\"")
            .Replace("Source=\"/Styles/Tokens.xaml\"",
                     "Source=\"pack://application:,,,/MacroHelper;component/Styles/Tokens.xaml\"");

        return (ResourceDictionary)XamlReader.Parse(dicionario);
    }

    // ── Caminhos de binding ──────────────────────────────────────────────────────

    private static readonly Regex _bindingElemento =
        new(@"<Binding\s+([^>]*?)/?>", RegexOptions.Compiled);

    /// <summary>
    /// Caminhos de propriedade citados no arquivo, tanto na forma <c>{Binding Foo}</c> quanto
    /// na forma de elemento <c>&lt;Binding Path="Foo"/&gt;</c> usada dentro de MultiBinding.
    ///
    /// Bindings com ElementName ficam de fora: eles apontam para outro controle da própria tela,
    /// não para o DataContext. <c>RelativeSource Self</c> também, e pela mesma razão — o alvo é
    /// o próprio controle, então <c>{Binding Tag, RelativeSource={RelativeSource Self}}</c> é o
    /// Tag do botão, e não uma propriedade que o ViewModel precise ter.
    ///
    /// <c>RelativeSource AncestorType</c> continua valendo: esse aponta para o DataContext da
    /// tela, que é justamente o que interessa conferir.
    /// </summary>
    public static IReadOnlyList<string> CaminhosDeBinding(string xaml)
    {
        var caminhos = new List<string>();

        foreach (var expressao in ExpressoesEntreChaves(xaml))
        {
            if (NaoApontaParaODataContext(expressao)) continue;
            var caminho = PrimeiroCaminho(expressao["Binding".Length..]);
            if (caminho != null) caminhos.Add(caminho);
        }

        foreach (Match m in _bindingElemento.Matches(xaml))
        {
            var atributos = m.Groups[1].Value;
            if (NaoApontaParaODataContext(atributos)) continue;

            var path = Regex.Match(atributos, @"Path=""([^""]+)""");
            if (path.Success) caminhos.Add(path.Groups[1].Value);
        }

        return caminhos.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }

    private static bool NaoApontaParaODataContext(string expressao) =>
        expressao.Contains("ElementName=", StringComparison.Ordinal) ||
        expressao.Contains("{RelativeSource Self}", StringComparison.Ordinal);

    /// <summary>Acha cada {Binding ...} contando chaves, para não parar num {StaticResource} aninhado.</summary>
    private static IEnumerable<string> ExpressoesEntreChaves(string xaml)
    {
        const string abertura = "{Binding";
        var i = 0;

        while ((i = xaml.IndexOf(abertura, i, StringComparison.Ordinal)) >= 0)
        {
            // "{BindingGroup" e afins não valem.
            var depois = i + abertura.Length;
            if (depois < xaml.Length && (char.IsLetterOrDigit(xaml[depois]) || xaml[depois] == '_'))
            {
                i = depois;
                continue;
            }

            var profundidade = 0;
            var fim = i;
            for (; fim < xaml.Length; fim++)
            {
                if (xaml[fim] == '{') profundidade++;
                else if (xaml[fim] == '}' && --profundidade == 0) break;
            }

            yield return xaml[(i + 1)..Math.Min(fim, xaml.Length)];
            i = depois;
        }
    }

    /// <summary>
    /// O caminho é o primeiro item sem "=" da lista separada por vírgulas, ou o que vier depois
    /// de Path=. Devolve null para <c>{Binding}</c> puro e para <c>{Binding .}</c>.
    /// </summary>
    private static string? PrimeiroCaminho(string corpo)
    {
        foreach (var item in SepararPorVirgula(corpo).Select(p => p.Trim()))
        {
            if (item.Length == 0) continue;

            if (item.StartsWith("Path=", StringComparison.Ordinal))
                return Limpar(item["Path=".Length..]);

            if (!item.Contains('=', StringComparison.Ordinal))
                return Limpar(item);
        }

        return null;
    }

    private static string? Limpar(string caminho)
    {
        caminho = caminho.Trim().Trim('\'', '"');
        return caminho is "" or "." ? null : caminho;
    }

    private static IEnumerable<string> SepararPorVirgula(string texto)
    {
        var profundidade = 0;
        var inicio = 0;

        for (var i = 0; i < texto.Length; i++)
        {
            if (texto[i] == '{') profundidade++;
            else if (texto[i] == '}') profundidade--;
            else if (texto[i] == ',' && profundidade == 0)
            {
                yield return texto[inicio..i];
                inicio = i + 1;
            }
        }

        yield return texto[inicio..];
    }

    private static readonly Regex _inputBindings =
        new(@"<[A-Za-z]+\.InputBindings>(.*?)</[A-Za-z]+\.InputBindings>",
            RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>O conteúdo de cada bloco &lt;X.InputBindings&gt; do arquivo.</summary>
    public static IEnumerable<string> BlocosDeInputBinding(string xaml) =>
        _inputBindings.Matches(xaml).Select(m => m.Groups[1].Value);

    // ── Chaves de recurso ────────────────────────────────────────────────────────

    private static readonly Regex _chaveDefinida = new(@"x:Key=""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex _chaveUsada =
        new(@"\{(?:Static|Dynamic)Resource\s+([A-Za-z_][\w]*)\s*\}", RegexOptions.Compiled);

    public static IReadOnlySet<string> ChavesDefinidas(string xaml) =>
        _chaveDefinida.Matches(xaml).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> ChavesUsadas(string xaml) =>
        _chaveUsada.Matches(xaml).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    // ── Reflexão ─────────────────────────────────────────────────────────────────

    public static bool TemPropriedade(Type tipo, string nome) =>
        tipo.GetProperty(nome, BindingFlags.Public | BindingFlags.Instance) != null;
}
