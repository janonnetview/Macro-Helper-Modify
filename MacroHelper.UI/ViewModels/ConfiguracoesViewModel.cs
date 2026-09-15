using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroHelper.Services;
using MacroHelper.UI.Properties;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows.Input;
using HotkeyService = MacroHelper.UI.HotkeyService;

namespace MacroHelper.UI.ViewModels;

/// <summary>Registra (ou remove) o app na chave Run do registro do usuário, para iniciar junto com o Windows.</summary>
public static class IniciarComWindowsHelper
{
    private const string ChaveRegistro = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Precisa ser IDÊNTICO ao ValueName gravado por setup.iss — senão o instalador cria uma
    /// entrada e o toggle do app remove outra, deixando o app iniciando sozinho para sempre.
    /// </summary>
    public const string NomeValor = "SKMacroHelper";

    public static bool EstaAtivo()
    {
        using var chave = Registry.CurrentUser.OpenSubKey(ChaveRegistro, writable: false);
        return chave?.GetValue(NomeValor) != null;
    }

    public static void Definir(bool ativo)
    {
        using var chave = Registry.CurrentUser.OpenSubKey(ChaveRegistro, writable: true);
        if (chave == null) return;

        if (ativo)
        {
            var caminhoExe = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(caminhoExe))
                chave.SetValue(NomeValor, $"\"{caminhoExe}\"");
        }
        else
        {
            if (chave.GetValue(NomeValor) != null)
                chave.DeleteValue(NomeValor);
        }
    }
}

/// <summary>Uma bolinha da paleta: o nome que fica salvo e o hexadecimal que ela mostra agora.</summary>
public sealed record AmostraDeCor(string Nome, string Hex);

public partial class ConfiguracoesViewModel : ViewModelComMensagem
{
    private readonly ThemeService        _themeService;
    private readonly HealthService       _healthService;
    private readonly HotkeyService       _hotkeyService;
    private readonly KeyboardHookService _hookService;
    private readonly TrayService         _trayService;
    private readonly ClipboardMonitorService _clipboardMonitor;

    [ObservableProperty] private string _seuNome              = string.Empty;
    [ObservableProperty] private string _temaSelecionado      = "Sistema";
    [ObservableProperty] private bool   _iniciarComWindows    = false;
    [ObservableProperty] private bool   _minimizarParaBandeja = true;

    // Gatilho / digitação
    [ObservableProperty] private string  _gatilhoPrefixo      = "/";
    [ObservableProperty] private string  _appsModoDigitacao   = string.Empty;
    [ObservableProperty] private bool    _sugestaoProativaIA  = true;
    [ObservableProperty] private bool    _historicoClipboardAtivo = true;

    // Aparência avançada

    /// <summary>O NOME da cor escolhida. É o que fica salvo e o que acende o anel na bolinha.</summary>
    [ObservableProperty] private string _corAccentSelecionada = PaletaDeDestaque.NomePadrao;

    /// <summary>
    /// As bolinhas da paleta, já com a amostra do tema em uso.
    ///
    /// A lista é reconstruída quando o tema muda porque cada cor tem duas versões, e a bolinha
    /// tem de mostrar a que a pessoa vai ver de verdade — senão escolher "Azul" no tema escuro
    /// pinta o app de um azul que não é o da bolinha.
    /// </summary>
    public ObservableCollection<AmostraDeCor> CoresAccentDisponiveis { get; } = new();

    // Saúde do app
    [ObservableProperty] private int     _totalMacrosHealth   = 0;
    [ObservableProperty] private string  _memoriaTexto        = "—";
    [ObservableProperty] private string  _versaoAppTexto      = "—";
    [ObservableProperty] private bool    _carregandoSaude     = false;

    // Recursos da máquina local
    [ObservableProperty] private string  _cpuTexto            = "—";
    [ObservableProperty] private string  _memoriaSistemaTexto = "—";
    [ObservableProperty] private string  _discoTexto          = "—";

    // Atalhos de teclado remapeáveis
    [ObservableProperty] private string  _atalhoBuscaTexto    = string.Empty;
    [ObservableProperty] private string  _atalhoRepetirTexto  = string.Empty;
    [ObservableProperty] private string? _capturandoAtalho    = null;

    public ConfiguracoesViewModel(ThemeService themeService, HealthService healthService,
        HotkeyService hotkeyService, KeyboardHookService hookService, TrayService trayService,
        ClipboardMonitorService clipboardMonitor)
    {
        _themeService     = themeService;
        _healthService    = healthService;
        _hotkeyService    = hotkeyService;
        _hookService      = hookService;
        _trayService      = trayService;
        _clipboardMonitor = clipboardMonitor;

        TemaSelecionado = themeService.TemaAtual.ToString();

        try
        {
            SeuNome              = Settings.Default.SeuNome ?? string.Empty;
            IniciarComWindows    = IniciarComWindowsHelper.EstaAtivo();
            MinimizarParaBandeja = Settings.Default.MinimizarParaBandeja;

            GatilhoPrefixo     = string.IsNullOrEmpty(Settings.Default.GatilhoPrefixo) ? "/" : Settings.Default.GatilhoPrefixo;
            AppsModoDigitacao  = Settings.Default.AppsModoDigitacao ?? string.Empty;
            SugestaoProativaIA      = Settings.Default.SugestaoProativaIA;
            HistoricoClipboardAtivo = Settings.Default.HistoricoClipboardAtivo;

            CorAccentSelecionada = PaletaDeDestaque.Resolver(Settings.Default.CorAccent).Nome;
        }
        catch (Exception ex) { App.LogErro(ex); }

        AtualizarTextosAtalhos();
        MontarPaleta();
        _ = CarregarSaudeAsync();
    }

    // ── Atalhos de teclado remapeáveis ─────────────────────────
    private void AtualizarTextosAtalhos()
    {
        AtalhoBuscaTexto   = FormatarComboCompleto((ModifierKeys)Settings.Default.AtalhoBuscaModificador, Settings.Default.AtalhoBuscaTecla);
        AtalhoRepetirTexto = $"Ctrl + Alt + {FormatarTecla(Settings.Default.AtalhoRepetirVk)}";
    }

    private static string FormatarComboCompleto(ModifierKeys mods, int vk)
    {
        var partes = new List<string>();
        if (mods.HasFlag(ModifierKeys.Control)) partes.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt))     partes.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift))   partes.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Windows)) partes.Add("Win");
        partes.Add(FormatarTecla(vk));
        return string.Join(" + ", partes);
    }

    private static string FormatarTecla(int vk)
    {
        try
        {
            var key = KeyInterop.KeyFromVirtualKey(vk);
            return key switch
            {
                Key.Space => "Espaço",
                Key.D0 => "0", Key.D1 => "1", Key.D2 => "2", Key.D3 => "3", Key.D4 => "4",
                Key.D5 => "5", Key.D6 => "6", Key.D7 => "7", Key.D8 => "8", Key.D9 => "9",
                _ => key.ToString().ToUpperInvariant()
            };
        }
        catch { return "?"; }
    }

    [RelayCommand]
    public void IniciarCapturaAtalho(string qual) => CapturandoAtalho = qual;

    [RelayCommand]
    public void CancelarCapturaAtalho() => CapturandoAtalho = null;

    /// <summary>Chamado pelo code-behind da View quando uma tecla é pressionada durante a captura de um atalho.</summary>
    public void FinalizarCapturaAtalho(int vk, ModifierKeys modificadores)
    {
        var qual = CapturandoAtalho;
        CapturandoAtalho = null;
        if (qual == null) return;

        try
        {
            switch (qual)
            {
                case "busca":
                    if (modificadores == ModifierKeys.None)
                    {
                        MostrarMsg("Escolha uma combinação com Ctrl, Alt, Shift ou Win.", false);
                        return;
                    }
                    // Ctrl+Shift+Espaço já é a janela principal, e essa combinação é fixa.
                    // Sem esta checagem o registro falharia e a pessoa leria "em uso por outro
                    // programa", que é o próprio app.
                    if ((int)modificadores == HotkeyService.ModificadorJanelaPrincipal &&
                        vk == HotkeyService.TeclaJanelaPrincipal)
                    {
                        MostrarMsg("Essa combinação já abre a janela principal. Escolha outra.", false);
                        return;
                    }
                    if (!_hotkeyService.Reconfigurar((int)modificadores, vk))
                    {
                        MostrarMsg("Essa combinação já está em uso por outro programa. Escolha outra.", false);
                        return;
                    }
                    Settings.Default.AtalhoBuscaModificador = (int)modificadores;
                    Settings.Default.AtalhoBuscaTecla       = vk;
                    break;

                case "repetir":
                    _hookService.VkRepetirUltima = vk;
                    Settings.Default.AtalhoRepetirVk = vk;
                    break;
            }

            Settings.Default.Save();
            AtualizarTextosAtalhos();
            MostrarMsg("Atalho atualizado.", true);
        }
        catch (Exception ex) { App.LogErro(ex); MostrarMsg($"Erro: {ex.Message}", false); }
    }

    [RelayCommand]
    public void RestaurarAtalhosPadrao()
    {
        Settings.Default.AtalhoBuscaModificador = 2;
        Settings.Default.AtalhoBuscaTecla       = 0x20;
        Settings.Default.AtalhoRepetirVk        = 0x30;
        Settings.Default.Save();

        _hotkeyService.Reconfigurar(2, 0x20);
        _hookService.VkRepetirUltima = 0x30;

        AtualizarTextosAtalhos();
        MostrarMsg("Atalhos restaurados ao padrão.", true);
    }

    [RelayCommand]
    public async Task CarregarSaudeAsync()
    {
        CarregandoSaude = true;
        try
        {
            var info = await _healthService.ObterAsync();
            TotalMacrosHealth = info.TotalMacros;
            MemoriaTexto      = FormatarBytes(info.MemoriaProcessoBytes);
            VersaoAppTexto    = info.VersaoApp;

            CpuTexto            = $"{info.CpuUsoPercent:0.#}%";
            MemoriaSistemaTexto = info.MemoriaSistemaTotalBytes > 0
                ? $"{FormatarBytes(info.MemoriaSistemaUsadaBytes)} / {FormatarBytes(info.MemoriaSistemaTotalBytes)}"
                : "—";
            DiscoTexto          = info.DiscoTotalBytes > 0
                ? $"{FormatarBytes(info.DiscoTotalBytes - info.DiscoLivreBytes)} / {FormatarBytes(info.DiscoTotalBytes)}"
                : "—";
        }
        catch (Exception ex) { App.LogErro(ex); MostrarMsg($"Erro ao verificar saúde do app: {ex.Message}", false); }
        finally { CarregandoSaude = false; }
    }

    private static string FormatarBytes(long bytes)
    {
        double valor = bytes;
        string[] unidades = ["B", "KB", "MB", "GB"];
        var i = 0;
        while (valor >= 1024 && i < unidades.Length - 1) { valor /= 1024; i++; }
        return $"{valor:0.#} {unidades[i]}";
    }

    [RelayCommand]
    public void AplicarTema(string tema)
    {
        TemaSelecionado = tema;
        _themeService.AplicarTema(tema switch
        {
            "Claro"  => TemaApp.Claro,
            "Escuro" => TemaApp.Escuro,
            _        => TemaApp.Sistema
        });

        // As bolinhas mudam de tinta junto com o tema.
        MontarPaleta();
        MostrarMsg("Tema aplicado!", true);
    }

    private void MontarPaleta()
    {
        CoresAccentDisponiveis.Clear();
        foreach (var cor in PaletaDeDestaque.Cores)
            CoresAccentDisponiveis.Add(new AmostraDeCor(cor.Nome, cor.ParaTema(_themeService.EscuroAtivo)));
    }

    [RelayCommand]
    public void SalvarConfiguracoes()
    {
        try
        {
            IniciarComWindowsHelper.Definir(IniciarComWindows);
            Settings.Default.SeuNome              = SeuNome.Trim();
            Settings.Default.IniciarComWindows    = IniciarComWindows;
            Settings.Default.MinimizarParaBandeja = MinimizarParaBandeja;
            Settings.Default.Save();
            MostrarMsg("Configurações salvas!", true);
        }
        catch (Exception ex) { App.LogErro(ex); MostrarMsg("Erro ao salvar.", false); }
    }

    [RelayCommand]
    public void SalvarGatilho()
    {
        try
        {
            Settings.Default.GatilhoPrefixo          = string.IsNullOrWhiteSpace(GatilhoPrefixo) ? "/" : GatilhoPrefixo[..1];
            Settings.Default.AppsModoDigitacao       = AppsModoDigitacao;
            Settings.Default.SugestaoProativaIA      = SugestaoProativaIA;
            Settings.Default.HistoricoClipboardAtivo = HistoricoClipboardAtivo;
            Settings.Default.Save();

            // Os dois interruptores que mexem em CAPTURA valem na hora, e não no próximo
            // start: são justamente os que alguém desliga por causa do que está prestes a
            // digitar ou copiar. "Reinicie para aplicar" ali é a resposta errada.
            _hookService.DeteccaoFraseAtiva = SugestaoProativaIA;
            if (!SugestaoProativaIA) _hookService.EsquecerTudo();

            _clipboardMonitor.Ativo = HistoricoClipboardAtivo;

            // O prefixo do gatilho é o único que ainda espera o reinício: ele é lido uma vez
            // na montagem do hook, junto com o popup de sugestões.
            MostrarMsg("Configurações salvas! O prefixo do gatilho vale no próximo início do app.", true);
        }
        catch (Exception ex) { App.LogErro(ex); MostrarMsg("Erro ao salvar.", false); }
    }

    /// <summary>Recebe o NOME da cor, que é o que a paleta guarda e o que o tema traduz.</summary>
    [RelayCommand]
    public void AplicarCorAccent(string nome)
    {
        CorAccentSelecionada = nome;
        try
        {
            Settings.Default.CorAccent = nome;
            Settings.Default.Save();
            _themeService.AplicarCorAccent(nome);
            MostrarMsg($"Cor de destaque: {nome}.", true);
        }
        catch (Exception ex) { App.LogErro(ex); MostrarMsg($"Erro: {ex.Message}", false); }
    }

    /// <summary>
    /// Único jeito de descobrir se as notificações realmente chegam. No Windows 10/11 o balão
    /// da bandeja vira um toast do sistema e obedece ao Assistente de Foco: com as notificações
    /// do app desligadas nas configurações do Windows, NADA aparece e NENHUM erro é lançado —
    /// os lembretes simplesmente não avisam, sem sintoma nenhum de que algo está errado.
    /// </summary>
    [RelayCommand]
    public void TestarNotificacao()
    {
        try
        {
            _trayService.MostrarNotificacao(
                "Notificação de teste",
                "Se você está lendo isto, os lembretes de tarefas vão funcionar.",
                System.Windows.Forms.ToolTipIcon.Info, 6000);

            MostrarMsg("Enviada. Se nada apareceu, veja as notificações do SK MacroHelper " +
                       "em Configurações do Windows › Sistema › Notificações.", true);
        }
        catch (Exception ex)
        {
            App.LogErro(ex);
            MostrarMsg($"Não foi possível enviar: {ex.Message}", false);
        }
    }

    [RelayCommand]
    public void RefazerTour()
    {
        try
        {
            Settings.Default.TourConcluido = false;
            Settings.Default.Save();
            MostrarMsg("O tour será exibido na próxima abertura do app.", true);
        }
        catch (Exception ex) { App.LogErro(ex); MostrarMsg("Erro ao salvar.", false); }
    }

}
