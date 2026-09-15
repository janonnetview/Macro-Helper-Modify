using MacroHelper.Core.Entities;
using MacroHelper.Services;
using MacroHelper.UI.Helpers;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace MacroHelper.UI.Views;

public partial class BuscadorRapidoWindow : Window
{
    /// <summary>
    /// As abas da janela. Macros é a padrão porque é o que o Ctrl+Espaço sempre fez — as
    /// outras entram como desvio, não como concorrentes.
    /// </summary>
    private enum Aba { Macros, Tarefas, Lembretes, Notas, Clipboard }

    private readonly MacroService           _macroService;
    private readonly InsercaoDeMacroService _insercao;
    private readonly LogUsoService          _logService;
    private readonly TarefaService          _tarefaService;
    private readonly CompromissoService     _compromissoService;
    private readonly NotaService            _notaService;
    private readonly ClipboardService       _clipboardService;
    private readonly TextInsertionService   _insercaoTexto;
    private readonly IRelogio               _relogio;

    private Aba    _aba          = Aba.Macros;
    private bool   _montada;
    private IntPtr _janelaOrigem = IntPtr.Zero;

    /// <summary>Primeiro clique em "Limpar" arma; o segundo esvazia. Ver <see cref="BtnLimpar_Click"/>.</summary>
    private bool _confirmandoLimpeza;


    public BuscadorRapidoWindow(MacroService macroService,
        InsercaoDeMacroService insercao, LogUsoService logService,
        TarefaService tarefaService, CompromissoService compromissoService,
        NotaService notaService, ClipboardService clipboardService,
        TextInsertionService insercaoTexto, IRelogio relogio)
    {
        InitializeComponent();

        // Sem sombra nem fio do sistema: aqui o limite da janela é a borda do cartão.
        MolduraNativa.Aplicar(this, comSombra: false);

        _macroService     = macroService;
        _insercao         = insercao;
        _logService       = logService;
        _tarefaService    = tarefaService;
        _compromissoService = compromissoService;
        _notaService      = notaService;
        _clipboardService = clipboardService;
        _insercaoTexto    = insercaoTexto;
        _relogio          = relogio;

        Loaded      += (_, _) => { TxtBusca.Focus(); Recarregar(); };
        Deactivated += (_, _) => AoPerderFoco();
        // Restaura o idioma do sistema quando esta janela fica em foco
        Activated   += (_, _) => App.RestaurarIdiomaDoSistema();
        ListResultados.SelectionChanged += (_, _) =>
        {
            AtualizarPreview();

            // Com a nota aberta ao lado, andar na lista — de seta ou de clique — troca o que
            // ela mostra. É o pedido de "clicar em outra nota altera para ela", e de quebra
            // transforma a dupla num mestre-detalhe que se navega pelo teclado.
            if (_janelaNota is { IsVisible: true } && ListResultados.SelectedItem is Nota outraNota)
                _janelaNota.Abrir(outraNota);
            else if (_janelaTarefa is { IsVisible: true } && ListResultados.SelectedItem is Tarefa outraTarefa)
                _janelaTarefa.Abrir(outraTarefa);
            else if (_janelaLembrete is { IsVisible: true } && ListResultados.SelectedItem is Compromisso outro)
                _janelaLembrete.Abrir(outro);
        };

        _montada = true;
    }

    public void Mostrar()
    {
        // Salva a janela que estava ativa ANTES do buscador aparecer
        _janelaOrigem = GetForegroundWindow();
        TxtBusca.Text = string.Empty;
        FecharJanelasLaterais();

        // Toda abertura volta para Macros. Uma janela de atalho que reabre no estado em que
        // foi fechada obriga a olhar para a tela antes de digitar — que é justamente o que o
        // atalho existe para evitar.
        RbMacros.IsChecked = true;
        DefinirAba(Aba.Macros);
        DesarmarLimpeza();

        PosicionarNaTelaDoCursor();
        Show();
        Activate();
        TxtBusca.Focus();
    }

    // ── Abas ─────────────────────────────────────────────────────────────────

    private void Aba_Checked(object sender, RoutedEventArgs e)
    {
        // O Checked do RbMacros dispara lá dentro do InitializeComponent, antes de os campos
        // de serviço existirem. Até o construtor terminar não há nada que possa recarregar.
        if (!_montada) return;

        DefinirAba(sender == RbTarefas   ? Aba.Tarefas
                 : sender == RbLembretes ? Aba.Lembretes
                 : sender == RbNotas     ? Aba.Notas
                 : sender == RbClipboard ? Aba.Clipboard
                 :                         Aba.Macros);
    }

    private void DefinirAba(Aba aba)
    {
        _aba = aba;

        TxtPlaceholder.Text = aba switch
        {
            Aba.Tarefas   => "Buscar tarefa...",
            Aba.Lembretes => "Buscar lembrete...",
            Aba.Notas     => "Buscar nota...",
            Aba.Clipboard => "Buscar no que você copiou...",
            _             => "Buscar macro...",
        };

        // O Enter faz coisas diferentes em cada aba. O rodapé precisa dizer qual, ou o
        // primeiro Enter na aba vira surpresa.
        TxtDicaAcao.Text = aba switch
        {
            Aba.Tarefas   => "concluir  ",
            Aba.Lembretes => "marcar como feito  ",
            Aba.Notas     => "abrir  ",
            _           => "inserir  ",
        };

        // Criar só existe onde não há formulário no app: macro tem o dela, e ninguém "cria" um
        // item de área de transferência — ele aparece por ter sido copiado.
        BtnNovo.Visibility      = aba is Aba.Tarefas or Aba.Lembretes or Aba.Notas ? Visibility.Visible : Visibility.Collapsed;
        BtnLimpar.Visibility    = aba == Aba.Clipboard ? Visibility.Visible : Visibility.Collapsed;
        DicaEditar.Visibility   = aba is Aba.Tarefas or Aba.Lembretes ? Visibility.Visible : Visibility.Collapsed;
        DicaApagar.Visibility   = aba == Aba.Clipboard ? Visibility.Visible : Visibility.Collapsed;

        DesarmarLimpeza();

        // Sair da aba de Notas fecha a nota aberta: ela ficaria ao lado de uma lista que não
        // tem mais nada a ver com ela.
        if (aba != Aba.Notas)     FecharJanelaDaNota();
        if (aba != Aba.Tarefas)   FecharJanelaDaTarefa();
        if (aba != Aba.Lembretes) FecharJanelaDoLembrete();

        // O termo digitado continua valendo na aba nova: quem procurou "contrato" nas macros
        // e não achou quer ver se existe uma nota com esse nome, sem redigitar.
        Recarregar();
        TxtBusca.Focus();
    }

    /// <summary>Tab e Shift+Tab giram entre as abas — o foco continua no campo de busca.</summary>
    private void GirarAba(int passo)
    {
        var abas  = new[] { RbMacros, RbTarefas, RbLembretes, RbNotas, RbClipboard };
        var atual = Array.FindIndex(abas, r => r.IsChecked == true);
        if (atual < 0) atual = 0;

        // O resto de C# é negativo para índice negativo; o segundo módulo corrige o Shift+Tab
        // a partir da primeira aba.
        var proxima = ((atual + passo) % abas.Length + abas.Length) % abas.Length;
        abas[proxima].IsChecked = true; // dispara Aba_Checked
    }

    // ── Posicionamento ───────────────────────────────────────────────────────

    /// <summary>Centraliza a janela no monitor onde está o cursor do mouse.</summary>
    private void PosicionarNaTelaDoCursor()
    {
        var area = AreaDeTrabalhoDoCursor();
        if (area.IsEmpty) return;

        var altura = ActualHeight > 0 ? ActualHeight : Height;

        // Arredondado: numa janela com AllowsTransparency, meio pixel de deslocamento borra a
        // janela inteira, porque não há grade de pixel onde encaixar o que foi desenhado.
        Left = Math.Round(area.Left + (area.Width  - Width)  / 2);
        Top  = Math.Round(area.Top  + (area.Height - altura) / 2);
    }

    /// <summary>
    /// Área útil, em DIPs, do monitor onde está o cursor — e não a do monitor primário, que é
    /// o que WindowStartupLocation="CenterScreen" faria, ignorando em qual tela a pessoa está
    /// trabalhando num setup de vários monitores. Rect.Empty quando o Windows não responde.
    /// </summary>
    private static Rect AreaDeTrabalhoDoCursor()
    {
        if (!GetCursorPos(out var pt)) return Rect.Empty;
        var hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
        if (hMonitor == IntPtr.Zero) return Rect.Empty;

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMonitor, ref info)) return Rect.Empty;

        var dpiX = 96.0;
        var dpiY = 96.0;
        if (GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out var rawDpiX, out var rawDpiY) == 0)
        {
            dpiX = rawDpiX;
            dpiY = rawDpiY;
        }

        return new Rect(
            info.rcWork.Left * 96.0 / dpiX,
            info.rcWork.Top  * 96.0 / dpiY,
            (info.rcWork.Right  - info.rcWork.Left) * 96.0 / dpiX,
            (info.rcWork.Bottom - info.rcWork.Top)  * 96.0 / dpiY);
    }

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int  MDT_EFFECTIVE_DPI        = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    // ── Carregamento da lista ────────────────────────────────────────────────

    private CancellationTokenSource? _buscaCts;

    /// <summary>
    /// A porta de entrada dos eventos, que não podem devolver Task. Existe para que nenhum
    /// <c>async void</c> deixe exceção escapar sem log — o retry de "JWT expired" que um dia
    /// envolveu esta carga morreu com o Supabase: o banco é local e não há token a vencer.
    /// </summary>
    private async void Recarregar(bool comDebounce = false)
    {
        try { await RecarregarAsync(comDebounce); }
        catch (Exception ex) { App.LogErro(ex); }
    }

    /// <summary>
    /// Um caminho só para encher a lista, seja qual for a aba e tenha ou não termo digitado.
    /// A troca de aba, a digitação e o Enter que conclui uma tarefa terminam todos aqui — não
    /// existe um segundo lugar que também mexa em ItemsSource e possa divergir deste.
    /// </summary>
    private async Task RecarregarAsync(bool comDebounce)
    {
        LimparFalha();

        _buscaCts?.Cancel();
        var cts = new CancellationTokenSource();
        _buscaCts = cts;

        var termo = TxtBusca.Text;

        try
        {
            if (comDebounce) await Task.Delay(150, cts.Token);
            if (cts.IsCancellationRequested) return;

            var itens = _aba switch
            {
                Aba.Tarefas   => (await _tarefaService.PesquisarAsync(termo)).Cast<object>().ToList(),
                Aba.Lembretes => (await _compromissoService.PesquisarAsync(termo)).Cast<object>().ToList(),
                Aba.Notas     => (await _notaService.PesquisarAsync(termo)).Cast<object>().ToList(),
                Aba.Clipboard => (await _clipboardService.PesquisarAsync(termo)).Cast<object>().ToList(),
                _             => await BuscarMacrosAsync(termo),
            };

            if (cts.IsCancellationRequested) return;

            ListResultados.ItemsSource = itens;
            if (itens.Count > 0) ListResultados.SelectedIndex = 0;
            AtualizarMensagemVazia();
        }
        // Cancelamento é o caminho normal aqui: cada tecla nova cancela a busca anterior.
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Sem termo, a ordem é a que serve para escolher com o dedo já na seta: favoritas
    /// primeiro, depois as usadas há pouco, e só então o resto em ordem alfabética.
    /// </summary>
    private async Task<List<object>> BuscarMacrosAsync(string termo)
    {
        if (!string.IsNullOrWhiteSpace(termo))
            return (await _macroService.PesquisarAsync(termo)).Cast<object>().ToList();

        var todos        = (await _macroService.ObterTodosAsync()).ToList();
        var favoritos    = todos.Where(m => m.Favorito).ToList();
        var logsRecentes = (await _logService.ObterRecentesAsync(40)).ToList();
        var idsRecentes  = logsRecentes.Where(l => l.MacroId != null)
            .Select(l => l.MacroId!.Value).Distinct().Take(8).ToList();
        var recentes = idsRecentes
            .Select(id => todos.FirstOrDefault(m => m.Id == id))
            .Where(m => m != null && !favoritos.Any(f => f.Id == m!.Id))
            .Cast<Macro>().ToList();
        var resto = todos
            .Where(m => !favoritos.Any(f => f.Id == m.Id) && !recentes.Any(r => r.Id == m.Id))
            .OrderBy(m => m.Titulo).ToList();

        var lista = new List<object>();
        lista.AddRange(favoritos);
        lista.AddRange(recentes);
        lista.AddRange(resto);
        return lista;
    }

    /// <summary>
    /// A frase muda com a aba e com o termo. Antes era fixa e citava a busca sempre, então
    /// uma lista vazia sem ninguém ter procurado nada terminava em aspas vazias — com três
    /// abas, e uma delas podendo estar legitimamente vazia, esse caso deixou de ser raro.
    /// </summary>
    private void AtualizarMensagemVazia()
    {
        var termo = TxtBusca.Text.Trim();

        // A aba de copiados foge do molde das outras três: nada ali é "cadastrado", e o texto
        // vazio precisa dizer o que fazer para a lista deixar de estar vazia.
        if (_aba == Aba.Clipboard)
        {
            TxtVazio.Text = termo.Length == 0
                ? "Nada copiado ainda. Copie um texto em qualquer programa e ele aparece aqui."
                : $"Nada copiado corresponde a \"{termo}\".";
            return;
        }

        // A frase inteira por aba, e não um substantivo encaixado num molde: "lembrete" é
        // masculino, e o molde que servia às outras três produziria "Nenhuma lembrete
        // cadastrada ainda."
        var (semNada, semResultado) = _aba switch
        {
            Aba.Tarefas   => ("Nenhuma tarefa cadastrada ainda.", "Nenhuma tarefa encontrada para"),
            Aba.Lembretes => ("Nenhum lembrete marcado ainda.",   "Nenhum lembrete encontrado para"),
            Aba.Notas     => ("Nenhuma nota cadastrada ainda.",   "Nenhuma nota encontrada para"),
            _             => ("Nenhuma macro cadastrada ainda.",  "Nenhuma macro encontrada para"),
        };

        TxtVazio.Text = termo.Length == 0 ? semNada : $"{semResultado} \"{termo}\".";
    }

    private void TxtBusca_TextChanged(object sender, TextChangedEventArgs e)
        => Recarregar(comDebounce: true);

    // ── Preview ──────────────────────────────────────────────────────────────

    private void AtualizarPreview()
    {
        switch (ListResultados.SelectedItem)
        {
            case Macro macro:
                MostrarPreviewDaMacro(macro);
                break;

            // Nota e tarefa entram como texto puro: não têm variável para resolver, e passar
            // pelo formatador transformaria em itálico um asterisco que a pessoa escreveu de
            // propósito.
            case Nota nota:
                MostrarPreviewDeTexto(nota.Conteudo);
                break;

            case Tarefa tarefa:
                MostrarPreviewDeTexto(tarefa.Observacoes);
                break;

            case Compromisso lembrete:
                MostrarPreviewDeTexto(lembrete.Observacoes);
                break;

            // Aqui a prévia é a única forma de ver o texto inteiro: o cartão da lista mostra
            // uma linha, e um item copiado costuma ter mais do que isso.
            case ItemClipboard item:
                MostrarPreviewDeTexto(item.Conteudo);
                break;

            default:
                PainelPreview.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private void MostrarPreviewDaMacro(Macro macro)
    {
        // O marcador de cursor sai da prévia: ele não é texto que vai aparecer no outro app.
        var conteudo = MarcadorDeCursor.Remover(macro.Conteudo);

        if (VariavelService.TemVariaveis(conteudo))
        {
            var variaveis = VariavelService.ExtrairVariaveis(conteudo, App.NomeDoUsuario);
            var valores = variaveis.ToDictionary(v => v.Token, v => v.ValorPadrao ?? v.Token);
            conteudo = VariavelService.Substituir(conteudo, valores);
        }

        ListaPreview.ItemsSource = PreviewFormatador.EmLinhas(conteudo);
        PainelPreview.Visibility = Visibility.Visible;
    }

    private void MostrarPreviewDeTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            PainelPreview.Visibility = Visibility.Collapsed;
            return;
        }

        // Sem formatação: numa observação de tarefa ou num texto copiado, asterisco é
        // asterisco — não é negrito de macro.
        ListaPreview.ItemsSource = PreviewFormatador.EmLinhas(texto.Trim(), formatar: false);
        PainelPreview.Visibility = Visibility.Visible;
    }

    private void BtnNovo_Click(object sender, RoutedEventArgs e) => NovoItem();

    private async void BtnFavorito_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: Macro macro }) return;
        e.Handled = true;

        try
        {
            await _macroService.ToggleFavoritoAsync(macro);
            if (string.IsNullOrWhiteSpace(TxtBusca.Text)) Recarregar();
        }
        catch (Exception ex) { App.LogErro(ex); AvisarFalha("Não consegui mudar o favorito."); }
    }

    // ── Quando uma ação não funciona ─────────────────────────────────────────

    /// <summary>
    /// Escreve a falha no lugar das dicas do rodapé.
    ///
    /// Esta janela não tem o toast das telas de dentro do app, e por isso tudo que dava errado
    /// aqui ia só para o erros.log: a pessoa apertava Enter, nada acontecia, e não havia como
    /// saber por quê. A mensagem fica até a próxima busca — ver <see cref="RecarregarAsync"/>.
    /// </summary>
    private void AvisarFalha(string texto)
    {
        PainelDicas.Visibility = Visibility.Collapsed;
        Estado.Visibility      = Visibility.Visible;
        Estado.Falhar(texto);
    }

    /// <summary>Devolve as dicas ao rodapé. Sem efeito quando não há falha na tela.</summary>
    private void LimparFalha()
    {
        if (Estado.Visibility != Visibility.Visible) return;

        Estado.Limpar();
        Estado.Visibility      = Visibility.Collapsed;
        PainelDicas.Visibility = Visibility.Visible;
    }

    // ── Área de transferência ────────────────────────────────────────────────

    private async void BtnFixarClipboard_Click(object sender, RoutedEventArgs e)
    {
        // e.Handled antes do await: sem isso o clique continua subindo e chega ao
        // MouseLeftButtonUp da lista, que insere o item no app de destino — fixar viraria
        // fixar-e-colar.
        e.Handled = true;
        if (sender is not System.Windows.Controls.Button { Tag: ItemClipboard item }) return;

        try
        {
            var (ok, msg) = await _clipboardService.FixarAsync(item.Id, !item.Fixado);
            if (!ok) { AvisarFalha(msg); return; }

            await RecarregarPreservandoPosicaoAsync();
        }
        catch (Exception ex) { App.LogErro(ex); AvisarFalha("Não consegui fixar o item."); }
    }

    private void BtnApagarClipboard_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is System.Windows.Controls.Button { Tag: ItemClipboard item })
            ApagarDoClipboard(item);
    }

    /// <summary>
    /// Apaga sem pedir confirmação, ao contrário de tarefa e nota.
    ///
    /// A diferença é o que se perde: uma nota é conteúdo escrito, que só existe ali; um item
    /// do histórico é uma cópia de algo que continua existindo na origem, e que sai da lista
    /// sozinho pela retenção de qualquer forma. Pedir dois cliques para isso seria atrito num
    /// gesto de limpeza que se repete muito.
    /// </summary>
    private async void ApagarDoClipboard(ItemClipboard item)
    {
        try
        {
            var (ok, msg) = await _clipboardService.ExcluirAsync(item.Id);
            if (!ok) { AvisarFalha(msg); return; }

            await RecarregarPreservandoPosicaoAsync();
        }
        catch (Exception ex) { App.LogErro(ex); AvisarFalha("Não consegui apagar o item."); }
    }

    /// <summary>
    /// Recarrega mantendo a seleção na mesma POSIÇÃO, e não no mesmo item.
    ///
    /// É o comportamento certo depois de apagar: a seleção fica sobre o item que ocupou o
    /// lugar, e apagar vários seguidos é apertar Delete várias vezes. Mesmo raciocínio do
    /// <see cref="AlternarConclusao"/>.
    /// </summary>
    private async Task RecarregarPreservandoPosicaoAsync()
    {
        var posicao = ListResultados.SelectedIndex;
        await RecarregarAsync(comDebounce: false);

        if (ListResultados.Items.Count > 0)
            ListResultados.SelectedIndex = Math.Min(Math.Max(posicao, 0), ListResultados.Items.Count - 1);
    }

    /// <summary>
    /// Esvaziar o histórico, com o segundo clique confirmando.
    ///
    /// Aqui os dois cliques existem porque a ação é irreversível E em lote — o oposto de
    /// apagar um item, que atinge uma linha só.
    /// </summary>
    private async void BtnLimpar_Click(object sender, RoutedEventArgs e)
    {
        if (!_confirmandoLimpeza)
        {
            _confirmandoLimpeza = true;
            BtnLimpar.Content   = "Limpar mesmo?";
            return;
        }

        DesarmarLimpeza();

        try
        {
            var (ok, msg) = await _clipboardService.LimparAsync();
            if (!ok) { AvisarFalha(msg); return; }

            await RecarregarAsync(comDebounce: false);
        }
        catch (Exception ex) { App.LogErro(ex); AvisarFalha("Não consegui limpar o histórico."); }
    }

    private void DesarmarLimpeza()
    {
        _confirmandoLimpeza = false;
        BtnLimpar.Content   = "Limpar";
    }

    // ── Teclado e clique ─────────────────────────────────────────────────────

    private void Vidro_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    // Captura Up/Down/Enter/Escape/Tab em qualquer elemento da janela
    // (PreviewKeyDown = túnel, antes de qualquer controle)
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+Enter na lista de notas insere sem abrir — o caminho rápido de quem só quer o
        // texto lá no outro programa.
        if (e.Key == Key.Return && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            && ListResultados.SelectedItem is Nota atalho)
        {
            InserirNota(atalho);
            e.Handled = true;
            return;
        }

        // Ctrl+N cria na aba em que se está. Em Macros não faz nada: macro tem formulário
        // próprio no app, com atalho, categoria e histórico — não cabe numa janela de atalho.
        if (e.Key == Key.N && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            NovoItem();
            e.Handled = true;
            return;
        }

        // Shift+Delete apaga o item copiado que está selecionado.
        //
        // Com Shift, e não Delete sozinho: o foco fica SEMPRE no campo de busca — é o que
        // permite continuar digitando enquanto se anda na lista — então um Delete puro é uma
        // tecla de edição de texto, e sequestrá-la faria apagar uma letra do termo apagar um
        // item do histórico. Shift+Delete é a mesma combinação que remove uma sugestão da
        // barra de endereços do navegador, que é exatamente este gesto.
        if (e.Key == Key.Delete && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
            && ListResultados.SelectedItem is ItemClipboard selecionado)
        {
            ApagarDoClipboard(selecionado);
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.F2:
                EditarSelecionado();
                e.Handled = true;
                break;
            case Key.Down:
                MoverSelecao(+1);
                e.Handled = true;
                break;
            case Key.Up:
                MoverSelecao(-1);
                e.Handled = true;
                break;
            case Key.Tab:
                // e.Handled impede o Tab de mover o foco: aqui ele troca de aba, e o foco
                // precisa continuar no campo de busca para a próxima tecla ser texto.
                GirarAba(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : +1);
                e.Handled = true;
                break;
            case Key.Return:
                AcionarSelecionado();
                e.Handled = true;
                break;
            case Key.Escape:
                Esconder();
                e.Handled = true;
                break;
        }
    }

    private void MoverSelecao(int delta)
    {
        if (ListResultados.Items.Count == 0) return;
        var idx = ListResultados.SelectedIndex + delta;
        idx = Math.Clamp(idx, 0, ListResultados.Items.Count - 1);
        ListResultados.SelectedIndex = idx;
        ListResultados.ScrollIntoView(ListResultados.SelectedItem);
        TxtBusca.Focus(); // mantém foco no campo de texto
    }

    private void TxtBusca_KeyDown(object sender, KeyEventArgs e) { /* tratado em Window_PreviewKeyDown */ }

    private void ListResultados_KeyDown(object sender, KeyEventArgs e) { /* tratado em Window_PreviewKeyDown */ }

    private void ListResultados_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => AcionarSelecionado();

    // ── Ação sobre o item selecionado ────────────────────────────────────────

    /// <summary>
    /// O que o Enter (e o clique) faz depende do que está selecionado, não da aba: macro e
    /// nota vão para o app de destino como texto, tarefa é marcada. Quem decide é o tipo do
    /// item, então a lista e a ação nunca podem discordar sobre qual aba está aberta.
    /// </summary>
    private void AcionarSelecionado()
    {
        switch (ListResultados.SelectedItem)
        {
            case Macro macro:        InserirMacro(macro);       break;
            case Nota nota:          AbrirNota(nota);           break;
            case Tarefa tarefa:      AlternarConclusao(tarefa); break;
            case Compromisso lembrete: AlternarRealizado(lembrete); break;

            // Vai pelo mesmo caminho de uma nota: teclas para o app de destino, sem passar
            // pela área de transferência de novo. Colar daqui não custa o que estiver copiado
            // agora — que é o problema que este histórico existe para não ter.
            case ItemClipboard item: InserirTexto(item.Conteudo); break;
        }
    }

    private async void InserirMacro(Macro macro)
    {
        Esconder();

        try
        {
            // Sem gatilho para remover: aqui o usuário não digitou nada no app de destino.
            await _insercao.InserirAsync(macro, antesDeInserir: DevolverFocoAJanelaDeOrigem);
        }
        catch (Exception ex) { App.LogErro(ex); }
    }

    private void InserirNota(Nota nota) => InserirTexto(nota.Conteudo);

    /// <summary>
    /// Manda texto cru para o app de destino, como uma macro faria — mas sem passar pelo
    /// resolvedor de variáveis: nota é texto que a pessoa escreveu para ler, e um {campo} no
    /// meio dela não é placeholder, é chave e colchete.
    /// </summary>
    private async void InserirTexto(string conteudo)
    {
        Esconder();

        try
        {
            DevolverFocoAJanelaDeOrigem();

            // Fora do log de uso de propósito: aquele log alimenta o ranking de macros mais
            // usadas e a ordem das recentes aqui em cima. Nota entrando nele empurraria macro
            // de verdade para fora das duas listas.
            //
            // interpretarMarcador: false — o {|} só vale em macro. Uma nota ou um trecho
            // copiado entram como estão escritos, e um {cursor} no meio deles é texto.
            await _insercaoTexto.InserirTextoAsync(string.Empty, conteudo, interpretarMarcador: false);
        }
        catch (Exception ex) { App.LogErro(ex); }
    }

    /// <summary>
    /// Marca ou desmarca a tarefa com a janela aberta. Fechar aqui seria copiar o
    /// comportamento da macro sem o motivo dele: a macro fecha porque o texto vai para outro
    /// app, e quem abriu na aba de tarefas quase sempre tem mais de uma para riscar.
    /// </summary>
    private async void AlternarConclusao(Tarefa tarefa)
    {
        try
        {
            var posicao = ListResultados.SelectedIndex;

            var (ok, msg) = await _tarefaService.ConcluirAsync(tarefa.Id, !tarefa.Concluida);
            if (!ok) { AvisarFalha(msg); return; }

            await RecarregarAsync(comDebounce: false);

            // A lista reordena e a concluída desce para o fim; manter a POSIÇÃO, e não o
            // item, deixa a seleção sobre a próxima tarefa aberta — a que vem a seguir.
            if (ListResultados.Items.Count > 0)
                ListResultados.SelectedIndex = Math.Min(posicao, ListResultados.Items.Count - 1);
        }
        catch (Exception ex) { App.LogErro(ex); AvisarFalha("Não consegui concluir a tarefa."); }
    }

    /// <summary>
    /// Marca o lembrete como feito, ou o devolve para a agenda — o mesmo gesto do Enter que
    /// conclui uma tarefa, e pelo mesmo motivo: quem abriu a aba quase sempre tem mais de um
    /// item para resolver, então a janela fica aberta e a seleção anda para o próximo.
    ///
    /// Marcar aqui também CALA os avisos que ainda não saíram: o lembrete deixa de estar
    /// agendado, e a varredura do LembreteService só enxerga os agendados. É o que evita ser
    /// avisado às 13h30 de uma visita que você acabou de riscar da lista.
    /// </summary>
    private async void AlternarRealizado(Compromisso lembrete)
    {
        try
        {
            var posicao = ListResultados.SelectedIndex;

            var (ok, msg) = await _compromissoService.DefinirSituacaoAsync(lembrete.Id, lembrete.Realizado
                ? SituacaoCompromisso.Agendado
                : SituacaoCompromisso.Realizado);

            if (!ok) { AvisarFalha(msg); return; }

            await RecarregarAsync(comDebounce: false);

            // Mantém a POSIÇÃO, e não o item: o realizado desce para o fim da lista, e a
            // seleção fica sobre o próximo lembrete que ainda vem.
            if (ListResultados.Items.Count > 0)
                ListResultados.SelectedIndex = Math.Min(posicao, ListResultados.Items.Count - 1);
        }
        catch (Exception ex) { App.LogErro(ex); AvisarFalha("Não consegui marcar o lembrete."); }
    }

    // ── As janelas laterais ──────────────────────────────────────────────────

    /// <summary>
    /// Abertas sob demanda e reaproveitadas. Ficam ao lado desta janela, não por cima: a lista
    /// continua à vista, e trocar de item é andar na lista — não voltar e procurar de novo.
    /// Só uma das duas fica aberta por vez; elas pertencem a abas diferentes.
    /// </summary>
    private NotaFlutuanteWindow?        _janelaNota;
    private TarefaFlutuanteWindow?      _janelaTarefa;
    private CompromissoFlutuanteWindow? _janelaLembrete;

    private Window? JanelaLateralAberta =>
        _janelaNota     is { IsVisible: true } ? _janelaNota     :
        _janelaTarefa   is { IsVisible: true } ? _janelaTarefa   :
        _janelaLembrete is { IsVisible: true } ? _janelaLembrete : null;

    private void AbrirNota(Nota nota)
    {
        FecharJanelaDaTarefa();
        FecharJanelaDoLembrete();
        _janelaNota ??= CriarJanelaDaNota();

        var jaEstavaAberta = _janelaNota.IsVisible;
        _janelaNota.Abrir(nota);

        // Só recoloca o par quando a janela aparece. Reposicionar a cada troca de item faria
        // as duas pularem enquanto a pessoa anda de seta na lista.
        if (!jaEstavaAberta) PosicionarPar(_janelaNota);
    }

    private void AbrirTarefa(Tarefa tarefa)
    {
        FecharJanelaDaNota();
        FecharJanelaDoLembrete();
        _janelaTarefa ??= CriarJanelaDaTarefa();

        var jaEstavaAberta = _janelaTarefa.IsVisible;
        _janelaTarefa.Abrir(tarefa);

        if (!jaEstavaAberta) PosicionarPar(_janelaTarefa);
    }

    private void AbrirLembrete(Compromisso lembrete)
    {
        FecharJanelaDaNota();
        FecharJanelaDaTarefa();
        _janelaLembrete ??= CriarJanelaDoLembrete();

        var jaEstavaAberta = _janelaLembrete.IsVisible;
        _janelaLembrete.Abrir(lembrete);

        if (!jaEstavaAberta) PosicionarPar(_janelaLembrete);
    }

    /// <summary>Ctrl+N: abre a janela lateral em branco, na aba em que se está.</summary>
    private void NovoItem()
    {
        if (_aba == Aba.Tarefas)
        {
            FecharJanelaDaNota();
            FecharJanelaDoLembrete();
            _janelaTarefa ??= CriarJanelaDaTarefa();

            var jaEstavaAberta = _janelaTarefa.IsVisible;
            _janelaTarefa.Nova();
            if (!jaEstavaAberta) PosicionarPar(_janelaTarefa);
        }
        else if (_aba == Aba.Lembretes)
        {
            FecharJanelaDaNota();
            FecharJanelaDaTarefa();
            _janelaLembrete ??= CriarJanelaDoLembrete();

            var jaEstavaAberta = _janelaLembrete.IsVisible;
            _janelaLembrete.Nova();
            if (!jaEstavaAberta) PosicionarPar(_janelaLembrete);
        }
        else if (_aba == Aba.Notas)
        {
            FecharJanelaDaTarefa();
            FecharJanelaDoLembrete();
            _janelaNota ??= CriarJanelaDaNota();

            var jaEstavaAberta = _janelaNota.IsVisible;
            _janelaNota.Nova();
            if (!jaEstavaAberta) PosicionarPar(_janelaNota);
        }
    }

    /// <summary>
    /// O lápis da linha, em Tarefas e Lembretes. Abre o mesmo que o F2, mas o alvo é o item
    /// CLICADO: a seleção vai junto, para o que está destacado na lista e o que está aberto ao
    /// lado nunca discordarem.
    /// </summary>
    private void BtnEditarItem_Click(object sender, RoutedEventArgs e)
    {
        // Sem isto o clique sobe até o MouseLeftButtonUp da lista, que risca a tarefa: editar
        // viraria editar-e-concluir — o engano que este botão existe para evitar.
        e.Handled = true;
        if (sender is not System.Windows.Controls.Button { Tag: { } item }) return;

        ListResultados.SelectedItem = item;
        EditarSelecionado();
    }

    /// <summary>F2: edita o item selecionado. Em Notas o Enter já faz isso; em Tarefas e em
    /// Lembretes o Enter risca o item, que é o que se quer na maioria das vezes — então
    /// editar precisa de tecla própria.</summary>
    private void EditarSelecionado()
    {
        switch (ListResultados.SelectedItem)
        {
            case Nota nota:            AbrirNota(nota);        break;
            case Tarefa tarefa:        AbrirTarefa(tarefa);    break;
            case Compromisso lembrete: AbrirLembrete(lembrete); break;
        }
    }

    private NotaFlutuanteWindow CriarJanelaDaNota()
    {
        var janela = new NotaFlutuanteWindow(_notaService) { Owner = this };
        janela.InserirSolicitado += conteudo => InserirTexto(conteudo);

        // A lista precisa refletir o título e a ordem novos — notas se ordenam pela última edição.
        janela.NotaGravada += () => Recarregar();

        // Perder o foco aqui vale o mesmo que perder o foco na busca: quem decide se o
        // conjunto todo some é EsconderSeSaiuDoConjunto, que olha as duas janelas.
        janela.Deactivated += (_, _) => AoPerderFoco();
        return janela;
    }

    private TarefaFlutuanteWindow CriarJanelaDaTarefa()
    {
        var janela = new TarefaFlutuanteWindow(_tarefaService, _relogio) { Owner = this };
        janela.ListaMudou  += () => Recarregar();
        janela.Deactivated += (_, _) => AoPerderFoco();
        return janela;
    }

    private CompromissoFlutuanteWindow CriarJanelaDoLembrete()
    {
        var janela = new CompromissoFlutuanteWindow(_compromissoService, _relogio) { Owner = this };
        janela.ListaMudou  += () => Recarregar();
        janela.Deactivated += (_, _) => AoPerderFoco();
        return janela;
    }

    private void FecharJanelaDaNota()
    {
        if (_janelaNota is { IsVisible: true }) _janelaNota.Fechar();
    }

    private void FecharJanelaDaTarefa()
    {
        if (_janelaTarefa is { IsVisible: true }) _janelaTarefa.Fechar();
    }

    private void FecharJanelaDoLembrete()
    {
        if (_janelaLembrete is { IsVisible: true }) _janelaLembrete.Fechar();
    }

    private void FecharJanelasLaterais()
    {
        FecharJanelaDaNota();
        FecharJanelaDaTarefa();
        FecharJanelaDoLembrete();
    }

    /// <summary>
    /// Põe as duas lado a lado e centraliza o CONJUNTO. Deixar esta janela parada no centro
    /// deixaria o par visivelmente torto; e se as duas não couberem juntas, a lateral vai para
    /// a borda direita da área de trabalho, que é o menos ruim numa tela estreita.
    /// </summary>
    private void PosicionarPar(Window lateral)
    {
        const double folga = 14;
        var area = AreaDeTrabalhoDoCursor();
        if (area.IsEmpty) return;

        var total = Width + folga + lateral.Width;

        if (total <= area.Width)
        {
            var esquerda = area.Left + (area.Width - total) / 2;
            Left = Math.Round(esquerda);
            lateral.Left = Math.Round(esquerda + Width + folga);
        }
        else
        {
            lateral.Left = Math.Round(area.Right - lateral.Width);
        }

        lateral.Top = Math.Round(Top);
    }

    /// <summary>
    /// Some com todas as janelas. Público porque o Ctrl+Espaço apertado de novo esconde a busca
    /// a partir da MainWindow, e um Hide() cru deixaria a lateral flutuando sozinha na tela.
    /// </summary>
    public void Esconder()
    {
        FecharJanelasLaterais();
        Hide();
    }

    private void AoPerderFoco()
    {
        // Ir da busca para a lateral (ou o contrário) passa por um instante em que NENHUMA das
        // duas está ativa. Decidir no próximo ciclo do dispatcher deixa a outra assumir o foco
        // antes — sem isso, clicar na nota fecharia a busca que a abriu.
        Dispatcher.BeginInvoke(new Action(EsconderSeSaiuDoConjunto), DispatcherPriority.Background);
    }

    private void EsconderSeSaiuDoConjunto()
    {
        if (IsActive || JanelaLateralAberta?.IsActive == true) return;
        Esconder();
    }

    /// <summary>
    /// Devolve o foco para a janela que estava ativa antes do buscador abrir. Precisa
    /// acontecer depois de resolver o conteúdo e antes de mandar as teclas: mais cedo, o
    /// diálogo de variáveis roubaria o foco de volta e o texto iria para a janela errada.
    /// </summary>
    private void DevolverFocoAJanelaDeOrigem()
    {
        if (_janelaOrigem != IntPtr.Zero) SetForegroundWindow(_janelaOrigem);
    }
}
