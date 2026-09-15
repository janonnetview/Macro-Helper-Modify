using CommunityToolkit.Mvvm.Input;
using MacroHelper.Core.Entities;
using MacroHelper.Core.Texto;
using MacroHelper.Services;
using MacroHelper.UI.Helpers;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace MacroHelper.UI.Views;

/// <summary>
/// A nota aberta, numa janela própria ao lado da busca rápida.
///
/// Ela não substitui a lista: as duas ficam na tela ao mesmo tempo, e andar pela lista troca
/// a nota mostrada aqui. Ocupar o lugar da busca — que foi a primeira tentativa — obrigava a
/// voltar e procurar de novo a cada nota que se quisesse comparar.
/// </summary>
public partial class NotaFlutuanteWindow : Window
{
    private readonly NotaService _svc;

    private Nota?  _nota;
    private string _tituloOriginal   = string.Empty;
    private string _conteudoOriginal = string.Empty;
    private bool   _confirmandoExclusao;

    /// <summary>Pedido de mandar o texto para o programa de destino — quem sabe qual é ele é a busca rápida.</summary>
    public event Action<string>? InserirSolicitado;

    /// <summary>Disparado depois de gravar, para a lista da busca rápida se atualizar.</summary>
    public event Action? NotaGravada;

    public NotaFlutuanteWindow(NotaService svc)
    {
        InitializeComponent();

        // Sem sombra nem fio do sistema: aqui o limite da janela é a borda do cartão.
        MolduraNativa.Aplicar(this, comSombra: false);

        _svc = svc;

        // A caixa que a barra formata, apontada aqui e não por {Binding ElementName} no XAML: a
        // barra vem ANTES da caixa na árvore, e nessa ordem o binding só resolveria quando a
        // janela carregasse — e se não resolvesse, os botões não fariam nada, calados.
        Barra.Alvo = TxtConteudo;

        // A caixa de marcar da prévia escreve na NOTA, e não num estado de tela à parte: marcar
        // um item é editar o texto dela, e por isso o que sai daqui é o mesmo Text que a
        // gravação lê. O painel é redesenhado em seguida, com a marca já no lugar.
        MarkdownVisual.SetAoMarcar(PainelDaPrevia, new RelayCommand<int>(linha =>
        {
            Conteudo = Markdown.AlternarTarefa(Conteudo, linha);
            DesenharPrevia();
        }));

        // Gravar assim que o foco sai, e não só ao fechar: entre clicar na lista e clicar em
        // outro programa não há diferença do ponto de vista desta janela.
        Deactivated += (_, _) => GravarSeMudou();
    }

    /// <summary>A nota que está aberta, ou null com a janela escondida.</summary>
    public Nota? NotaAtual => _nota;

    /// <summary>
    /// Mostra a nota. Chamada de novo com outra nota, grava a anterior e troca o conteúdo —
    /// sem reativar a janela, senão andar de seta na lista roubaria o foco a cada tecla.
    /// </summary>
    public void Abrir(Nota nota)
    {
        if (_nota != null && _nota.Id != nota.Id) GravarSeMudou();

        _nota           = nota;
        _tituloOriginal = nota.Titulo;

        TxtTitulo.Text = nota.Titulo;
        Conteudo       = nota.Conteudo;

        // O original é lido DE VOLTA do documento, e não copiado da nota: a ida e a volta podem
        // escrever a mesma nota com a marcação arrumada (um "*item*" que vira "- item"), e
        // comparar com o texto de antes faria toda nota aberta parecer editada — e ser gravada,
        // avançando a data de edição de quem só passou os olhos.
        _conteudoOriginal = Conteudo;

        Estado.Mostrar("Salva sozinha ao fechar");
        TxtConteudo.ScrollToHome();

        // Andar na lista com a prévia ligada troca o DESENHO da nota, e não só o texto por trás
        // dele — senão a janela mostraria a nota anterior até alguém clicar no olho.
        if (Vendo) DesenharPrevia();

        // Nota que ainda não existe não tem o que apagar.
        BtnApagar.Visibility = nota.Id == 0 ? Visibility.Collapsed : Visibility.Visible;
        DesarmarExclusao();

        if (IsVisible) return;

        Show();
        Activate();
        TxtConteudo.Focus();
    }

    /// <summary>
    /// Abre uma nota em branco. Grava a anterior antes — inclusive quando ela também era nova e
    /// ainda não tinha id, que é o caso em que um Ctrl+N seguido do outro perderia o texto.
    /// Sem nada escrito, nada é gravado ao fechar.
    /// </summary>
    public void Nova()
    {
        GravarSeMudou();
        Abrir(new Nota());

        // Folha em branco não tem o que visualizar: nota nova sempre abre para escrever.
        MostrarPrevia(false);

        Estado.Mostrar("Escreva a nota. O título sai da primeira linha");
        TxtConteudo.Focus();
    }

    /// <summary>Esconde gravando. Hide e não Close: a janela é reaproveitada na próxima nota.</summary>
    public void Fechar()
    {
        GravarSeMudou();
        _nota = null;
        Hide();
    }

    // ── Escrever e Ver ───────────────────────────────────────────────────────

    /// <summary>Se a janela está mostrando o desenho da marcação em vez do texto dela.</summary>
    private bool Vendo => BtnVer.IsChecked == true;

    private void BtnVer_Click(object sender, RoutedEventArgs e) => MostrarPrevia(Vendo);

    /// <summary>
    /// Troca o que ocupa a área do meio. A barra de formatação vai junto do texto: na prévia
    /// não há cursor onde inserir marcação nenhuma.
    /// </summary>
    private void MostrarPrevia(bool previa)
    {
        BtnVer.IsChecked = previa;

        if (previa) DesenharPrevia();

        Previa.Visibility      = previa ? Visibility.Visible   : Visibility.Collapsed;
        TxtConteudo.Visibility = previa ? Visibility.Collapsed : Visibility.Visible;
        Barra.Visibility       = previa ? Visibility.Collapsed : Visibility.Visible;

        BtnVer.ToolTip = previa ? "Voltar a escrever" : "Ver a nota formatada";

        if (!previa) TxtConteudo.Focus();
    }

    private void DesenharPrevia() => MarkdownVisual.SetConteudo(PainelDaPrevia, Conteudo);

    /// <summary>
    /// A nota como TEXTO, que é como ela é gravada, buscada e inserida no outro programa. Na
    /// tela ela é um documento formatado; a conversão nos dois sentidos é do
    /// <see cref="DocumentoMarkdown"/>.
    /// </summary>
    private string Conteudo
    {
        get => DocumentoMarkdown.Ler(TxtConteudo.Document);
        set => TxtConteudo.Document = DocumentoMarkdown.Montar(value);
    }

    /// <summary>
    /// Lê os campos AGORA, síncrono, e dispara a gravação. Ler depois de um await abriria
    /// espaço para a janela trocar de nota entre o "vou gravar" e o "li o que gravar".
    /// </summary>
    private void GravarSeMudou()
    {
        if (_nota == null) return;

        var titulo   = TxtTitulo.Text;
        var conteudo = Conteudo;

        // Só grava se mudou: abrir uma nota para ler não pode avançar a data de edição dela.
        if (titulo == _tituloOriginal && conteudo == _conteudoOriginal) return;

        _nota.Titulo      = titulo;
        _nota.Conteudo    = conteudo;
        _tituloOriginal   = titulo;
        _conteudoOriginal = conteudo;

        GravarAsync(_nota);
    }

    private async void GravarAsync(Nota nota)
    {
        try
        {
            var (ok, msg, _) = await _svc.SalvarAsync(nota);
            if (!ok) { Estado.Falhar(msg); return; }

            // O serviço pode deduzir o título a partir do conteúdo; o campo mostra o que
            // ficou gravado de verdade.
            if (ReferenceEquals(_nota, nota))
            {
                TxtTitulo.Text       = nota.Titulo;
                _tituloOriginal      = nota.Titulo;
                BtnApagar.Visibility = Visibility.Visible;
                Estado.Mostrar($"Salva às {DateTime.Now:HH:mm}");
            }

            NotaGravada?.Invoke();
        }
        catch (Exception ex) { App.LogErro(ex); }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // O resto do teclado é do editor: Enter quebra linha e as setas andam no texto.
        if (e.Key == Key.Escape)
        {
            // Com a exclusão armada, Esc desarma em vez de fechar: é a saída de quem clicou
            // em Apagar sem querer.
            if (_confirmandoExclusao)
            {
                DesarmarExclusao();
                Estado.Mostrar("Salva sozinha ao fechar");
            }
            else
            {
                Fechar();
                Owner?.Activate();
            }

            e.Handled = true;
        }
        // Ctrl+B, Ctrl+I e companhia. Só dentro do texto: no campo do título, que é uma linha
        // sem marcação nenhuma, negrito não quer dizer nada.
        else if (TxtConteudo.IsKeyboardFocused && e.Key == Key.K &&
                 Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            // Link pede endereço, e quem pergunta é a barra.
            Barra.PedirEndereco();
            e.Handled = true;
        }
        else if (TxtConteudo.IsKeyboardFocused && MarcacaoDeTexto.Atalho(TxtConteudo, e))
        {
            e.Handled = true;
        }
        else if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            GravarSeMudou();
            e.Handled = true;
        }
        else if (e.Key == Key.Return && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            PedirInsercao();
            e.Handled = true;
        }
    }

    private void PedirInsercao()
    {
        var conteudo = Conteudo;
        Fechar();
        InserirSolicitado?.Invoke(conteudo);
    }

    private void BtnFechar_Click(object sender, RoutedEventArgs e)
    {
        Fechar();
        Owner?.Activate();
    }

    private void BtnSalvar_Click(object sender, RoutedEventArgs e)
    {
        DesarmarExclusao();
        GravarSeMudou();
    }

    private void BtnInserir_Click(object sender, RoutedEventArgs e) => PedirInsercao();

    // ── Exclusão ─────────────────────────────────────────────────────────────

    private void DesarmarExclusao()
    {
        _confirmandoExclusao = false;
        BtnApagar.Content    = "Apagar";
    }

    private async void BtnApagar_Click(object sender, RoutedEventArgs e)
    {
        if (_nota is not { Id: > 0 }) return;

        // Dois cliques, e o botão diz o que o segundo vai fazer. Um clique só é pouco para uma
        // nota que não vai para lixeira nenhuma.
        if (!_confirmandoExclusao)
        {
            _confirmandoExclusao = true;
            BtnApagar.Content    = "Apagar mesmo?";
            Estado.Mostrar("Clique de novo para apagar.");
            return;
        }

        var alvo = _nota;

        // Zerado ANTES de esconder: o Deactivated dispara a gravação, e sem isso a nota seria
        // regravada no caminho para ser apagada.
        _nota = null;
        Hide();

        try
        {
            await _svc.ExcluirAsync(alvo.Id);
            NotaGravada?.Invoke();
        }
        catch (Exception ex) { App.LogErro(ex); }
    }
}
