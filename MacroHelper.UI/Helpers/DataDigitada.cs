using System.Globalization;

namespace MacroHelper.UI.Helpers;

/// <summary>
/// O entendimento do que foi digitado num campo de data ou de hora. Mora aqui, e não dentro de
/// um ViewModel, porque o calendário do <see cref="Controls.SeletorDeData"/> precisa do mesmo
/// entendimento para saber qual dia já vem marcado ao abrir: duas leituras diferentes do mesmo
/// texto dariam um calendário abrindo num dia e um "Salvar" gravando outro.
/// </summary>
public static class DataDigitada
{
    private static readonly string[] Formatos =
        ["dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy", "dd/MM", "d/M", "ddMMyyyy", "ddMM"];

    /// <summary>
    /// Aceita as formas que alguém realmente digita: "20/08/2026", "20/8", "2008". Sem ano,
    /// assume o da <paramref name="referencia"/> — que é o que se quer em quase toda tarefa.
    /// Devolve null para o que não for data, inclusive para o texto ainda pela metade.
    /// </summary>
    public static DateTime? Interpretar(string? texto, DateTime referencia)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        texto = texto.Trim();
        var cultura = CultureInfo.GetCultureInfo("pt-BR");

        foreach (var formato in Formatos)
            if (DateTime.TryParseExact(texto, formato, cultura, DateTimeStyles.None, out var data))
                return formato.Contains('y')
                    ? data.Date
                    : new DateTime(referencia.Year, data.Month, data.Day);

        return null;
    }

    /// <summary>
    /// Hora vazia num lembrete que tem data significa "de manhã", não "meia-noite" — ninguém
    /// marca um lembrete para as 00:00 sem dizer.
    /// </summary>
    public static TimeSpan? InterpretarHora(string? texto)
    {
        texto = (texto ?? string.Empty).Trim();
        if (texto.Length == 0) return TimeSpan.FromHours(9);

        // "%H" e não "H": formato de um caractere só o .NET lê como formato *padrão*, e
        // TryParseExact lança FormatException em vez de devolver false. Era o que acontecia ao
        // salvar um lembrete com a hora escrita como "9" — ou com qualquer texto que não fosse
        // hora nenhuma.
        foreach (var formato in new[] { "HH:mm", "H:mm", "HHmm", "HH", "%H" })
            if (DateTime.TryParseExact(texto, formato, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var hora))
                return hora.TimeOfDay;

        return null;
    }
}
