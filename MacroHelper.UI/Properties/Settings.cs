using System.Configuration;

namespace MacroHelper.UI.Properties;

internal sealed class Settings : ApplicationSettingsBase
{
    private static readonly Settings _default =
        (Settings)Synchronized(new Settings());
    public static Settings Default => _default;

    /// <summary>
    /// Substitui o nome que vinha da conta logada. Alimenta o placeholder {usuario} das macros,
    /// o cabeçalho do Início e o tooltip da bandeja.
    /// </summary>
    [UserScopedSetting, DefaultSettingValue("")]
    public string SeuNome
    {
        get => (string)(this["SeuNome"] ?? string.Empty);
        set => this["SeuNome"] = value;
    }

    [UserScopedSetting, DefaultSettingValue("Escuro")]
    public string Tema
    {
        get => (string)(this["Tema"] ?? "Escuro");
        set => this["Tema"] = value;
    }

    [UserScopedSetting, DefaultSettingValue("False")]
    public bool IniciarComWindows
    {
        get => (bool)(this["IniciarComWindows"] ?? false);
        set => this["IniciarComWindows"] = value;
    }

    [UserScopedSetting, DefaultSettingValue("True")]
    public bool MinimizarParaBandeja
    {
        get => (bool)(this["MinimizarParaBandeja"] ?? true);
        set => this["MinimizarParaBandeja"] = value;
    }

    [UserScopedSetting, DefaultSettingValue("/")]
    public string GatilhoPrefixo
    {
        get => (string)(this["GatilhoPrefixo"] ?? "/");
        set => this["GatilhoPrefixo"] = value;
    }

    [UserScopedSetting, DefaultSettingValue("")]
    public string? AppsModoDigitacao
    {
        get => this["AppsModoDigitacao"] as string;
        set => this["AppsModoDigitacao"] = value;
    }

    /// <summary>Liga a detecção de frases repetidas, que sugere transformar o texto em macro.</summary>
    [UserScopedSetting, DefaultSettingValue("True")]
    public bool SugestaoProativaIA
    {
        get => (bool)(this["SugestaoProativaIA"] ?? true);
        set => this["SugestaoProativaIA"] = value;
    }

    [UserScopedSetting]
    public DateTime UltimoArquivamentoLogs
    {
        get => this["UltimoArquivamentoLogs"] is DateTime d ? d : default;
        set => this["UltimoArquivamentoLogs"] = value;
    }

    /// <summary>
    /// Como a tela de Lembretes abre: "lista", "semana" ou "mes".
    ///
    /// Fica salvo porque é jeito de trabalhar, e não escolha de momento: quem enxerga a semana
    /// em grade não quer reescolher isso toda vez que abre o app.
    /// </summary>
    [UserScopedSetting, DefaultSettingValue("lista")]
    public string ModoAgenda
    {
        get => (string)(this["ModoAgenda"] ?? "lista");
        set => this["ModoAgenda"] = value;
    }

    /// <summary>
    /// O NOME da cor de destaque, como está em <c>PaletaDeDestaque</c> — e não o hexadecimal,
    /// que agora é um por tema. As instalações antigas guardaram um hexadecimal aqui; ler isso
    /// continua funcionando, ver <c>PaletaDeDestaque.Resolver</c>.
    /// </summary>
    [UserScopedSetting, DefaultSettingValue("Verde sálvia")]
    public string CorAccent
    {
        get => (string)(this["CorAccent"] ?? "Verde sálvia");
        set => this["CorAccent"] = value;
    }

    /// <summary>
    /// Liga a captura do histórico da área de transferência.
    ///
    /// Ligado por padrão: é a funcionalidade da aba "Copiados", e desligada a aba fica vazia
    /// sem explicação. Quem desliga continua com o que já foi guardado — o desligamento para
    /// de capturar, não apaga; para apagar existe o "Limpar" da própria aba.
    /// </summary>
    [UserScopedSetting, DefaultSettingValue("True")]
    public bool HistoricoClipboardAtivo
    {
        get => (bool)(this["HistoricoClipboardAtivo"] ?? true);
        set => this["HistoricoClipboardAtivo"] = value;
    }

    [UserScopedSetting, DefaultSettingValue("False")]
    public bool TourConcluido
    {
        get => (bool)(this["TourConcluido"] ?? false);
        set => this["TourConcluido"] = value;
    }

    [UserScopedSetting, DefaultSettingValue("2")]
    public int AtalhoBuscaModificador
    {
        get => (int)(this["AtalhoBuscaModificador"] ?? 2);
        set => this["AtalhoBuscaModificador"] = value;
    }

    [UserScopedSetting, DefaultSettingValue("32")]
    public int AtalhoBuscaTecla
    {
        get => (int)(this["AtalhoBuscaTecla"] ?? 32);
        set => this["AtalhoBuscaTecla"] = value;
    }

    [UserScopedSetting, DefaultSettingValue("48")]
    public int AtalhoRepetirVk
    {
        get => (int)(this["AtalhoRepetirVk"] ?? 48);
        set => this["AtalhoRepetirVk"] = value;
    }

    /// <summary>
    /// O endereço em que a página do Ctrl+Shift+F estava quando se saiu dela. É a única parte
    /// do estado dessa página que não sobrevive sozinha ao fechamento do app: o login e o
    /// resto ficam no perfil do WebView2, em disco.
    /// </summary>
    [UserScopedSetting, DefaultSettingValue("")]
    public string PaginaOcultaEndereco
    {
        get => (string)(this["PaginaOcultaEndereco"] ?? string.Empty);
        set => this["PaginaOcultaEndereco"] = value;
    }
}
