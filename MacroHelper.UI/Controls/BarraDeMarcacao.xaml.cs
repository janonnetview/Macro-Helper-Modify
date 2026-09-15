using MacroHelper.UI.Helpers;
using System.Windows;
using System.Windows.Controls;
// WinForms traz os mesmos nomes do WPF; aqui é sempre WPF.
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using RichTextBox = System.Windows.Controls.RichTextBox;
using UserControl = System.Windows.Controls.UserControl;

namespace MacroHelper.UI.Controls;

/// <summary>
/// A barra de formatação das notas: negrito, itálico, listas, link e os caracteres que não
/// estão no teclado.
///
/// É um controle, e não XAML repetido em cada tela, porque a barra aparece em três situações
/// que precisam concordar entre si: fixa no editor da tela de Notas, fixa na janela flutuante
/// da nota, e flutuando em cima da seleção (<see cref="Helpers.BarraAoSelecionar"/>). Se cada
/// uma tivesse a sua, o botão que existe numa e não na outra viraria uma pergunta.
///
/// A barra não sabe formatar nada: quem formata é o <see cref="MarcacaoDeTexto"/>, sobre a
/// caixa apontada por <see cref="Alvo"/>.
/// </summary>
public partial class BarraDeMarcacao : UserControl
{
    /// <summary>
    /// A caixa de texto que os botões formatam.
    ///
    /// Sem ela a barra existe e não faz nada — de propósito: é melhor do que estourar no
    /// clique se alguém montar a barra antes de a caixa existir na árvore.
    /// </summary>
    public static readonly DependencyProperty AlvoProperty =
        DependencyProperty.Register(
            nameof(Alvo), typeof(RichTextBox), typeof(BarraDeMarcacao), new PropertyMetadata(null));

    public RichTextBox? Alvo
    {
        get => (RichTextBox?)GetValue(AlvoProperty);
        set => SetValue(AlvoProperty, value);
    }

    /// <summary>
    /// Se algum dos popups da barra está aberto.
    ///
    /// Quem pergunta é a barra flutuante: o campo do endereço é focável, e o foco saindo do
    /// texto para ele não pode fechar a barra — fecharia o campo junto, antes de alguém colar
    /// o endereço.
    /// </summary>
    public bool EmUso => PopupCaracteres.IsOpen || PopupDoLink.IsOpen;

    /// <summary>
    /// Os caracteres da grade, por assunto.
    ///
    /// A escolha é a das notas de trabalho deste app, e não um mapa de caracteres completo: o
    /// certo e o errado de uma lista de conferência, as setas de "virou isto", os traços e as
    /// aspas que o teclado brasileiro não tem, e os sinais que aparecem em texto técnico. Um
    /// mapa completo seria mais bonito de mostrar e pior de usar — ninguém procura o ∑ aqui.
    /// </summary>
    private static readonly (string Titulo, string[] Sinais)[] _secoes =
    [
        ("Marcas",  ["✓", "✔", "✗", "✘", "★", "☆", "⚠",
                     "●", "○", "▪", "▸", "•", "·"]),

        ("Setas",   ["→", "←", "↑", "↓", "↔", "⇒", "⇐",
                     "⇔", "↳", "➜"]),

        ("Texto",   ["—", "–", "…", "«", "»", "“", "”",
                     "‘", "’", "§", "¶", "†"]),

        ("Sinais",  ["±", "×", "÷", "≈", "≠", "≤", "≥",
                     "°", "º", "ª", "µ", "∞", "©", "®",
                     "™", "R$", "€", "£"]),
    ];

    public BarraDeMarcacao()
    {
        InitializeComponent();
        MontarCaracteres();
    }

    private void AplicarMarcacao(object sender, RoutedEventArgs e)
    {
        if (Alvo is not { } caixa) return;
        if (sender is not Button { Tag: string marcacao }) return;

        MarcacaoDeTexto.Aplicar(caixa, marcacao);
    }

    // ── Link ─────────────────────────────────────────────────────────────────

    /// <summary>Abre o campo do endereço. É o que o Ctrl+K chama, de qualquer uma das telas.</summary>
    public void PedirEndereco() => PopupDoLink.IsOpen = true;

    private void AbrirOLink(object sender, RoutedEventArgs e) => PedirEndereco();

    /// <summary>
    /// Já vem preenchido com o que estiver na área de transferência, quando for um endereço:
    /// copiar o link e depois escrever a nota é a ordem em que isso acontece na vida real.
    /// </summary>
    private void AoAbrirOLink(object? sender, EventArgs e)
    {
        CampoDoEndereco.Text = AreaDeTransferencia();
        CampoDoEndereco.SelectAll();
        CampoDoEndereco.Focus();
    }

    private static string AreaDeTransferencia()
    {
        try
        {
            var texto = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : string.Empty;

            return texto.StartsWith("http://",  StringComparison.OrdinalIgnoreCase) ||
                   texto.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? texto
                : "https://";
        }
        catch (Exception ex)
        {
            // O clipboard é compartilhado e OpenClipboard falha se outro processo o mantém
            // aberto. Não é motivo para o link deixar de funcionar.
            App.LogErro(ex);
            return "https://";
        }
    }

    private void AoTeclarNoEndereco(object sender, KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)      { AplicarOLink(sender, e); e.Handled = true; }
        else if (e.Key == System.Windows.Input.Key.Escape) { PopupDoLink.IsOpen = false; e.Handled = true; }
    }

    private void AplicarOLink(object sender, RoutedEventArgs e)
    {
        PopupDoLink.IsOpen = false;

        if (Alvo is { } caixa) MarcacaoDeTexto.Ligar(caixa, CampoDoEndereco.Text.Trim());
    }

    // ── Caracteres especiais ─────────────────────────────────────────────────

    private void MontarCaracteres()
    {
        foreach (var (titulo, sinais) in _secoes)
        {
            var rotulo = new TextBlock
            {
                Text     = titulo,
                FontSize = 10.5,
                Margin   = new Thickness(4, GradeDeCaracteres.Children.Count == 0 ? 0 : 8, 0, 4),
            };

            // Referência de recurso, e não o pincel resolvido agora: assim a grade acompanha a
            // troca de tema como o resto da tela.
            rotulo.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
            GradeDeCaracteres.Children.Add(rotulo);

            var grade = new WrapPanel();

            foreach (var sinal in sinais)
            {
                var botao = new Button
                {
                    Content = sinal,
                    Style   = (Style)FindResource("BotaoDeCaractere"),
                    ToolTip = sinal,
                };

                // O popup fecha ao inserir: quem escolheu o sinal quer voltar a escrever. Se
                // precisar de dois seguidos, abre de novo — é um clique, e o contrário (ficar
                // aberto cobrindo o texto) incomoda toda vez.
                botao.Click += (_, _) =>
                {
                    if (Alvo is { } caixa) MarcacaoDeTexto.Inserir(caixa, sinal);
                    BotaoCaracteres.IsChecked = false;
                };

                grade.Children.Add(botao);
            }

            GradeDeCaracteres.Children.Add(grade);
        }
    }
}
