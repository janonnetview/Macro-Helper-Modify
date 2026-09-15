using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace MacroHelper.Tests;

internal sealed record SkaldMedida(string Elemento, Rect Original, Rect Visivel, bool Recortado);

internal sealed record SkaldMapa(string Id, Rect Hospedeiro, Rect Candidato,
    IReadOnlyList<SkaldMedida> Elementos, IReadOnlyList<Rect> Obstaculos)
{
    public const double Corpo = 64;
    public const double Folga = 8;
    public const double PercursoMinimo = 48;
    public double MaximoEstatico => Math.Max(0, Math.Min(Candidato.Width, Candidato.Height) - 2 * Folga);
    public double MaximoComPercurso => Math.Max(0, Math.Min(Candidato.Width - PercursoMinimo, Candidato.Height) - 2 * Folga);
    public bool Cabe => MaximoComPercurso >= Corpo;
    public Rect Inicio => new(Candidato.Left + Folga, Candidato.Bottom - Folga - Corpo, Corpo, Corpo);
    public Rect Pausa => new(Inicio.X + Math.Min(120, Candidato.Width - Corpo - 2 * Folga), Inicio.Y, Corpo, Corpo);

    public bool Permite(Rect corpo)
    {
        var envelope = corpo;
        envelope.Inflate(Folga, Folga);
        return Candidato.Contains(envelope) && !Obstaculos.Any(o => IntersecaoPositiva(o, envelope));
    }

    public static bool IntersecaoPositiva(Rect a, Rect b)
    {
        var intersecao = Rect.Intersect(a, b);
        return !intersecao.IsEmpty && intersecao.Width > 0.01 && intersecao.Height > 0.01;
    }
}

internal static class SkaldMedidor
{
    public static ScrollViewer RolagemDaPagina(FrameworkElement vista) => Descendentes(vista)
        .OfType<ScrollViewer>().First(s => s.IsVisible && s.Content is StackPanel or ItemsControl);

    public static IEnumerable<FrameworkElement> Descendentes(DependencyObject pai)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(pai); i++)
        {
            var filho = VisualTreeHelper.GetChild(pai, i);
            if (filho is FrameworkElement elemento) yield return elemento;
            foreach (var neto in Descendentes(filho)) yield return neto;
        }
    }

    public static Rect Original(FrameworkElement elemento, Visual raiz) =>
        elemento.TransformToVisual(raiz).TransformBounds(new Rect(elemento.RenderSize));

    public static Rect Visivel(FrameworkElement elemento, FrameworkElement raiz)
    {
        if (!elemento.IsVisible || elemento.Opacity <= 0 || elemento.ActualWidth <= 0 || elemento.ActualHeight <= 0) return Rect.Empty;
        var area = Rect.Intersect(Original(elemento, raiz), new Rect(raiz.RenderSize));
        for (DependencyObject? atual = elemento; atual is Visual visual; atual = VisualTreeHelper.GetParent(atual))
        {
            if (visual is UIElement ui)
            {
                if (ui.Visibility != Visibility.Visible || ui.Opacity <= 0) return Rect.Empty;
                if (ui.ClipToBounds) area.Intersect(visual.TransformToVisual(raiz).TransformBounds(new Rect(ui.RenderSize)));
                var clip = VisualTreeHelper.GetClip(visual);
                if (clip != null) area.Intersect(visual.TransformToVisual(raiz).TransformBounds(clip.Bounds));
            }
            if (ReferenceEquals(atual, raiz)) break;
        }
        return area;
    }

    public static bool TemEstilo(FrameworkElement elemento, FrameworkElement tela, string chave) =>
        ReferenceEquals(elemento.Style, tela.TryFindResource(chave));

    public static SkaldMapa Medir(string tela, FrameworkElement vista, FrameworkElement raiz)
    {
        var elementos = Descendentes(vista).ToArray();
        var scroll = RolagemDaPagina(vista);
        var viewport = Descendentes(scroll).OfType<ScrollContentPresenter>().First();
        var limite = Visivel(viewport, raiz);
        var medidas = new List<SkaldMedida>();
        void Registrar(string nome, FrameworkElement elemento)
        {
            var original = Original(elemento, raiz);
            var visivel = Visivel(elemento, raiz);
            var recortado = visivel.IsEmpty || Math.Abs(original.X - visivel.X) > 0.01 ||
                Math.Abs(original.Y - visivel.Y) > 0.01 || Math.Abs(original.Width - visivel.Width) > 0.01 ||
                Math.Abs(original.Height - visivel.Height) > 0.01;
            medidas.Add(new(nome, original, visivel, recortado));
        }
        Registrar("CurrentView", vista);
        Registrar("ScrollContentPresenter", viewport);
        var obstaculos = new List<Rect>();
        foreach (var elemento in elementos.Where(e => e.IsVisible &&
                     (e is ButtonBase or TextBoxBase or PasswordBox or Selector or ScrollBar || e.InputBindings.Count > 0 ||
                      e is TextBlock texto && !string.IsNullOrWhiteSpace(new TextRange(texto.ContentStart, texto.ContentEnd).Text) ||
                      TemEstilo(e, vista, "ListRow") || TemEstilo(e, vista, "EmptyStatePanel"))))
        {
            var area = Visivel(elemento, raiz);
            if (area.IsEmpty || area.Width <= 0 || area.Height <= 0) continue;
            obstaculos.Add(area);
            Registrar($"obstaculo-{medidas.Count:000}-{elemento.GetType().Name}-{elemento.Name}", elemento);
        }

        Rect candidato;
        string id;
        if (tela == "Configuracoes")
        {
            var coluna = (FrameworkElement)scroll.Content;
            Registrar("StackPanel-central-MaxWidth760", coluna);
            var borda = Original(coluna, raiz);
            candidato = Retangulo(limite.Left, limite.Top, borda.Left, limite.Bottom);
            id = "AN-CONFIG-LEFT";
        }
        else if (tela is "Notas" or "Categorias")
        {
            var lista = elementos.OfType<ItemsControl>().Single(e =>
                BindingOperations.GetBinding(e, ItemsControl.ItemsSourceProperty)?.Path.Path == tela);
            Registrar($"ItemsControl-{tela}-hierarquia-completa", lista);
            // ItemsControl pode esticar até o fundo do viewport; a última linha gerada é
            // que delimita o conteúdo real. Em Categorias ela inclui todas as subcategorias.
            var fim = Original(lista, raiz).Top;
            if (lista.Items.Count > 0)
            {
                var ultimaLinha = lista.ItemContainerGenerator.ContainerFromIndex(lista.Items.Count - 1) as FrameworkElement
                    ?? throw new InvalidOperationException("Última linha não materializada: não é seguro aprovar a área inferior.");
                Registrar("ultimo-container-inclui-subcategorias", ultimaLinha);
                fim = Original(ultimaLinha, raiz).Bottom;
            }
            fim += Math.Max(28, lista.Margin.Bottom);
            foreach (var vazio in elementos.Where(e => e.IsVisible && TemEstilo(e, vista, "EmptyStatePanel")))
                fim = Math.Max(fim, Original(vazio, raiz).Bottom + vazio.Margin.Bottom);
            candidato = Retangulo(limite.Left, Math.Max(limite.Top, fim), limite.Right, limite.Bottom);
            id = tela == "Notas" ? "AN-NOTES-LOWER" : "AN-CATEGORIES-LOWER";
        }
        else
        {
            var painel = elementos.OfType<Border>().First(e => TemEstilo(e, vista, "SectionPanel"));
            Registrar("Inicio-painel-cumprimento", painel);
            var local = Visivel(painel, raiz);
            if (!local.IsEmpty) local.Inflate(-8, -8);
            candidato = MaiorBolsao(local, obstaculos);
            id = "AN-HOME-LOCAL";
        }
        // O painel hospedeiro tem cantos arredondados. Mantém uma reserva interna adicional.
        var interior = limite;
        interior.Inflate(-12, -12);
        candidato.Intersect(interior);
        if (candidato.IsEmpty) candidato = new Rect(limite.Left, limite.Top, 0, 0);
        return new(id, Visivel(vista, raiz), candidato, medidas, obstaculos);
    }

    private static Rect Retangulo(double x, double y, double direita, double baseY) =>
        new(x, y, Math.Max(0, direita - x), Math.Max(0, baseY - y));

    // Só busca dentro de um painel optante. Não transforma todo espaço vazio em permissão.
    private static Rect MaiorBolsao(Rect local, IReadOnlyList<Rect> obstaculos)
    {
        if (local.IsEmpty) return new Rect();
        var bloqueios = obstaculos.Select(o => Rect.Intersect(local, o)).Where(o => !o.IsEmpty).ToArray();
        var ys = bloqueios.SelectMany(o => new[] { o.Top, o.Bottom }).Append(local.Top).Append(local.Bottom).Distinct().Order().ToArray();
        var melhor = new Rect(local.Location, new Size());
        for (var a = 0; a < ys.Length; a++)
        for (var b = a + 1; b < ys.Length; b++)
        {
            var intervalos = new List<(double Inicio, double Fim)> { (local.Left, local.Right) };
            foreach (var o in bloqueios.Where(o => o.Top < ys[b] && o.Bottom > ys[a]))
                intervalos = intervalos.SelectMany(i => new[] { (i.Inicio, Math.Min(i.Fim, o.Left)), (Math.Max(i.Inicio, o.Right), i.Fim) })
                    .Where(i => i.Item2 > i.Item1).ToList();
            foreach (var i in intervalos)
            {
                var r = new Rect(i.Inicio, ys[a], i.Fim - i.Inicio, ys[b] - ys[a]);
                if (Math.Min(r.Width, r.Height) > Math.Min(melhor.Width, melhor.Height)) melhor = r;
            }
        }
        return melhor;
    }
}

internal sealed class SkaldCamadaDiagnostica : FrameworkElement
{
    public SkaldMapa? Mapa { get; set; }
    public Rect? Corpo { get; private set; }
    public string Fase { get; private set; } = "oculto";
    public int Versao { get; private set; }
    public SkaldCamadaDiagnostica()
    {
        IsHitTestVisible = false;
        Focusable = false;
        IsEnabled = false;
    }

    public void Mostrar(Rect corpo, string fase)
    {
        if (Mapa == null || !Mapa.Permite(corpo)) throw new InvalidOperationException("Envelope fora da área autorizada.");
        Corpo = corpo;
        Fase = fase;
        InvalidateVisual();
    }

    public void Retirar(string motivo)
    {
        Corpo = null;
        Fase = motivo;
        Versao++;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Mapa == null) return;
        dc.PushClip(new RectangleGeometry(Mapa.Hospedeiro));
        var vermelho = new Pen(new SolidColorBrush(Color.FromArgb(100, 240, 80, 100)), 0.7);
        foreach (var o in Mapa.Obstaculos) dc.DrawRectangle(null, vermelho, o);
        dc.DrawRectangle(null, new Pen(Brushes.DeepSkyBlue, 2), Mapa.Candidato);
        if (Mapa.Cabe)
        {
            var inicio = Mapa.Inicio;
            var pausa = Mapa.Pausa;
            dc.DrawLine(new Pen(Brushes.Goldenrod, 2), new Point(inicio.X + 32, inicio.Y + 64), new Point(pausa.X + 32, pausa.Y + 64));
        }
        if (Corpo is Rect corpo)
        {
            var folga = corpo;
            folga.Inflate(SkaldMapa.Folga, SkaldMapa.Folga);
            dc.DrawRectangle(null, new Pen(Brushes.Goldenrod, 1) { DashStyle = DashStyles.Dash }, folga);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(75, 0, 210, 220)), new Pen(Brushes.DarkCyan, 2), corpo);
            dc.DrawText(new FormattedText("TESTE\n64 DIP", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 12, Brushes.Teal, 1), new Point(corpo.X + 7, corpo.Y + 12));
        }
        dc.Pop();
    }
}
