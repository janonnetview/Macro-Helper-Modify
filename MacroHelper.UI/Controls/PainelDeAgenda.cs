using MacroHelper.Core.Agenda;
using System.Windows;
// WinForms traz Size e Panel com os mesmos nomes; aqui e sempre WPF.
using Panel = System.Windows.Controls.Panel;
using Size = System.Windows.Size;

namespace MacroHelper.UI.Controls;

/// <summary>
/// O painel de um dia da grade semanal: põe cada compromisso na altura da hora dele e na faixa
/// que a <see cref="GradeDeAgenda"/> reservou.
///
/// É um Panel de verdade, e não um Canvas com posições calculadas antes, porque a largura da
/// faixa depende da largura que o dia recebeu no layout — e isso só se sabe no Arrange. Num
/// Canvas seria preciso adivinhar a largura, ou recalcular tudo a cada redimensionamento da
/// janela.
/// </summary>
public class PainelDeAgenda : Panel
{
    /// <summary>O bloco que este filho desenha. Vem do ItemContainerStyle do ItemsControl.</summary>
    public static readonly DependencyProperty BlocoProperty =
        DependencyProperty.RegisterAttached(
            "Bloco", typeof(BlocoDaGrade), typeof(PainelDeAgenda),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsParentMeasure |
                FrameworkPropertyMetadataOptions.AffectsParentArrange));

    public static void SetBloco(DependencyObject alvo, BlocoDaGrade? valor) => alvo.SetValue(BlocoProperty, valor);
    public static BlocoDaGrade? GetBloco(DependencyObject alvo) => (BlocoDaGrade?)alvo.GetValue(BlocoProperty);

    public static readonly DependencyProperty PrimeiraHoraProperty =
        DependencyProperty.Register(nameof(PrimeiraHora), typeof(int), typeof(PainelDeAgenda),
            new FrameworkPropertyMetadata(7, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>A hora do topo da grade. O que começa antes dela é encostado no topo.</summary>
    public int PrimeiraHora
    {
        get => (int)GetValue(PrimeiraHoraProperty);
        set => SetValue(PrimeiraHoraProperty, value);
    }

    public static readonly DependencyProperty UltimaHoraProperty =
        DependencyProperty.Register(nameof(UltimaHora), typeof(int), typeof(PainelDeAgenda),
            new FrameworkPropertyMetadata(20, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>A hora do fim da grade. Uma grade de 8h às 20h tem UltimaHora 20.</summary>
    public int UltimaHora
    {
        get => (int)GetValue(UltimaHoraProperty);
        set => SetValue(UltimaHoraProperty, value);
    }

    public static readonly DependencyProperty AlturaDaHoraProperty =
        DependencyProperty.Register(nameof(AlturaDaHora), typeof(double), typeof(PainelDeAgenda),
            new FrameworkPropertyMetadata(60.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>
    /// Quantos pixels vale uma hora. É o mesmo número das faixas de hora do fundo e da régua da
    /// esquerda — se os três discordarem, o compromisso das 10h aparece na linha das 11h.
    /// </summary>
    public double AlturaDaHora
    {
        get => (double)GetValue(AlturaDaHoraProperty);
        set => SetValue(AlturaDaHoraProperty, value);
    }

    /// <summary>Altura mínima de um bloco, para o título de um compromisso curto ainda caber.</summary>
    private const double AlturaMinima = 24;

    private double PixelsPorMinuto => AlturaDaHora / 60.0;

    private double AlturaTotal => Math.Max(0, UltimaHora - PrimeiraHora) * AlturaDaHora;

    protected override Size MeasureOverride(Size disponivel)
    {
        var largura = double.IsInfinity(disponivel.Width) ? 0 : disponivel.Width;

        foreach (UIElement filho in InternalChildren)
        {
            var bloco = GetBloco(filho);
            var colunas = Math.Max(1, bloco?.TotalColunas ?? 1);

            filho.Measure(new Size(largura / colunas, Math.Max(AlturaMinima, Altura(bloco))));
        }

        return new Size(largura, AlturaTotal);
    }

    protected override Size ArrangeOverride(Size final)
    {
        foreach (UIElement filho in InternalChildren)
        {
            var bloco = GetBloco(filho);
            if (bloco == null)
            {
                filho.Arrange(new Rect(0, 0, 0, 0));
                continue;
            }

            var larguraDaColuna = final.Width / Math.Max(1, bloco.TotalColunas);
            var x = bloco.Coluna * larguraDaColuna;

            var y = (bloco.InicioMinutos - (PrimeiraHora * 60)) * PixelsPorMinuto;
            var altura = Math.Max(AlturaMinima, Altura(bloco));

            // Recorte contra a grade: um compromisso das 6h numa grade que começa às 8h encosta
            // no topo em vez de ser desenhado fora dela, onde ninguém o veria.
            if (y < 0)
            {
                altura += y;
                y = 0;
            }

            altura = Math.Min(altura, Math.Max(0, final.Height - y));

            filho.Arrange(new Rect(x, y, Math.Max(0, larguraDaColuna), Math.Max(0, altura)));
        }

        return new Size(final.Width, AlturaTotal);
    }

    private double Altura(BlocoDaGrade? bloco) =>
        (bloco?.DuracaoMinutos ?? 0) * PixelsPorMinuto;
}
