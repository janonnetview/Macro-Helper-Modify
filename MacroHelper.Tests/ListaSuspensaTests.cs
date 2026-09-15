using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace MacroHelper.Tests;

/// <summary>
/// A lista suspensa (o "Repetir" da tarefa, a categoria da macro) tem um ToggleButton
/// invisível cobrindo o campo inteiro — é ele que recebe o clique, por cima do texto e da seta.
///
/// Invisível só enquanto tiver Template próprio. Sem um, ele cai no template do Windows, que no
/// hover IGNORA o Background e pinta <c>#FFBEE6FD</c> — azul claro opaco — na largura toda; com
/// a lista aberta e no clique, o mesmo. Como o botão está por cima, o que sumia era o texto do
/// campo: passar o mouse no "Repetir" apagava a opção escolhida.
/// </summary>
public class ListaSuspensaTests
{
    /// <summary>Nada no botão de clique pode pintar fundo em cima do texto — em estado nenhum.</summary>
    [Fact]
    public void BotaoDeClique_NaoPintaNadaPorCimaDoCampo()
    {
        var pintores = new List<string>();

        TelaDeTeste.Executar(() =>
        {
            var botao = BotaoDe(Montar(out _));

            foreach (var gatilho in botao.Template.Triggers)
            {
                if (gatilho is not Trigger t) continue;

                foreach (Setter s in t.Setters)
                    if (s.Property.Name.Contains("Background") || s.Property.Name.Contains("BorderBrush"))
                        pintores.Add($"{t.Property.Name}={t.Value} -> {s.Property.Name}={s.Value}");
            }
        });

        Assert.True(pintores.Count == 0,
            "o botão de clique pinta por cima do campo: " + string.Join("; ", pintores));
    }

    /// <summary>
    /// E o clique precisa continuar chegando nele. Um Template vazio DE VERDADE — sem o
    /// Border com Background="Transparent" — não seria desenhado e o mouse passaria direto
    /// para o que está atrás, deixando o campo inteiro morto.
    /// </summary>
    [Fact]
    public void OMouseAindaChegaNoBotaoDeClique()
    {
        var acertou = false;

        TelaDeTeste.Executar(() =>
        {
            var lista = Montar(out _);
            var botao = BotaoDe(lista);

            // O meio do campo: onde a pessoa clica para abrir a lista.
            var alvo = VisualTreeHelper.HitTest(lista, new Point(lista.ActualWidth / 2, lista.ActualHeight / 2));

            for (var no = alvo?.VisualHit; no is not null; no = VisualTreeHelper.GetParent(no))
                if (ReferenceEquals(no, botao)) { acertou = true; break; }
        });

        Assert.True(acertou, "o clique no meio do campo não chega no botão que abre a lista");
    }

    /// <summary>
    /// E continua ligado ao IsDropDownOpen. Abrir de verdade aqui não dá — fora de uma janela
    /// o ComboBox força IsDropDownOpen de volta para false —, então o que se garante é a
    /// ligação: sem ela o botão vira um retângulo transparente que não faz nada.
    /// </summary>
    [Fact]
    public void BotaoDeClique_ContinuaLigadoAoAbrirDaLista()
    {
        string? caminho = null;

        TelaDeTeste.Executar(() =>
        {
            var ligacao = BindingOperations.GetBinding(BotaoDe(Montar(out _)), ToggleButton.IsCheckedProperty);
            caminho = ligacao?.Path.Path;
        });

        Assert.Equal("IsDropDownOpen", caminho);
    }

    private static ComboBox Montar(out Border pai)
    {
        var lista = new ComboBox
        {
            Style  = (Style)Application.Current.Resources[typeof(ComboBox)],
            Width  = 200,
            Height = 32,
            Items  = { new ComboBoxItem { Content = "Não repete" }, new ComboBoxItem { Content = "Todo dia" } },
            SelectedIndex = 0,
        };

        pai = new Border { Child = lista };
        pai.Measure(new Size(400, 400));
        pai.Arrange(new Rect(0, 0, 400, 400));
        pai.UpdateLayout();

        return lista;
    }

    private static ToggleButton BotaoDe(DependencyObject raiz) =>
        Procurar(raiz) ?? throw new InvalidOperationException(
            "o template da lista suspensa não tem mais o ToggleButton que recebe o clique");

    private static ToggleButton? Procurar(DependencyObject raiz)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var filho = VisualTreeHelper.GetChild(raiz, i);
            if (filho is ToggleButton achado) return achado;

            var fundo = Procurar(filho);
            if (fundo is not null) return fundo;
        }

        return null;
    }
}
