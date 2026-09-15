using MacroHelper.Core.Entities;
using MacroHelper.UI.ViewModels;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using UserControl = System.Windows.Controls.UserControl;

namespace MacroHelper.UI.Views;

public partial class MacrosView : UserControl
{
    private ScrollViewer? _conteudoScroll;

    public MacrosView()
    {
        InitializeComponent();
    }

    private void Overlay_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is MacrosViewModel vm)
            vm.MostrarFormulario = false;
    }

    private void PainelFormulario_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        PainelBuscaSubstituicao.Visibility = Visibility.Collapsed;
        TxtBuscar.Text = string.Empty;
        TxtSubstituir.Text = string.Empty;
        Dispatcher.BeginInvoke(AtualizarNumerosLinha, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // ── Numeração de linha ──────────────────────────────────────
    private void TxtConteudo_Loaded(object sender, RoutedEventArgs e)
    {
        _conteudoScroll = EncontrarScrollViewer(TxtConteudo);
        if (_conteudoScroll != null) _conteudoScroll.ScrollChanged += (_, _) => AtualizarNumerosLinha();
        AtualizarNumerosLinha();
    }

    private void TxtConteudo_TextChanged(object sender, TextChangedEventArgs e)
    {
        AtualizarNumerosLinha();
        AtualizarContagemBusca();
    }

    private void TxtConteudo_SizeChanged(object sender, SizeChangedEventArgs e) => AtualizarNumerosLinha();

    private static ScrollViewer? EncontrarScrollViewer(DependencyObject raiz)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var filho = VisualTreeHelper.GetChild(raiz, i);
            if (filho is ScrollViewer sv) return sv;
            var encontrado = EncontrarScrollViewer(filho);
            if (encontrado != null) return encontrado;
        }
        return null;
    }

    private void AtualizarNumerosLinha()
    {
        if (CanvasNumerosLinha == null || TxtConteudo == null) return;
        if (!TxtConteudo.IsLoaded || TxtConteudo.ActualHeight <= 0) return; // ainda não foi medido/renderizado

        CanvasNumerosLinha.Children.Clear();

        var texto = TxtConteudo.Text ?? string.Empty;
        var linhas = texto.Split('\n');
        var corTexto = (Brush)FindResource("TextMutedBrush");
        var charIndex = 0;

        for (var i = 0; i < linhas.Length; i++)
        {
            Rect rect;
            try { rect = TxtConteudo.GetRectFromCharacterIndex(charIndex); }
            catch { break; }

            if (!double.IsFinite(rect.Top)) continue; // layout ainda não disponível para esta linha

            var label = new TextBlock
            {
                Text = (i + 1).ToString(),
                FontFamily = TxtConteudo.FontFamily,
                FontSize = 11,
                Foreground = corTexto,
                Width = 24,
                TextAlignment = TextAlignment.Right
            };
            Canvas.SetTop(label, rect.Top);
            Canvas.SetLeft(label, 0);
            CanvasNumerosLinha.Children.Add(label);

            charIndex += linhas[i].Length + 1;
        }
    }

    // ── Buscar e substituir ──────────────────────────────────────
    private void BtnBuscarSubstituir_Click(object sender, RoutedEventArgs e)
    {
        var abrir = PainelBuscaSubstituicao.Visibility != Visibility.Visible;
        PainelBuscaSubstituicao.Visibility = abrir ? Visibility.Visible : Visibility.Collapsed;
        if (abrir) { AtualizarContagemBusca(); TxtBuscar.Focus(); }
    }

    private void BtnFecharBusca_Click(object sender, RoutedEventArgs e)
        => PainelBuscaSubstituicao.Visibility = Visibility.Collapsed;

    private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e) => AtualizarContagemBusca();

    private void AtualizarContagemBusca()
    {
        if (TxtContagemBusca == null) return;
        var termo = TxtBuscar.Text;
        if (string.IsNullOrEmpty(termo)) { TxtContagemBusca.Text = string.Empty; return; }

        var texto = TxtConteudo.Text ?? string.Empty;
        var total = 0; var idx = 0;
        while ((idx = texto.IndexOf(termo, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            total++; idx += termo.Length;
        }
        TxtContagemBusca.Text = $"{total} ocorrência(s)";
    }

    private void BtnBuscarProximo_Click(object sender, RoutedEventArgs e) => Buscar(paraFrente: true);
    private void BtnBuscarAnterior_Click(object sender, RoutedEventArgs e) => Buscar(paraFrente: false);

    private void Buscar(bool paraFrente)
    {
        var termo = TxtBuscar.Text;
        if (string.IsNullOrEmpty(termo)) return;
        var texto = TxtConteudo.Text ?? string.Empty;
        if (texto.Length == 0) return;

        int idx;
        if (paraFrente)
        {
            var pos = Math.Min(TxtConteudo.SelectionStart + TxtConteudo.SelectionLength, texto.Length);
            idx = texto.IndexOf(termo, pos, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) idx = texto.IndexOf(termo, 0, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            var pos = Math.Min(TxtConteudo.SelectionStart, texto.Length);
            idx = texto.Substring(0, pos).LastIndexOf(termo, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) idx = texto.LastIndexOf(termo, StringComparison.OrdinalIgnoreCase);
        }

        if (idx >= 0)
        {
            TxtConteudo.Focus();
            TxtConteudo.Select(idx, termo.Length);
        }
    }

    private void BtnSubstituir_Click(object sender, RoutedEventArgs e)
    {
        var termo = TxtBuscar.Text;
        if (string.IsNullOrEmpty(termo)) return;

        if (TxtConteudo.SelectionLength > 0 &&
            string.Equals(TxtConteudo.SelectedText, termo, StringComparison.OrdinalIgnoreCase))
        {
            TxtConteudo.SelectedText = TxtSubstituir.Text;
        }
        Buscar(paraFrente: true);
    }

    private void BtnSubstituirTudo_Click(object sender, RoutedEventArgs e)
    {
        var termo = TxtBuscar.Text;
        if (string.IsNullOrEmpty(termo)) return;

        var substituicao = TxtSubstituir.Text.Replace("$", "$$");
        TxtConteudo.Text = Regex.Replace(TxtConteudo.Text ?? string.Empty,
            Regex.Escape(termo), substituicao, RegexOptions.IgnoreCase);
        AtualizarContagemBusca();
    }

    // ── Menu "Mais ações" da linha de macro ─────────────────────
    private void BtnMaisAcoes_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } botao)
        {
            menu.PlacementTarget = botao;
            menu.IsOpen = true;
        }
    }

    /// <summary>Arquivar e Restaurar são mutuamente exclusivos: só faz sentido oferecer
    /// o que corresponde ao estado atual da macro. Itens localizados por Tag (ver XAML).</summary>
    private void MenuMaisAcoes_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu { PlacementTarget: FrameworkElement botao } menu ||
            botao.DataContext is not Macro macro)
            return;

        MenuItem? PorTag(string tag) => menu.Items.OfType<MenuItem>()
            .FirstOrDefault(mi => (mi.Tag as string) == tag);

        if (PorTag("arquivar") is { } miArquivar)
            miArquivar.Visibility = macro.Ativo ? Visibility.Visible : Visibility.Collapsed;

        if (PorTag("desarquivar") is { } miDesarquivar)
            miDesarquivar.Visibility = macro.Ativo ? Visibility.Collapsed : Visibility.Visible;
    }

    private void MenuDuplicar_Click(object sender, RoutedEventArgs e)
        => ExecutarComandoDaLinha(sender, vm => vm.DuplicarMacroCommand);

    private void MenuEnviarEmail_Click(object sender, RoutedEventArgs e)
        => ExecutarComandoDaLinha(sender, vm => vm.EnviarPorEmailCommand);

    private void MenuArquivar_Click(object sender, RoutedEventArgs e)
        => ExecutarComandoDaLinha(sender, vm => vm.ArquivarMacroCommand);

    private void MenuDesarquivar_Click(object sender, RoutedEventArgs e)
        => ExecutarComandoDaLinha(sender, vm => vm.DesarquivarMacroCommand);

    private void MenuExcluir_Click(object sender, RoutedEventArgs e)
        => ExecutarComandoDaLinha(sender, vm => vm.ExcluirMacroCommand);

    private void ExecutarComandoDaLinha(object sender, Func<MacrosViewModel, ICommand> obterComando)
    {
        if (sender is not FrameworkElement { DataContext: Macro macro } ||
            DataContext is not MacrosViewModel vm)
            return;

        var comando = obterComando(vm);
        if (comando.CanExecute(macro)) comando.Execute(macro);
    }
}
