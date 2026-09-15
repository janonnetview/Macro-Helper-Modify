using MacroHelper.Core.Entities;
using MacroHelper.Services;
using MacroHelper.UI.Helpers;
using MacroHelper.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using Application = System.Windows.Application;

namespace MacroHelper.UI.Views;

public partial class MainWindow : Window
{
    private readonly KeyboardHookService    _hookService;
    private readonly MacroService           _macroService;
    private readonly TextInsertionService   _insertionService;
    private readonly LogUsoService          _logService;
    private readonly InsercaoDeMacroService _insercao;
    private readonly LembreteService        _lembretes;
    private readonly TarefaService          _tarefaService;
    private readonly CompromissoService     _compromissoService;
    private readonly NotaService            _notaService;
    private readonly ClipboardService       _clipboardService;
    private readonly IRelogio               _relogio;
    private readonly TrayService            _trayService;
    private readonly HotkeyService          _hotkeyService;
    private readonly ClipboardMonitorService _clipboardMonitor;
    private MacroPopupWindow?             _popup;
    private BuscadorRapidoWindow?         _buscador;
    private TarefaFlutuanteWindow?        _tarefaDoLembrete;
    private bool                          _hookAtivo = true;

    public MainWindow()
    {
        InitializeComponent();
        DataContext       = App.Services.GetRequiredService<MainViewModel>();
        _hookService      = App.Services.GetRequiredService<KeyboardHookService>();
        _macroService     = App.Services.GetRequiredService<MacroService>();
        _insertionService = App.Services.GetRequiredService<TextInsertionService>();
        _logService       = App.Services.GetRequiredService<LogUsoService>();
        _insercao         = App.Services.GetRequiredService<InsercaoDeMacroService>();
        _lembretes        = App.Services.GetRequiredService<LembreteService>();
        _tarefaService    = App.Services.GetRequiredService<TarefaService>();
        _compromissoService = App.Services.GetRequiredService<CompromissoService>();
        _notaService      = App.Services.GetRequiredService<NotaService>();
        _clipboardService = App.Services.GetRequiredService<ClipboardService>();
        _relogio          = App.Services.GetRequiredService<IRelogio>();

        _trayService      = App.Services.GetRequiredService<TrayService>();
        _hotkeyService    = App.Services.GetRequiredService<HotkeyService>();
        _clipboardMonitor = App.Services.GetRequiredService<ClipboardMonitorService>();

        Loaded += OnWindowLoaded;
        Closing += OnWindowClosing;
        StateChanged += OnStateChanged;
        Activated += (_, _) => App.RestaurarIdiomaDoSistema();
        PreviewKeyDown += OnPreviewKeyDown;

        if (DataContext is MainViewModel mvm)
            mvm.SaidaSolicitada += (_, _) => Dispatcher.Invoke(FecharApp);

        LigarPaginaOculta();
    }

    /// <summary>
    /// As três saídas da página que a interface não anuncia, além do próprio atalho: o
    /// Ctrl+Shift+F digitado lá dentro (que não chega ao WPF e volta como recado do navegador),
    /// navegar para outra página e clicar na barra lateral.
    /// </summary>
    private void LigarPaginaOculta()
    {
        PaginaOculta.SaidaSolicitada += () => Dispatcher.Invoke(FecharPaginaOculta);

        if (DataContext is MainViewModel vm)
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.PaginaAtiva)) FecharPaginaOculta();
            };

        // Clicar de novo no item em que já se está não muda PaginaAtiva, e sem isto a página
        // continuaria por cima de um clique que pedia justamente o contrário.
        Sidebar.PreviewMouseLeftButtonDown += (_, _) => FecharPaginaOculta();
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        AplicarEstiloNativo();
        IniciarHook();
        IniciarTray();
        IniciarHotkey();
        IniciarClipboard();
        IniciarLembretes();
        MostrarTourSeNecessario();
    }

    // ── Histórico da área de transferência ───────────────────────
    private void IniciarClipboard()
    {
        _clipboardMonitor.Ativo    = Properties.Settings.Default.HistoricoClipboardAtivo;
        _clipboardMonitor.AoFalhar = App.LogErro;

        // O serviço de inserção avisa quando o PRÓPRIO app escreve na área de transferência —
        // ao colar uma macro e, meio segundo depois, ao devolver o que estava lá antes. Sem
        // esta ligação as duas escritas entrariam no histórico como se fossem cópias de quem
        // está usando o app.
        _insertionService.EscreveuNoClipboard += _clipboardMonitor.IgnorarProximaEscrita;

        _clipboardMonitor.TextoCopiado += RegistrarCopia;
        _clipboardMonitor.Iniciar(this);
    }

    private async void RegistrarCopia(string texto, string? origem)
    {
        try { await _clipboardService.RegistrarAsync(texto, origem); }
        catch (Exception ex) { App.LogErro(ex); }
    }

    // ── Lembretes de tarefas ─────────────────────────────────────
    private void IniciarLembretes()
    {
        _lembretes.LembretesVencidos += NotificarLembretes;
        _lembretes.AvisosDeCompromisso += NotificarCompromissos;

        // Depois da bandeja existir: a primeira varredura acontece dentro de IniciarAsync e
        // pode disparar um balão na hora, para lembretes que venceram com o app fechado.
        _ = _lembretes.IniciarAsync();
    }

    /// <summary>
    /// Um lembrete vira um balão com o título da tarefa; vários viram um resumo. Vinte balões
    /// em sequência se sobrescreveriam na bandeja e só o último apareceria — resumir é o que
    /// realmente informa depois de uma ausência longa.
    /// </summary>
    private void NotificarLembretes(IReadOnlyList<Tarefa> tarefas)
    {
        if (tarefas.Count == 0) return;

        if (tarefas.Count == 1)
        {
            var tarefa  = tarefas[0];
            var atrasou = tarefa.Lembrete.HasValue &&
                          DateTime.Now - tarefa.Lembrete.Value > TimeSpan.FromMinutes(5);

            // Clicar abre a tarefa, e é ali que estão os botões de adiar. É o que fecha o
            // ciclo do lembrete: o balão da bandeja não aceita botões próprios, então sem um
            // destino para o clique um lembrete que chega em hora ruim está simplesmente
            // perdido — lembrete_disparado já foi gravado, e ele não volta sozinho.
            _trayService.MostrarNotificacao(
                atrasou ? $"Lembrete de {tarefa.Lembrete:dd/MM 'às' HH:mm}" : "Lembrete",
                tarefa.Titulo,
                System.Windows.Forms.ToolTipIcon.Info, 8000,
                aoClicar: () => AbrirTarefaDoLembrete(tarefa.Id));
            return;
        }

        // Vários de uma vez não cabem numa janela só: o clique leva para a tela de Tarefas.
        _trayService.MostrarNotificacao(
            $"{tarefas.Count} lembretes venceram",
            string.Join(" · ", tarefas.Take(3).Select(t => t.Titulo)) +
                (tarefas.Count > 3 ? $" e mais {tarefas.Count - 3}" : string.Empty),
            System.Windows.Forms.ToolTipIcon.Info, 10000,
            aoClicar: AbrirTelaDeTarefas);
    }

    /// <summary>
    /// Abre a tarefa do lembrete na janela flutuante, com os botões de adiar à mão.
    ///
    /// Sem Owner de propósito: com o app na bandeja, a MainWindow está escondida, e apontar
    /// para ela faria o Esc da janelinha trazer o app inteiro para a frente — o oposto de
    /// "só quero adiar isto e voltar ao que eu estava fazendo".
    /// </summary>
    private async void AbrirTarefaDoLembrete(int id)
    {
        try
        {
            var tarefa = await _tarefaService.ObterPorIdAsync(id);
            if (tarefa == null) return;

            if (_tarefaDoLembrete == null)
            {
                _tarefaDoLembrete = new TarefaFlutuanteWindow(_tarefaService, _relogio)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                };
                _tarefaDoLembrete.ListaMudou += AtualizarTelaDeTarefas;
            }

            _tarefaDoLembrete.Abrir(tarefa);
            _tarefaDoLembrete.Activate();
        }
        catch (Exception ex) { App.LogErro(ex); }
    }

    /// <summary>
    /// O aviso de um compromisso vira um balão que diz QUANTO FALTA, e não que horas são.
    /// "Em 25 min" é a informação que faz alguém se levantar; "14:00" obriga a conta mental
    /// que, no meio de outra tarefa, é justamente a que não é feita.
    ///
    /// Clicar leva para a agenda, e não para uma janelinha de adiar como nas tarefas: um
    /// compromisso não se adia sozinho — o técnico chega às 14h de qualquer forma. O que
    /// resta a fazer é olhar o que está marcado.
    /// </summary>
    private void NotificarCompromissos(IReadOnlyList<AvisoVencido> avisos)
    {
        if (avisos.Count == 0) return;

        if (avisos.Count == 1)
        {
            var compromisso = avisos[0].Compromisso;
            var onde = string.IsNullOrWhiteSpace(compromisso.Local)
                ? string.Empty
                : $" · {compromisso.Local}";

            _trayService.MostrarNotificacao(
                $"Lembrete: {compromisso.ContagemTexto(DateTime.Now)}",
                compromisso.Titulo + onde,
                System.Windows.Forms.ToolTipIcon.Info, 10000,
                aoClicar: AbrirTelaDeCompromissos);
            return;
        }

        // Vários de uma vez não cabem num balão só — mesma razão do resumo dos lembretes.
        _trayService.MostrarNotificacao(
            $"{avisos.Count} lembretes chegando",
            string.Join(" · ", avisos.Take(3).Select(a => $"{a.Compromisso.Titulo} ({a.Compromisso.HoraTexto})")) +
                (avisos.Count > 3 ? $" e mais {avisos.Count - 3}" : string.Empty),
            System.Windows.Forms.ToolTipIcon.Info, 10000,
            aoClicar: AbrirTelaDeCompromissos);
    }

    private void AbrirTelaDeCompromissos()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        if (DataContext is MainViewModel vm) vm.NavigarParaCompromissosCommand.Execute(null);
    }

    private void AbrirTelaDeTarefas()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        if (DataContext is MainViewModel vm) vm.NavigarParaTarefasCommand.Execute(null);
    }

    /// <summary>Refaz a lista de Tarefas se ela estiver aberta — o adiamento mudou o que ela mostra.</summary>
    private void AtualizarTelaDeTarefas()
    {
        if (DataContext is MainViewModel { PaginaAtiva: "tarefas" } vm)
            vm.NavigarParaTarefasCommand.Execute(null);
    }

    /// <summary>
    /// A janela principal é a única que fica com a sombra do sistema: ela tem tamanho de janela
    /// de programa e, sem sombra, encostaria no que estiver atrás sem nenhum limite. As
    /// flutuantes se separam pela borda do cartão — ver <see cref="MolduraNativa"/>.
    /// </summary>
    private void AplicarEstiloNativo() => MolduraNativa.Aplicar(this, comSombra: true);

    private void MostrarTourSeNecessario()
    {
        try
        {
            if (Properties.Settings.Default.TourConcluido) return;
            var tour = new TourWindow();
            tour.Closed += (_, _) =>
            {
                Properties.Settings.Default.TourConcluido = true;
                Properties.Settings.Default.Save();
            };
            tour.Show();
        }
        catch (Exception ex) { App.LogErro(ex); }
    }

    // ── Hook de teclado ─────────────────────────────────────────
    private string? _ultimaFraseSugerida;
    private CancellationTokenSource? _gatilhoCts;

    private void IniciarHook()
    {
        // Prefixo de gatilho customizável
        var prefixoCfg = Properties.Settings.Default.GatilhoPrefixo;
        _hookService.Prefixo = !string.IsNullOrEmpty(prefixoCfg) ? prefixoCfg[0] : '/';

        // Apps que devem usar digitação direta em vez de colar
        var appsDigitacao = Properties.Settings.Default.AppsModoDigitacao;
        if (!string.IsNullOrWhiteSpace(appsDigitacao))
            _insertionService.AppsModoDigitacao = appsDigitacao
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        _hookService.DeteccaoFraseAtiva = Properties.Settings.Default.SugestaoProativaIA;
        _hookService.VkRepetirUltima    = Properties.Settings.Default.AtalhoRepetirVk;

        _popup = new MacroPopupWindow(_insercao);

        _hookService.GatilhoDetectado += async (_, gatilho) =>
        {
            if (!_hookAtivo) return;

            // Cancela a busca anterior e aguarda 120ms antes de disparar a nova query
            _gatilhoCts?.Cancel();
            var cts = new CancellationTokenSource();
            _gatilhoCts = cts;
            try
            {
                await Task.Delay(120, cts.Token);
                if (cts.IsCancellationRequested) return;
                var macros = await _macroService.BuscarPorGatilhoAsync(gatilho, _hookService.Prefixo);
                if (cts.IsCancellationRequested) return;
                var lista = macros.ToList();
                await Dispatcher.InvokeAsync(() =>
                {
                    if (lista.Count > 0) _popup!.AtualizarSugestoes(gatilho, lista);
                    else                 _popup!.Fechar();
                });
            }
            // Cancelamento é o caminho normal aqui: cada tecla nova cancela a busca anterior.
            catch (OperationCanceledException) { }
            catch (Exception ex) { App.LogErro(ex); }
        };

        _hookService.GatilhoCancelado += (_, _) =>
            Dispatcher.Invoke(() => _popup?.Fechar());

        // Ctrl+Shift+Z — desfaz a última inserção
        _hookService.UndoSolicitado += (_, _) =>
        {
            if (_insertionService.TemUndoDisponivel)
                _ = _insertionService.DesfazerUltimaInsercaoAsync();
        };

        // Ctrl+Alt+1..9 — atalho de teclado dedicado por macro.
        // Passa pelo mesmo InsercaoDeMacroService dos outros dois caminhos: até a Onda 3 este
        // era o único que não abria o diálogo de variáveis, e trocava {nome} por vazio.
        _hookService.AtalhoTecladoPressionado += async (_, digito) =>
        {
            if (!_hookAtivo) return;
            try
            {
                var macro = await _macroService.ObterPorAtalhoTeclaAsync(digito);
                if (macro == null) return;
                await _insercao.InserirAsync(macro);
            }
            catch (Exception ex) { App.LogErro(ex); }
        };

        // Ctrl+Alt+0 — repete a última macro inserida
        _hookService.RepetirUltimaSolicitado += (_, _) =>
        {
            if (_insertionService.TemUltimaParaRepetir)
                _ = _insertionService.RepetirUltimaInsercaoAsync();
        };

        // Frase repetida 3x — sugestão proativa de macro
        _hookService.FraseRepetidaDetectada += (_, frase) =>
        {
            _ultimaFraseSugerida = frase;
            Dispatcher.Invoke(() => _trayService.MostrarNotificacao(
                "Que tal criar uma macro?",
                "Você digitou o mesmo texto 3 vezes. Clique aqui para transformar em macro.",
                System.Windows.Forms.ToolTipIcon.Info, 6000,
                aoClicar: AbrirSugestaoDeMacro));
        };

        _hookService.Iniciar();

        if (DataContext is MainViewModel mvm)
            mvm.StatusMensagem = "SK MacroHelper · Hook ativo · Ctrl+Espaço para busca rápida";
    }

    private void AbrirSugestaoDeMacro()
    {
        if (string.IsNullOrWhiteSpace(_ultimaFraseSugerida)) return;
        AbrirJanelaPrincipal();
        if (DataContext is MainViewModel vm)
        {
            vm.NavigarParaMacrosCommand.Execute(null);
            vm.AbrirNovaMacroComConteudo(_ultimaFraseSugerida);
        }
        _ultimaFraseSugerida = null;
    }

    // ── Bandeja do sistema ───────────────────────────────────────
    private void IniciarTray()
    {
        var nome = App.NomeDoUsuario;
        _trayService.Iniciar(string.IsNullOrWhiteSpace(nome) ? "local" : nome);

        _trayService.AbrirJanela += () => Dispatcher.Invoke(AbrirJanelaPrincipal);

        _trayService.AbrirBuscadorRapido += () =>
            Dispatcher.Invoke(AbrirBuscadorRapido);

        _trayService.AlternarHook += () => Dispatcher.Invoke(() =>
        {
            _hookAtivo = !_hookAtivo;
            _trayService.AtualizarStatus(_hookAtivo);
            if (DataContext is MainViewModel vm)
                vm.StatusMensagem = _hookAtivo
                    ? "Hook ativo · Ctrl+Espaço para busca"
                    : "⏸ Hook pausado · clique no ícone para retomar";
        });

        _trayService.Sair += () => Dispatcher.Invoke(FecharApp);
    }

    // ── Hotkey Busca rápida (padrão Ctrl+Espaço, remapeável em Configurações) ──
    private void IniciarHotkey()
    {
        _hotkeyService.Iniciar(this,
            Properties.Settings.Default.AtalhoBuscaModificador,
            Properties.Settings.Default.AtalhoBuscaTecla);
        _hotkeyService.BuscadorRapidoSolicitado += () =>
            Dispatcher.Invoke(() =>
            {
                if (_buscador?.IsVisible == true) _buscador.Esconder();
                else                              AbrirBuscadorRapido();
            });

        // Ctrl+Shift+Espaço: mesma tecla para chamar e para dispensar a janela principal,
        // de qualquer lugar do Windows — inclusive com o app escondido na bandeja.
        _hotkeyService.JanelaPrincipalSolicitada +=
            () => Dispatcher.Invoke(AlternarJanelaPrincipal);

        _hotkeyService.PainelSolicitado += () => Dispatcher.Invoke(AlternarPaginaOculta);
    }

    /// <summary>
    /// Ctrl+Shift+F com a janela do app na frente. O caminho global é o Ctrl+Alt+Shift+F, que
    /// mora no HotkeyService e explica lá por que o Alt entra na conta.
    ///
    /// PreviewKeyDown ligado no código, e não um KeyBinding no XAML: assim a combinação não
    /// aparece em arquivo nenhum que a interface mostre.
    /// </summary>
    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.F) return;
        if (System.Windows.Input.Keyboard.Modifiers !=
            (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift)) return;

        AlternarPaginaOculta();
        e.Handled = true;
    }

    /// <summary>
    /// Abre a página que a interface não anuncia, por cima da página aberta, ou volta para
    /// esta. Com o app escondido na bandeja ou atrás de outro programa, a primeira chamada
    /// traz o app para a frente — senão o atalho "sumiria" com o que se acabou de pedir.
    /// </summary>
    private void AlternarPaginaOculta()
    {
        try
        {
            if (PaginaOculta.Visibility == Visibility.Visible && EstaNaFrente())
            {
                FecharPaginaOculta();
                return;
            }

            AbrirJanelaPrincipal();
            PaginaOculta.Visibility = Visibility.Visible;
            _ = PaginaOculta.AbrirAsync();
        }
        catch (Exception ex) { App.LogErro(ex); }
    }

    /// <summary>
    /// Sai da página. Recolher e não descartar: o que está dentro continua vivo e tocando, e é
    /// isso que faz a próxima abertura voltar no ponto exato em que se estava.
    /// </summary>
    private void FecharPaginaOculta()
    {
        if (PaginaOculta.Visibility != Visibility.Visible) return;

        PaginaOculta.Gravar();
        PaginaOculta.Visibility = Visibility.Collapsed;
        Activate();
    }

    /// <summary>
    /// Se o app é o que está na frente. O <c>IsActive</c> sozinho não serve: com o foco dentro
    /// do navegador da página, o WPF chega a dizer que a janela não está ativa, e aí o atalho
    /// ficaria atrasado uma tecla — mostrando de novo o que já estava na tela.
    /// </summary>
    private bool EstaNaFrente()
    {
        if (IsActive) return true;

        var frente = GetForegroundWindow();
        return frente != IntPtr.Zero &&
               frente == new System.Windows.Interop.WindowInteropHelper(this).Handle;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// Janela na frente some; janela escondida, minimizada ou atrás de outra vem para cima.
    ///
    /// O IsActive é o que separa os dois casos que parecem um só. Uma janela pode estar
    /// visível e ainda assim ser o que a pessoa quer ver — aberta atrás do navegador, por
    /// exemplo. Sem essa checagem o atalho minimizaria justamente a janela que ela acabou de
    /// pedir, e seria preciso apertar duas vezes para trazê-la.
    /// </summary>
    private void AlternarJanelaPrincipal()
    {
        if (IsVisible && WindowState != WindowState.Minimized && IsActive)
            WindowState = WindowState.Minimized;   // OnStateChanged manda para a bandeja se estiver configurado
        else
            AbrirJanelaPrincipal();
    }

    /// <summary>Tira a janela da bandeja (ou do minimizado) e a coloca na frente de tudo.</summary>
    private void AbrirJanelaPrincipal()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void AbrirBuscadorRapido()
    {
        _buscador ??= new BuscadorRapidoWindow(_macroService, _insercao, _logService,
                                               _tarefaService, _compromissoService,
                                               _notaService, _clipboardService,
                                               _insertionService, _relogio);
        _buscador.Mostrar();
    }

    // ── Minimizar para tray ──────────────────────────────────────
    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized) return;
        try
        {
            if (Properties.Settings.Default.MinimizarParaBandeja)
            {
                Hide();
                _trayService.MostrarNotificacao(
                    "SK MacroHelper",
                    "Rodando em segundo plano. Clique duplo no ícone para abrir.",
                    System.Windows.Forms.ToolTipIcon.Info, 2000);
            }
        }
        catch (Exception ex) { App.LogErro(ex); }
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            if (Properties.Settings.Default.MinimizarParaBandeja)
            {
                e.Cancel = true;
                Hide();
                _trayService.MostrarNotificacao("SK MacroHelper",
                    "Minimizado para a bandeja. Clique duplo para abrir.",
                    System.Windows.Forms.ToolTipIcon.Info, 1500);
                return;
            }
        }
        catch (Exception ex) { App.LogErro(ex); }
        FecharApp();
    }

    private void LiberarRecursos()
    {
        _lembretes.Parar();
        _hookService.Parar();
        _hotkeyService.Dispose();
        _clipboardMonitor.Dispose();
        _trayService.Dispose();
        _popup?.Close();
        _buscador?.Close();
        _tarefaDoLembrete?.Close();

        // Grava onde a pessoa parou na página que a interface não anuncia.
        PaginaOculta.Gravar();
    }

    private void FecharApp()
    {
        LiberarRecursos();
        Application.Current.Shutdown();
    }

    // ── Title bar controls ───────────────────────────────────────
    private void Window_Activated(object s, EventArgs e) => App.RestaurarIdiomaDoSistema();

    private void TitleBar_MouseLeftButtonDown(object s, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) ToggleMax(); else DragMove();
    }
    private void SearchButton_Click(object s, RoutedEventArgs e)
    {
        if (_buscador?.IsVisible == true) _buscador.Esconder();
        else                              AbrirBuscadorRapido();
    }
    private void MinimizeButton_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeButton_Click(object s, RoutedEventArgs e) => ToggleMax();
    private void ToggleMax() => WindowState = WindowState == WindowState.Maximized
        ? WindowState.Normal : WindowState.Maximized;
    private void CloseButton_Click(object s, RoutedEventArgs e)
    {
        try
        {
            if (Properties.Settings.Default.MinimizarParaBandeja) { Hide(); return; }
        }
        catch (Exception ex) { App.LogErro(ex); }
        FecharApp();
    }
}
