using MacroHelper.Core.Entities;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MacroHelper.UI.Converters;

/// <summary>Visível quando o objeto é não-nulo (qualquer referência, não só string). ConverterParameter="invert" inverte.</summary>
public class ObjectNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool ehNulo = value == null;
        bool inverter = parameter as string == "invert";
        return (inverter ? !ehNulo : ehNulo) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Visível quando o valor (string) é igual ao ConverterParameter. Ex: ConverterParameter="Admin".</summary>
public class StringEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.Equals(value as string, parameter as string, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Compara o valor (string) com o ConverterParameter e devolve bool — usado para pré-marcar RadioButtons a partir de uma propriedade string.</summary>
public class StringEqualsToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.Equals(value as string, parameter as string, StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Compara dois valores (ex: int? selecionado vs Id do item) e devolve Visibility.
/// ConverterParameter="invert" inverte o resultado (visível quando DIFERENTE).</summary>
public class EqualsToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return Visibility.Collapsed;
        bool iguais = Equals(values[0], values[1]);
        bool inverter = parameter as string == "invert";
        return (inverter ? !iguais : iguais) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Converte um hex (#RRGGBB) em SolidColorBrush. ConverterParameter="soft" devolve a mesma cor em baixa opacidade (uso como fundo de tile).</summary>
public class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hex = value as string;
        System.Windows.Media.Color color;
        try { color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(string.IsNullOrWhiteSpace(hex) ? "#A7C957" : hex); }
        catch { color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A7C957"); }
        return new SolidColorBrush(color) { Opacity = parameter as string == "soft" ? 0.16 : 1.0 };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StringNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
}

public class IntGreaterThanZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i && i > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Inverso do IntGreaterThanZeroToVisibilityConverter — usado para estados vazios (mostra quando a contagem é 0).</summary>
public class IntZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i && i == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}


/// <summary>Qualquer objeto não-nulo → true; null → false. Útil para IsEnabled.</summary>
public class NullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value != null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Nome completo -> iniciais (até 2 letras), usado no avatar circular do usuário.</summary>
public class NameInitialsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var nome = (value as string)?.Trim();
        if (string.IsNullOrEmpty(nome)) return "?";
        var partes = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return partes.Length switch
        {
            0 => "?",
            1 => partes[0][..Math.Min(2, partes[0].Length)].ToUpperInvariant(),
            _ => $"{partes[0][0]}{partes[^1][0]}".ToUpperInvariant()
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Data já passada -> true. Usado no Início para pintar de vermelho o prazo de uma tarefa
/// atrasada: um <c>DataTrigger</c> não consegue chamar <c>Tarefa.Atrasada(agora)</c>, que
/// recebe o relógio por parâmetro justamente para ser testável.
///
/// Compara por DIA, não por instante — é a mesma regra de <c>Tarefa.Atrasada</c>, e o
/// <c>DateTime.Today</c> aqui segue o que <see cref="DataAmigavelConverter"/> já faz.
/// </summary>
public class DataVencidaConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is DateTime data && data.Date < DateTime.Today;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Data em linguagem de gente: "Hoje", "Amanhã", "Ontem", "seg 25/08", "25/08/2027".
/// ConverterParameter="hora" acrescenta o horário — usado no lembrete, onde a hora é o dado
/// principal; o prazo é só o dia.
/// </summary>
public class DataAmigavelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime data) return string.Empty;

        var ptBr  = CultureInfo.GetCultureInfo("pt-BR");
        var hoje  = DateTime.Today;
        var dias  = (data.Date - hoje).Days;

        var rotulo = dias switch
        {
            0  => "Hoje",
            1  => "Amanhã",
            -1 => "Ontem",
            // Dentro da semana o dia da semana localiza melhor que o número.
            > 1 and < 7  => ptBr.DateTimeFormat.GetAbbreviatedDayName(data.DayOfWeek) + " " + data.ToString("dd/MM"),
            _ when data.Year == hoje.Year => data.ToString("dd/MM"),
            _  => data.ToString("dd/MM/yyyy"),
        };

        return parameter as string == "hora" ? $"{rotulo} às {data:HH:mm}" : rotulo;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// <c>RecorrenciaTarefa</c> → o texto que a pessoa lê ("Dias úteis").
///
/// A tradução mora na entidade, em <c>Tarefa.RecorrenciaTexto</c>, e aqui só é alcançada a
/// partir do valor solto — o que a lista de opções de um ComboBox oferece. Sem isto a lista
/// mostraria "DiasUteis", sem acento e sem espaço, do jeito que se escreve em C#.
/// </summary>
public class RecorrenciaTextoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not RecorrenciaTarefa r ? string.Empty
         : r.Texto() is { Length: > 0 } texto ? texto
         : "Não repete";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>bool → Visibility. True = Visible, False = Collapsed (ConverterParameter="invert" inverte).</summary>
public class BoolToVisConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool bv && bv;
        bool inv = parameter as string == "invert";
        return (inv ? !b : b) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
