using MacroHelper.UI.Helpers;
using System.Windows;

namespace MacroHelper.UI.Views;

/// <summary>
/// Aparece uma vez, na primeira abertura.
///
/// Os ícones vêm por código, não colados como caractere: os glifos do Segoe MDL2 vivem na Área
/// de Uso Privado do Unicode, que a maioria dos editores mostra como quadrado vazio — foi assim
/// que os cinco passos ficaram com o ícone em branco sem ninguém notar. Escrever 0xE945 aqui
/// espelha o <c>&amp;#xE945;</c> do XAML e sobrevive a qualquer conversão de encoding.
///
/// São os mesmos glifos da barra lateral, para cada passo apontar para um botão que a pessoa
/// vai reconhecer depois de fechar o tour.
/// </summary>
public partial class TourWindow : Window
{
    private static string Glifo(int codigo) => char.ConvertFromUtf32(codigo);

    private readonly (string Icone, string Titulo, string Descricao)[] _passos =
    [
        (Glifo(0xE945), "Bem-vindo ao SK MacroHelper!",
            "Guarde aqui os textos que você repete (respostas, modelos, blocos de contrato) e insira qualquer um deles em qualquer programa do Windows sem copiar e colar."),

        (Glifo(0xE70F), "Gatilho por texto",
            "Digite \"/\" seguido do atalho da macro (ex.: /chamado-recebido) em qualquer campo de texto. Um popup mostra as sugestões: setas para escolher, Enter para inserir, Esc para fechar."),

        (Glifo(0xE721), "Busca rápida",
            "Pressione Ctrl+Espaço em qualquer lugar do Windows para procurar uma macro pelo título, mesmo com a janela do app fechada na bandeja."),

        (Glifo(0xE9D5), "Tarefas, Lembretes e Notas",
            "Além das macros, o app guarda tarefas com prazo, lembretes com dia e hora marcados, e notas de texto livre. O lembrete avisa ANTES: na véspera e em cima da hora, por padrão, desde que o app esteja aberto, nem que seja na bandeja."),

        (Glifo(0xE713), "Tudo funciona offline",
            "Seus dados ficam num único arquivo no seu computador: não há login, servidor nem sincronização. Em Configurações você define seu nome, o tema, o prefixo do gatilho e se o app inicia junto com o Windows."),
    ];

    private int _indice;

    public TourWindow()
    {
        InitializeComponent();

        // Sem sombra nem fio do sistema: aqui o limite da janela é a borda do cartão.
        MolduraNativa.Aplicar(this, comSombra: false);

        AtualizarPasso();
    }

    private void AtualizarPasso()
    {
        var p = _passos[_indice];
        TxtIcone.Text = p.Icone;
        TxtTitulo.Text = p.Titulo;
        TxtDescricao.Text = p.Descricao;
        TxtPasso.Text = $"{_indice + 1} de {_passos.Length}";
        BtnProximo.Content = _indice == _passos.Length - 1 ? "Concluir" : "Próximo";
    }

    private void BtnProximo_Click(object sender, RoutedEventArgs e)
    {
        if (_indice < _passos.Length - 1) { _indice++; AtualizarPasso(); }
        else Close();
    }

    private void BtnPular_Click(object sender, RoutedEventArgs e) => Close();
}
