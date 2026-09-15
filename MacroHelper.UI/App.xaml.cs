using MacroHelper.Data;
using MacroHelper.Data.Context;
using MacroHelper.Data.Repositories;
using MacroHelper.Services;
using MacroHelper.UI.Properties;
using MacroHelper.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace MacroHelper.UI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    // LANGID PT-BR = 0x0416; KLF_ACTIVATE = 1
    private const uint KLF_ACTIVATE = 1;
    private static IntPtr _ptBrLayout = IntPtr.Zero;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

    [DllImport("user32.dll")]
    private static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint Flags);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowByCaption(string? lpClassName, string lpWindowName);
    private static IntPtr FindWindowByCaption(string caption) => FindWindowByCaption(null, caption);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    /// <summary>
    /// Fonte única do nome do usuário desde que o login saiu: o campo "Seu nome" das
    /// Configurações. Alimenta o placeholder {usuario} das macros (nos três caminhos de
    /// inserção), o cabeçalho do Início e o tooltip da bandeja.
    ///
    /// Vazio quando não configurado — quem precisa de um rótulo visível decide o texto padrão;
    /// quem substitui {usuario} numa macro deve inserir vazio mesmo, e não a palavra "Você".
    /// </summary>
    public static string NomeDoUsuario
    {
        get
        {
            try { return Settings.Default.SeuNome ?? string.Empty; }
            catch (Exception ex) { LogErro(ex); return string.Empty; }
        }
    }

    // Ativa explicitamente o layout PT-BR em qualquer janela que ganhar foco
    public static void RestaurarIdiomaDoSistema()
    {
        if (_ptBrLayout != IntPtr.Zero)
            ActivateKeyboardLayout(_ptBrLayout, 0);
    }

    private static System.Threading.Mutex? _instanceMutex;

    /// <summary>
    /// Deixou de ser <c>async void</c>: sem login, sem sessão a restaurar e sem rede, não há
    /// mais nada para aguardar aqui. O banco é local e o migrator é síncrono, então a janela
    /// abre no primeiro frame — e some junto a classe de bug em que uma exceção num
    /// <c>async void</c> escapa do try/catch do chamador.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        // Garante instância única: se já existe uma rodando, traz ao foco e sai
        _instanceMutex = new System.Threading.Mutex(true, "MacroHelper_SK_SingleInstance", out var isNewInstance);
        if (!isNewInstance)
        {
            var hWnd = FindWindowByCaption("SK MacroHelper");
            if (hWnd != IntPtr.Zero)
            {
                ShowWindow(hWnd, 9); // SW_RESTORE
                SetForegroundWindow(hWnd);
            }
            _instanceMutex.Close();
            Current.Shutdown();
            return;
        }

        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogErro(args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogErro(args.Exception);
            args.SetObserved();
        };

        // Carrega o layout PT-BR pelo LANGID fixo — independente do que o Windows
        // associou ao app (per-app language). "00000416" = Portuguese (Brazil).
        _ptBrLayout = LoadKeyboardLayout("00000416", KLF_ACTIVATE);

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Cria o arquivo do banco e aplica as migrações pendentes ANTES de qualquer
        // repositório rodar. Sem banco não há app: falhar aqui encerra em vez de abrir uma
        // janela que dá erro em toda tela.
        try
        {
            DatabaseMigrator.Migrar(Services.GetRequiredService<SqliteContext>());
        }
        catch (Exception ex)
        {
            LogErro(ex);
            MessageBox.Show(
                "Não foi possível abrir o banco de dados do MacroHelper.\n\n" +
                $"Arquivo: {SqliteContext.CaminhoPadrao}\n\n" +
                "Verifique se o arquivo não está aberto por outro programa e tente novamente. " +
                "Os detalhes técnicos foram gravados em erros.log.",
                "SK MacroHelper", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        // Conecta ThemeService ao Settings
        var theme = Services.GetRequiredService<ThemeService>();
        theme.LerPreferencia    = () => Settings.Default.Tema ?? "Sistema";
        theme.SalvarPreferencia = v => { Settings.Default.Tema = v; Settings.Default.Save(); };
        theme.CarregarPreferencia();
        if (!string.IsNullOrWhiteSpace(Settings.Default.CorAccent))
            theme.AplicarCorAccent(Settings.Default.CorAccent);

        // Manutenção de segundo plano: não segura a abertura da janela e uma falha aqui
        // não deve impedir o app de subir.
        _ = LimparHistoricoAntigoAsync();

        Services.GetRequiredService<MainWindow>().Show();
    }

    private static async Task LimparHistoricoAntigoAsync()
    {
        try { await Services.GetRequiredService<LogUsoService>().LimparHistoricoAntigoAsync(); }
        catch (Exception ex) { LogErro(ex); }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogErro(e.Exception);
        MessageBox.Show(
            "Ocorreu um erro inesperado e a operação foi cancelada.\n\n" +
            "O MacroHelper continuará aberto. Tente novamente; se o problema persistir, reinicie o aplicativo.\n\n" +
            "Os detalhes técnicos foram salvos em um log para análise.",
            "SK MacroHelper",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    /// <summary>Grava a exceção em %AppData%\MacroHelper\erros.log. Público para que qualquer
    /// catch do app registre o erro em vez de engoli-lo em silêncio.</summary>
    public static void LogErro(Exception? ex)
    {
        try
        {
            var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MacroHelper");
            Directory.CreateDirectory(pasta);
            File.AppendAllText(Path.Combine(pasta, "erros.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Se nem o log puder ser escrito, não há nada mais a fazer aqui.
        }
    }

    private static void ConfigureServices(IServiceCollection s)
    {
        // Infra — um arquivo SQLite em %AppData%\MacroHelper, sem rede e sem credencial.
        s.AddSingleton(_ => new SqliteContext());
        s.AddSingleton<MacroRepository>();
        s.AddSingleton<CategoriaRepository>();
        s.AddSingleton<LogUsoRepository>();
        s.AddSingleton<MacroVersaoRepository>();
        s.AddSingleton<VariavelGlobalRepository>();
        s.AddSingleton<TarefaRepository>();
        s.AddSingleton<CompromissoRepository>();
        s.AddSingleton<NotaRepository>();
        s.AddSingleton<ClipboardRepository>();

        // Services
        s.AddSingleton<IRelogio, RelogioDoSistema>();
        s.AddSingleton<MacroService>();
        s.AddSingleton<CategoriaService>();
        s.AddSingleton<LogUsoService>();
        s.AddSingleton<VariavelGlobalService>();
        s.AddSingleton<TarefaService>();
        s.AddSingleton<CompromissoService>();
        s.AddSingleton<NotaService>();
        s.AddSingleton<ClipboardService>();
        s.AddSingleton<ThemeService>();
        s.AddSingleton<TextInsertionService>();
        s.AddSingleton<KeyboardHookService>();
        s.AddSingleton<HealthService>();
        s.AddSingleton<HotkeyService>();
        s.AddSingleton<ClipboardMonitorService>();
        // Na DI junto com o HotkeyService: a tela de Configurações também precisa dele,
        // para o botão "Testar notificação".
        s.AddSingleton<TrayService>();

        // Caminho único de inserção, compartilhado pelo popup do gatilho, pela busca rápida e
        // pelo Ctrl+Alt+N. É aqui — e só aqui — que o serviço aprende a abrir uma janela WPF:
        // a camada de Services não conhece WPF, então a UI injeta o "como perguntar".
        s.AddSingleton(sp => new InsercaoDeMacroService(
            sp.GetRequiredService<MacroService>(),
            sp.GetRequiredService<TextInsertionService>(),
            sp.GetRequiredService<LogUsoService>())
        {
            ObterNomeUsuario   = () => NomeDoUsuario,
            PerguntarVariaveis = (conteudo, variaveis) =>
            {
                var janela = new Views.VariaveisWindow(conteudo, variaveis);
                return janela.ShowDialog() == true ? janela.ConteudoFinal : null;
            },
        });

        // O Dispatcher vai explícito: é o da thread de UI, capturado aqui, no startup.
        // O construtor sem parâmetro do DispatcherTimer amarraria em Dispatcher.CurrentDispatcher,
        // e se o contêiner resolvesse este serviço a partir de outra thread o Tick nunca
        // dispararia — sem erro, sem log, só lembretes que nunca avisam.
        s.AddSingleton(sp => new LembreteService(
            sp.GetRequiredService<TarefaService>(),
            sp.GetRequiredService<CompromissoService>(),
            sp.GetRequiredService<IRelogio>(),
            Current.Dispatcher)
        {
            AoFalhar = LogErro,
        });

        // ViewModels
        s.AddTransient<ViewModels.MainViewModel>();
        s.AddTransient<ViewModels.MacrosViewModel>();
        s.AddTransient<ViewModels.ConfiguracoesViewModel>();
        s.AddTransient<ViewModels.CategoriasViewModel>();
        s.AddTransient<ViewModels.DashboardViewModel>();
        s.AddTransient<ViewModels.VariaveisGlobaisViewModel>();
        s.AddTransient<ViewModels.TarefasViewModel>();
        s.AddTransient<ViewModels.CompromissosViewModel>();
        s.AddTransient<ViewModels.NotasViewModel>();
        s.AddTransient<ViewModels.AjudaViewModel>();
        s.AddTransient<ViewModels.ManualSqlViewModel>();

        // Views
        s.AddTransient<MainWindow>();
    }
}
