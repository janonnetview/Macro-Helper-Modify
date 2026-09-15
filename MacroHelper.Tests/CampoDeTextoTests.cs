using MacroHelper.UI.Controls;
using System.Windows;
using System.Windows.Controls;

namespace MacroHelper.Tests;

/// <summary>
/// O recuo do <c>InputStyle</c> entra uma vez só.
///
/// O TextBox do WPF já aplica o próprio <c>Padding</c> dentro do PART_ContentHost. Quando o
/// template embrulhava o ScrollViewer num Grid com <c>Margin="{TemplateBinding Padding}"</c>, o
/// recuo entrava DUAS vezes e o campo perdia essa largura de área útil — num campo estreito o
/// texto simplesmente sumia pela direita, sem barra de rolagem e sem aviso nenhum. O seletor de
/// data ficava com 22px para uma data de 66px e mostrava "24/0".
///
/// Nada disso aparece em teste de binding: o campo continua com o Text certo, é só o desenho
/// que corta. Por isso a garantia aqui é geométrica — o que cabe na viewport.
/// </summary>
public class CampoDeTextoTests
{
    /// <summary>Uma data inteira cabe no campo de data, com folga para o ícone do calendário.</summary>
    [Fact]
    public void DataInteira_CabeNoSeletor()
    {
        double sobra = 0, largura = 0;

        TelaDeTeste.Executar(() =>
        {
            var seletor = new SeletorDeData { Texto = "24/08/2026" };
            Arranjar(seletor);

            var campo = (TextBox)seletor.FindName("Campo")!;
            largura = campo.ViewportWidth;
            sobra   = campo.ViewportWidth - campo.ExtentWidth;
        });

        Assert.True(sobra > 0, $"a data não cabe: sobram {sobra:N1}px de {largura:N1}px de campo");
    }

    /// <summary>
    /// O recuo pedido é o recuo cobrado. Um campo de 120 com Padding 12+34 tem 74px de miolo;
    /// aceitar bem menos que isso é o sintoma do recuo aplicado duas vezes.
    /// </summary>
    [Fact]
    public void Recuo_NaoEntraDuasVezes()
    {
        double util = 0;

        TelaDeTeste.Executar(() =>
        {
            var campo = new TextBox
            {
                Style   = (Style)Application.Current.Resources["InputStyle"],
                Width   = 120,
                Height  = 38,
                Padding = new Thickness(12, 0, 34, 0),
                Text    = "24/08/2026",
            };

            Arranjar(campo);
            util = campo.ViewportWidth;
        });

        // 120 − 2 de borda − 46 de recuo = 72, menos alguns pixels que o cursor reserva.
        Assert.InRange(util, 64, 72);
    }

    /// <summary>Arranjar dentro de um pai: solto, o ScrollViewer não calcula viewport nenhuma.</summary>
    private static void Arranjar(FrameworkElement campo)
    {
        var pai = new Border { Child = campo };
        pai.Measure(new Size(400, 400));
        pai.Arrange(new Rect(0, 0, 400, 400));
        pai.UpdateLayout();
    }
}
