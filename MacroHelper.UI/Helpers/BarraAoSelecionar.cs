using MacroHelper.UI.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
// WinForms traz os mesmos nomes do WPF; aqui é sempre WPF.
using RichTextBox = System.Windows.Controls.RichTextBox;
using Size = System.Windows.Size;

namespace MacroHelper.UI.Helpers;

/// <summary>
/// A barra de formatação que aparece EM CIMA do que foi selecionado, como no Word.
///
/// Ligada com uma linha na caixa de texto:
/// <code>h:BarraAoSelecionar.Ligada="True"</code>
///
/// É propriedade anexada pelo motivo de sempre neste app: o que ela precisa montar — um Popup
/// com um controle dentro, ligado a meia dúzia de eventos da caixa — nenhum Binding produz. E
/// ser anexada é o que permite ligar a mesma barra no editor da tela de Notas e na janela
/// flutuante sem que uma saiba da outra.
///
/// A barra fixa continua existindo nos dois lugares: ela é a que ensina que a marcação existe.
/// Esta aqui é a que evita a viagem até o topo da tela quando já se sabe o que se quer.
/// </summary>
public static class BarraAoSelecionar
{
    /// <summary>Distância entre a barra e a linha selecionada.</summary>
    private const double Folga = 8;

    public static readonly DependencyProperty LigadaProperty =
        DependencyProperty.RegisterAttached(
            "Ligada", typeof(bool), typeof(BarraAoSelecionar),
            new PropertyMetadata(false, AoLigar));

    public static void SetLigada(DependencyObject alvo, bool valor) => alvo.SetValue(LigadaProperty, valor);
    public static bool GetLigada(DependencyObject alvo) => (bool)alvo.GetValue(LigadaProperty);

    private static void AoLigar(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RichTextBox caixa || e.NewValue is not true) return;

        _ = new BarraFlutuante(caixa);
    }

    /// <summary>
    /// O estado de UMA barra: o popup, a caixa dona dele e as assinaturas de evento.
    ///
    /// É uma classe, e não um punhado de handlers estáticos com dicionário, porque cada caixa
    /// tem a sua barra e a sua posição. O objeto fica vivo pelas assinaturas que faz na caixa,
    /// e morre junto com ela.
    /// </summary>
    private sealed class BarraFlutuante
    {
        private readonly RichTextBox      _caixa;
        private readonly Popup            _popup;
        private readonly Border           _moldura;
        private readonly BarraDeMarcacao  _barra;

        private Size _tamanho;

        public BarraFlutuante(RichTextBox caixa)
        {
            _caixa = caixa;

            _barra = new BarraDeMarcacao { Alvo = caixa };

            _moldura = new Border
            {
                Child           = _barra,
                Padding         = new Thickness(4, 3, 4, 3),
                BorderThickness = new Thickness(1),
            };

            // Recursos por referência: a barra vive fora da árvore da tela (Popup é outra
            // janela), e resolver o pincel agora a deixaria presa ao tema do momento.
            _moldura.SetResourceReference(Border.BackgroundProperty,  "PopupBgBrush");
            _moldura.SetResourceReference(Border.BorderBrushProperty, "BorderStrongBrush");
            _moldura.SetResourceReference(UIElement.EffectProperty, "ShadowGlass");

            // O raio vem da escala do app (RadiusMd, o dos botões e inputs) e não de um número
            // inventado aqui: é a régua do sistema de design.
            _moldura.SetResourceReference(Border.CornerRadiusProperty, "RadiusMd");

            _popup = new Popup
            {
                Child           = _moldura,
                PlacementTarget = caixa,
                Placement       = PlacementMode.Relative,
                AllowsTransparency = true,
                // StaysOpen: quem abre e fecha esta barra é a seleção, não o clique fora. Com
                // False, o próprio clique num botão dela a fecharia antes de o botão agir.
                StaysOpen       = true,
                PopupAnimation  = PopupAnimation.Fade,
            };

            caixa.SelectionChanged        += (_, _) => Atualizar();
            caixa.LostKeyboardFocus       += AoPerderFoco;
            caixa.PreviewMouseLeftButtonUp += (_, _) => Atualizar();
            caixa.IsVisibleChanged        += (_, _) => { if (!caixa.IsVisible) Esconder(); };
            caixa.Unloaded                += (_, _) => Esconder();

            // A janela mexendo é o caso do buscador rápido, que se arrasta pelo cabeçalho: o
            // popup não acompanha o arrasto, e uma barra parada no meio da tela é pior do que
            // barra nenhuma. Sair da janela esconde pelo mesmo motivo.
            caixa.Loaded += (_, _) =>
            {
                if (Window.GetWindow(caixa) is not { } janela) return;

                janela.LocationChanged += (_, _) => Esconder();
                janela.Deactivated     += (_, _) => Esconder();
            };

            // O texto rolando move a seleção junto: a barra reposiciona, e some quando o que
            // estava selecionado saiu de vista.
            caixa.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => Atualizar()));
        }

        private void AoPerderFoco(object remetente, KeyboardFocusChangedEventArgs e)
        {
            // O foco indo para dentro da própria barra não é sair do texto: acontece com o
            // popup de caracteres, que é focável por ser outra janela.
            if (e.NewFocus is Visual novo && _moldura.IsAncestorOf(novo)) return;

            // Nem o foco que o clique NA BARRA leva embora. O popup é outra janela, e fechar
            // neste instante comeria o clique que estava a caminho do botão.
            if (_moldura.IsMouseOver) return;

            // E o campo do endereço do link, que é focável de propósito: fechar a barra com ele
            // aberto tiraria da tela justamente o campo que está esperando o endereço.
            if (_barra.EmUso) return;

            Esconder();
        }

        private void Esconder() => _popup.IsOpen = false;

        private void Atualizar()
        {
            if (_caixa.Selection.IsEmpty || !_caixa.IsVisible) { Esconder(); return; }

            // Enquanto o botão do mouse está apertado a seleção ainda está sendo feita, e uma
            // barra pulando atrás do cursor atrapalha justamente na hora de escolher o trecho.
            // Ela aparece no soltar, que é quando a escolha terminou — como no Word.
            if (Mouse.LeftButton == MouseButtonState.Pressed) { Esconder(); return; }

            // O retângulo do primeiro e do último caractere selecionados, em coordenadas da
            // própria caixa — que é o que o Popup relativo entende.
            var doInicio = _caixa.Selection.Start.GetCharacterRect(LogicalDirection.Forward);
            var doFim    = _caixa.Selection.End.GetCharacterRect(LogicalDirection.Backward);

            // Retângulo vazio é caractere fora da parte visível: o texto rolou e o trecho ficou
            // para trás.
            if (doInicio.IsEmpty) { Esconder(); return; }

            var tamanho = TamanhoDaBarra();

            // Centrada no trecho quando ele cabe numa linha só; centrada no começo dele quando
            // a seleção desce várias linhas — aí o "meio" não quer dizer nada.
            var meio = !doFim.IsEmpty && Math.Abs(doFim.Top - doInicio.Top) < 1
                ? (doInicio.Left + doFim.Left) / 2
                : doInicio.Left;

            var x = meio - (tamanho.Width / 2);
            var y = doInicio.Top - tamanho.Height - Folga;

            // Sem espaço em cima (primeira linha do texto), a barra vai para baixo da seleção.
            if (y < 0) y = doInicio.Bottom + Folga;

            _popup.HorizontalOffset = Math.Round(Math.Clamp(x, 0, Math.Max(0, _caixa.ActualWidth - tamanho.Width)));
            _popup.VerticalOffset   = Math.Round(y);
            _popup.IsOpen           = true;
        }

        /// <summary>
        /// Mede a barra antes de abrir. O tamanho é preciso para o posicionamento, e depois de
        /// aberta seria tarde: o popup já teria aparecido no lugar errado por um quadro.
        /// </summary>
        private Size TamanhoDaBarra()
        {
            // Medida uma vez só: o conteúdo da barra é sempre o mesmo, e medir de novo com o
            // popup já aberto mexeria no layout dele no meio do caminho.
            if (_tamanho.Width > 0) return _tamanho;

            _moldura.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return _tamanho = _moldura.DesiredSize;
        }
    }
}
