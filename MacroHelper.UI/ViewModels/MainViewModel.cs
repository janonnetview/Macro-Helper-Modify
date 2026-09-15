using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroHelper.Services;

namespace MacroHelper.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly MacrosViewModel           _macrosVM;
    private readonly ConfiguracoesViewModel    _configVM;
    private readonly CategoriasViewModel       _categoriasVM;
    private readonly DashboardViewModel        _dashboardVM;
    private readonly VariaveisGlobaisViewModel _variaveisVM;
    private readonly TarefasViewModel          _tarefasVM;
    private readonly CompromissosViewModel     _compromissosVM;
    private readonly NotasViewModel            _notasVM;
    private readonly AjudaViewModel            _ajudaVM;
    private readonly ManualSqlViewModel        _manualSqlVM;

    [ObservableProperty] private ObservableObject? _currentView;
    [ObservableProperty] private string _paginaAtiva    = "macros";
    [ObservableProperty] private string _statusMensagem = "SK MacroHelper ativo";
    [ObservableProperty] private string _nomeUsuario    = string.Empty;

    // ── Sidebar: estado da expansão de Macros e dropdown do avatar ──
    [ObservableProperty] private bool _macrosExpandido  = true;
    [ObservableProperty] private bool _avatarMenuAberto = false;

    /// <summary>True quando qualquer sub-página de Macros está ativa (ativa o item pai na sidebar).</summary>
    public bool MacrosPaginaAtiva => PaginaAtiva is "macros" or "categorias" or "variaveis" or "manualsql";

    partial void OnPaginaAtivaChanged(string value) => OnPropertyChanged(nameof(MacrosPaginaAtiva));

    /// <summary>Pedido de encerrar o app pelo menu do avatar — MainWindow libera os recursos.</summary>
    public event EventHandler? SaidaSolicitada;

    public MainViewModel(
        MacrosViewModel macrosVM, ConfiguracoesViewModel configVM,
        CategoriasViewModel categoriasVM, DashboardViewModel dashboardVM,
        VariaveisGlobaisViewModel variaveisVM, TarefasViewModel tarefasVM,
        CompromissosViewModel compromissosVM,
        NotasViewModel notasVM, AjudaViewModel ajudaVM,
        ManualSqlViewModel manualSqlVM)
    {
        _macrosVM       = macrosVM;
        _tarefasVM      = tarefasVM;
        _compromissosVM = compromissosVM;
        _notasVM        = notasVM;
        _configVM       = configVM;
        _categoriasVM   = categoriasVM;
        _dashboardVM    = dashboardVM;
        _variaveisVM    = variaveisVM;
        _ajudaVM        = ajudaVM;
        _manualSqlVM    = manualSqlVM;

        NomeUsuario = NomeConfigurado();

        _dashboardVM.NovaMacroSolicitada += (_, _) =>
        {
            NavigarParaMacros();
            _macrosVM.NovaMacro();
        };

        _dashboardVM.TarefasSolicitadas += (_, _) => NavigarParaTarefas();

        _dashboardVM.CompromissosSolicitados += (_, _) => NavigarParaCompromissos();

        NavigarParaMacros();
    }

    [RelayCommand]
    private void ToggleMacrosExpansao() => MacrosExpandido = !MacrosExpandido;

    /// <summary>
    /// "Sair" agora encerra o app de verdade. Não existe mais sessão para derrubar, e o botão
    /// tem um uso concreto: com "minimizar para a bandeja" ligado, o X da janela só esconde.
    /// </summary>
    [RelayCommand]
    public void Sair()
    {
        AvatarMenuAberto = false;
        SaidaSolicitada?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Relê o nome das Configurações — chamado ao voltar da tela onde ele é editado.</summary>
    public void AtualizarNome() => NomeUsuario = NomeConfigurado();

    /// <summary>O avatar e o "Olá, ..." do Início precisam de um rótulo visível mesmo sem nome configurado.</summary>
    private static string NomeConfigurado() =>
        string.IsNullOrWhiteSpace(App.NomeDoUsuario) ? "Você" : App.NomeDoUsuario;

    [RelayCommand] public void NavigarParaMacros()
    {
        PaginaAtiva = "macros"; CurrentView = _macrosVM;
        _ = _macrosVM.CarregarAsync();
    }
    [RelayCommand] public void NavigarParaCategorias()
    {
        MacrosExpandido = true;
        PaginaAtiva = "categorias"; CurrentView = _categoriasVM;
        _ = _categoriasVM.CarregarAsync();
    }
    [RelayCommand] public void NavigarParaDashboard()
    {
        PaginaAtiva = "dashboard"; CurrentView = _dashboardVM;
        _ = _dashboardVM.CarregarAsync();
    }
    [RelayCommand] public void NavigarParaVariaveis()
    {
        MacrosExpandido = true;
        PaginaAtiva = "variaveis"; CurrentView = _variaveisVM;
        _ = _variaveisVM.CarregarAsync();
    }
    [RelayCommand] public void NavigarParaManualSql()
    {
        MacrosExpandido = true;
        PaginaAtiva = "manualsql"; CurrentView = _manualSqlVM;
    }
    [RelayCommand] public void NavigarParaTarefas()
    {
        PaginaAtiva = "tarefas"; CurrentView = _tarefasVM;
        _ = _tarefasVM.CarregarAsync();
    }
    [RelayCommand] public void NavigarParaCompromissos()
    {
        PaginaAtiva = "compromissos"; CurrentView = _compromissosVM;
        _ = _compromissosVM.CarregarAsync();
    }
    [RelayCommand] public void NavigarParaNotas()
    {
        PaginaAtiva = "notas"; CurrentView = _notasVM;
        _ = _notasVM.CarregarAsync();
    }
    [RelayCommand] public void NavigarParaAjuda()
    {
        AvatarMenuAberto = false;
        PaginaAtiva = "ajuda"; CurrentView = _ajudaVM;
    }
    [RelayCommand] public void NavigarParaConfiguracoes()
    {
        AvatarMenuAberto = false;
        PaginaAtiva = "configuracoes"; CurrentView = _configVM;
    }

    /// <summary>Sair de Configurações reflete na hora um "Seu nome" que tenha sido alterado lá.</summary>
    partial void OnPaginaAtivaChanging(string? oldValue, string newValue)
    {
        if (oldValue == "configuracoes" && newValue != "configuracoes") AtualizarNome();
    }

    public void AbrirNovaMacroComConteudo(string conteudo)
    {
        NavigarParaMacros();
        _macrosVM.NovaMacro();
        if (_macrosVM.FormularioAtual != null)
            _macrosVM.FormularioAtual.Conteudo = conteudo;
    }
}
