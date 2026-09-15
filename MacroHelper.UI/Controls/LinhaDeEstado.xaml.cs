using System.Windows;
// WinForms também tem UserControl; aqui é sempre o do WPF.
using TextBlock = System.Windows.Controls.TextBlock;
using UserControl = System.Windows.Controls.UserControl;

namespace MacroHelper.UI.Controls;

/// <summary>Como a frase do rodapé deve ser lida.</summary>
public enum TomDoEstado
{
    /// <summary>Nada aconteceu de errado: "Salva às 14:32". Cinza, sem ícone.</summary>
    Normal,

    /// <summary>Gravou, mas alguma coisa do que foi digitado ficou de fora. Âmbar.</summary>
    Aviso,

    /// <summary>Não gravou. Vermelho — é o que a pessoa precisa ler antes de fechar.</summary>
    Erro,
}

/// <summary>
/// A frase de estado do rodapé, com três tons — ver o comentário do XAML.
///
/// A API é de método, e não uma propriedade de texto, de propósito: <c>Falhar</c> e
/// <c>Avisar</c> obrigam quem escreve o código a decidir se aquilo é confirmação, ressalva ou
/// recusa, em vez de mandar texto e deixar a cor por conta do acaso. Foi assim que "não gravei"
/// chegou a aparecer no mesmo cinza de "salva às 14:32".
/// </summary>
public partial class LinhaDeEstado : UserControl
{
    public LinhaDeEstado() => InitializeComponent();

    /// <summary>O que está escrito agora. Existe para os testes lerem o rodapé.</summary>
    public string Texto => Rotulo.Text;

    /// <summary>Como o que está escrito deve ser lido.</summary>
    public TomDoEstado Tom { get; private set; } = TomDoEstado.Normal;

    /// <summary>Estado comum: "Salva às 14:32", "Salva sozinha ao fechar".</summary>
    public void Mostrar(string texto) => Escrever(texto, TomDoEstado.Normal);

    /// <summary>Gravou, mas com ressalva: um campo que não deu para entender ficou de fora.</summary>
    public void Avisar(string texto) => Escrever(texto, TomDoEstado.Aviso);

    /// <summary>Recusa: nada foi gravado, e o motivo está aqui.</summary>
    public void Falhar(string texto) => Escrever(texto, TomDoEstado.Erro);

    /// <summary>Apaga a linha — nada escrito, e de volta ao tom normal.</summary>
    public void Limpar() => Mostrar(string.Empty);

    private void Escrever(string texto, TomDoEstado tom)
    {
        Tom         = tom;
        Rotulo.Text = texto;

        // O glifo some no tom normal: um ícone permanente vira decoração e para de ser sinal.
        Glifo.Visibility = tom == TomDoEstado.Normal ? Visibility.Collapsed : Visibility.Visible;
        // E783 é o círculo de erro; E7BA, o triângulo de atenção (Segoe MDL2).
        Glifo.Text       = tom == TomDoEstado.Erro ? "\uE783" : "\uE7BA";

        var cor = tom switch
        {
            TomDoEstado.Erro  => "DangerBrush",
            TomDoEstado.Aviso => "WarningBrush",
            _                 => "TextMutedBrush",
        };

        Rotulo.SetResourceReference(TextBlock.ForegroundProperty, cor);
        Glifo.SetResourceReference(TextBlock.ForegroundProperty, cor);
    }
}
