using System.Globalization;
using System.Text;

namespace MacroHelper.Core;

/// <summary>
/// Reduz um texto à forma usada para busca: minúsculo e sem acentos.
///
/// É o que permite procurar "acao" e achar "Ação". O LIKE do SQLite só ignora maiúsculas
/// para ASCII — "Ç" e "ç" são caracteres distintos para ele — então a comparação precisa
/// acontecer sobre um texto já achatado dos dois lados: na coluna gravada e no termo digitado.
/// </summary>
public static class TextoNormalizado
{
    public static string Para(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;

        // FormD separa a letra do acento em dois caracteres; descartar as marcas que não
        // ocupam espaço ("^", "~", "¸") deixa a letra base. Cobre qualquer alfabeto.
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);

        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(SemAcento(c));
        }

        return sb.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Rede de segurança para as letras acentuadas do português, aplicada depois do Normalize.
    ///
    /// Não é redundância: com <c>InvariantGlobalization=true</c> — a opção que se costuma ligar
    /// para encolher o publish e dispensar o ICU — o <c>String.Normalize</c> devolve a string
    /// INTACTA, sem lançar nada. A busca por acentos simplesmente pararia de funcionar, em
    /// silêncio, e o sintoma seria "não acha a nota que eu acabei de escrever". Esta tabela
    /// torna o resultado independente do modo de globalização do runtime.
    /// </summary>
    private static char SemAcento(char c) => c switch
    {
        'á' or 'à' or 'â' or 'ã' or 'ä' or 'å' => 'a',
        'Á' or 'À' or 'Â' or 'Ã' or 'Ä' or 'Å' => 'A',
        'é' or 'è' or 'ê' or 'ë'               => 'e',
        'É' or 'È' or 'Ê' or 'Ë'               => 'E',
        'í' or 'ì' or 'î' or 'ï'               => 'i',
        'Í' or 'Ì' or 'Î' or 'Ï'               => 'I',
        'ó' or 'ò' or 'ô' or 'õ' or 'ö'        => 'o',
        'Ó' or 'Ò' or 'Ô' or 'Õ' or 'Ö'        => 'O',
        'ú' or 'ù' or 'û' or 'ü'               => 'u',
        'Ú' or 'Ù' or 'Û' or 'Ü'               => 'U',
        'ç' => 'c', 'Ç' => 'C',
        'ñ' => 'n', 'Ñ' => 'N',
        _   => c,
    };
}
