using MacroHelper.UI.Helpers;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using RichTextBox = System.Windows.Controls.RichTextBox;
using UserControl = System.Windows.Controls.UserControl;

namespace MacroHelper.UI.Views;

public partial class NotasView : UserControl
{
    public NotasView()
    {
        InitializeComponent();

        // A caixa que a barra formata, apontada aqui e não por {Binding ElementName} no XAML: a
        // barra vem ANTES da caixa na árvore, e nessa ordem o binding só resolveria quando a
        // tela carregasse — e se não resolvesse, os botões não fariam nada, calados.
        BarraFixa.Alvo = CaixaDeConteudo;
    }

    /// <summary>
    /// Ctrl+B, Ctrl+I e companhia dentro da caixa de conteúdo.
    ///
    /// Fica no code-behind, e não num comando do ViewModel, porque o que a marcação precisa
    /// saber é onde está o cursor e o que está selecionado — isso é da caixa de texto, e mandar
    /// a seleção para o ViewModel só para ela voltar seria dar a volta no quarteirão. O texto
    /// vai para a nota sozinho: o Text da caixa está ligado com
    /// UpdateSourceTrigger=PropertyChanged.
    /// </summary>
    private void AtalhoDeMarcacao(object sender, KeyEventArgs e)
    {
        if (sender is not RichTextBox caixa) return;

        // Link pede endereço, e quem pergunta é a barra.
        if (e.Key == Key.K && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            BarraFixa.PedirEndereco();
            e.Handled = true;
            return;
        }

        if (MarcacaoDeTexto.Atalho(caixa, e)) e.Handled = true;
    }
}
