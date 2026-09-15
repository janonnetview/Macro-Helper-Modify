using MacroHelper.UI.Helpers;

namespace MacroHelper.Tests;

/// <summary>
/// O texto digitado num campo de data vira DateTime em dois lugares: no "Salvar" do formulário
/// e no calendário, que precisa saber que dia abrir marcado. Estes testes existem para que os
/// dois continuem entendendo a mesma coisa.
/// </summary>
public class DataDigitadaTests
{
    private static readonly DateTime Referencia = new(2026, 8, 24);

    [Theory]
    [InlineData("24/08/2026", 2026, 8, 24)]
    [InlineData("24082026",   2026, 8, 24)]   // como sai da máscara antes das barras
    [InlineData("4/9/2027",   2027, 9,  4)]
    [InlineData("24/08/26",   2026, 8, 24)]
    [InlineData("24/08",      2026, 8, 24)]   // sem ano assume o corrente
    [InlineData("24/08/20",   2020, 8, 24)]   // dois dígitos de ano são ano, não meio ano
    [InlineData("4/9",        2026, 9,  4)]
    [InlineData("  24/08/2026  ", 2026, 8, 24)]
    public void Interpretar_AceitaAsFormasQueAlguemDigita(string texto, int ano, int mes, int dia) =>
        Assert.Equal(new DateTime(ano, mes, dia), DataDigitada.Interpretar(texto, Referencia));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("24/")]        // ainda digitando
    [InlineData("24/24")]      // o "2424" do campo sem máscara: mês 24 não existe
    [InlineData("32/01/2026")]
    [InlineData("amanhã")]
    public void Interpretar_DevolveNadaQuandoNaoEData(string? texto) =>
        Assert.Null(DataDigitada.Interpretar(texto, Referencia));

    [Theory]
    [InlineData("12:44", 12, 44)]
    [InlineData("1244",  12, 44)]
    [InlineData("9:05",   9,  5)]
    [InlineData("17",    17,  0)]
    [InlineData("9",      9,  0)]   // um dígito só: era aqui que o salvar estourava
    public void InterpretarHora_AceitaComEsemDoisPontos(string texto, int hora, int minuto) =>
        Assert.Equal(new TimeSpan(hora, minuto, 0), DataDigitada.InterpretarHora(texto));

    /// <summary>Lembrete com data e sem hora é de manhã — ninguém marca 00:00 sem dizer.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void InterpretarHora_VaziaEAsNoveDaManha(string? texto) =>
        Assert.Equal(TimeSpan.FromHours(9), DataDigitada.InterpretarHora(texto));

    [Theory]
    [InlineData("25:00")]
    [InlineData("12:99")]
    [InlineData("tarde")]
    public void InterpretarHora_DevolveNadaQuandoNaoEHora(string texto) =>
        Assert.Null(DataDigitada.InterpretarHora(texto));
}
