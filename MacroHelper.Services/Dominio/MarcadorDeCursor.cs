using System.Text.RegularExpressions;

namespace MacroHelper.Services;

/// <summary>
/// Onde o cursor para depois de a macro entrar.
///
/// Um modelo com lacuna no meio — "Prezado , seguem em anexo" — hoje obriga a percorrer o
/// texto de seta até o ponto certo, toda vez. Marcar esse ponto no conteúdo resolve de uma
/// vez, e é o que todo expansor de texto tem: <c>%|</c> no TextExpander e no PhraseExpress,
/// <c>$|$</c> no Espanso, <c>#{cursor}</c> no Beeftext.
///
/// Aqui a sintaxe é <c>{|}</c> — a mesma família de chaves de <c>{variavel}</c> e
/// <c>{macro:atalho}</c>, e impossível de confundir com uma delas: o pipe não é caractere de
/// nome. <c>{cursor}</c> vale como apelido, para quem preferir escrever por extenso.
///
/// Só o PRIMEIRO marcador posiciona; os demais são apagados. Um cursor só existe.
/// </summary>
public static class MarcadorDeCursor
{
    /// <summary>As duas formas aceitas: a curta e a por extenso.</summary>
    private static readonly Regex _marcador = new(@"\{\|\}|\{cursor\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Nome reservado. <see cref="VariavelService"/> consulta para não abrir um campo pedindo
    /// "valor para cursor" quando a macro usa a forma por extenso.
    /// </summary>
    public const string NomeReservado = "cursor";

    public static bool Tem(string? conteudo) =>
        !string.IsNullOrEmpty(conteudo) && _marcador.IsMatch(conteudo);

    /// <summary>Tira os marcadores sem calcular posição — para prévias e para o log de uso.</summary>
    public static string Remover(string? conteudo) =>
        string.IsNullOrEmpty(conteudo) ? string.Empty : _marcador.Replace(conteudo, string.Empty);

    /// <summary>
    /// Separa o texto que vai ser inserido de quantas vezes o cursor precisa voltar depois.
    /// </summary>
    /// <returns>
    /// O texto sem marcador nenhum, e quantas setas para a esquerda levam o cursor do fim do
    /// texto até o ponto marcado. Zero quando não há marcador.
    /// </returns>
    public static (string Texto, int Deslocamento) Extrair(string? conteudo)
    {
        if (string.IsNullOrEmpty(conteudo)) return (string.Empty, 0);

        var primeiro = _marcador.Match(conteudo);
        if (!primeiro.Success) return (conteudo, 0);

        // O texto que sobra ANTES do marcador, já limpo de outros marcadores que estivessem
        // ali — é ele que dá a posição do cursor no texto final.
        var antes  = Remover(conteudo[..primeiro.Index]);
        var limpo  = Remover(conteudo);

        // Conta só o que vira posição de cursor no app de destino. O \r do CRLF não vira: o
        // par inteiro é UMA quebra de linha lá, e uma seta para a esquerda a atravessa de uma
        // vez. Contar os dois faria o cursor parar um caractere adiante por linha do texto.
        var depois = limpo[antes.Length..];
        var deslocamento = depois.Count(c => c != '\r');

        return (limpo, deslocamento);
    }
}
