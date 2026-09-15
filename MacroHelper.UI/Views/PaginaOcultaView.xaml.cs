using System.Windows;
using System.Windows.Media;

namespace MacroHelper.UI.Views;

/// <summary>
/// O Spotify dentro do SK MacroHelper, numa página que a interface não anuncia.
///
/// Regras de vida desta classe, todas ligadas ao pedido de "voltar de onde parou":
///
/// 1. O controle é criado UMA vez, junto com a janela principal, e nunca é recriado. Sair da
///    página é <c>Visibility</c>, nunca descartar: recriar recarregaria a página, e a música
///    pararia toda vez que se fosse ver uma tarefa.
/// 2. O navegador só é iniciado de verdade na primeira vez que a página é chamada. Quem nunca
///    usa o atalho não paga a abertura do WebView2 na inicialização do app.
/// 3. A sessão (o login) fica no mesmo perfil de disco que a página do Manual SQL já usa, ou
///    seja, sobrevive a fechar e abrir o app. O endereço em que se estava fica gravado nas
///    configurações e é para onde a página volta na próxima abertura.
/// </summary>
public partial class PaginaOcultaView : System.Windows.Controls.UserControl
{
    private const string EnderecoPadrao = "https://open.spotify.com/";

    /// <summary>O RadiusXl do painel de conteúdo (Styles/Tokens.xaml), que é onde esta página mora.</summary>
    private const double RaioDoPainel = 24;

    private bool _iniciando;
    private bool _pronta;

    /// <summary>Pedido de sair da página, vindo de dentro do navegador (o Ctrl+Shift+F lá de dentro).</summary>
    public event Action? SaidaSolicitada;

    public PaginaOcultaView()
    {
        InitializeComponent();
        SizeChanged += (_, tamanho) => RecortarCantosDeBaixo(tamanho.NewSize);
    }

    /// <summary>
    /// Recorta o pé da página na curva do painel de conteúdo. Sem isso, os dois cantos de baixo
    /// do app ficam quadrados só nesta página — ela vai até a borda, e o que desenha a curva ali
    /// é o Border do painel, que fica ATRÁS.
    ///
    /// A geometria é a união do retângulo de cantos arredondados (que curvaria os quatro) com a
    /// faixa de cima inteira, o que devolve os dois de cima retos. ClipToBounds não serviria:
    /// ele recorta em retângulo, e é justamente a curva que está em questão.
    ///
    /// Vale só para o que o WPF desenha, ou seja, o fundo desta página. O navegador é uma
    /// janela do Windows por cima, e janela do Windows só se recorta em retângulo: o pé
    /// dele é arredondado por dentro, pelo CSS de <see cref="EstiloDosCantos"/>.
    /// </summary>
    private void RecortarCantosDeBaixo(System.Windows.Size tamanho)
    {
        if (tamanho.Width <= 0 || tamanho.Height <= RaioDoPainel) { Clip = null; return; }

        var arredondado = new RectangleGeometry(
            new Rect(0, 0, tamanho.Width, tamanho.Height), RaioDoPainel, RaioDoPainel);
        var faixa = new RectangleGeometry(
            new Rect(0, 0, tamanho.Width, tamanho.Height - RaioDoPainel));

        Clip = new CombinedGeometry(GeometryCombineMode.Union, arredondado, faixa);
    }

    /// <summary>
    /// Mostra a página e, na primeira vez, inicia o navegador. Chamada a cada abertura: da
    /// segunda em diante ela só devolve o foco para o que já estava carregado.
    /// </summary>
    public async Task AbrirAsync()
    {
        if (_pronta) { Web.Focus(); return; }
        if (_iniciando) return;

        _iniciando = true;
        try
        {
            // Ambiente padrão, o mesmo da página do Manual SQL. Um segundo ambiente com pasta
            // de perfil própria significaria um segundo processo de navegador dentro do app,
            // e é a pasta padrão que já guarda o login entre uma sessão e outra.

            // Fundo transparente para o CSS dos cantos poder abrir buraco no pé da
            // página: onde ela deixa de pintar, aparece o painel de trás, que já é
            // arredondado.
            Web.DefaultBackgroundColor = System.Drawing.Color.Transparent;

            await Web.EnsureCoreWebView2Async();

            Web.CoreWebView2.Settings.AreDevToolsEnabled           = false;
            Web.CoreWebView2.Settings.IsStatusBarEnabled           = false;
            Web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

            // F12, Ctrl+P e companhia denunciariam que ali dentro mora um navegador.
            Web.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;

            // O Ctrl+Shift+F digitado com o foco dentro da página não chega ao WPF: quem o
            // enxerga é o Chromium. Sem esta ponte, a mesma tecla que abre a página deixaria
            // de fechá-la depois do primeiro clique lá dentro. O ouvinte é de captura, para
            // vir antes dos atalhos que a própria página registra.
            await Web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                """
                window.addEventListener('keydown', function (e) {
                    if (e.ctrlKey && e.shiftKey && !e.altKey && (e.key === 'f' || e.key === 'F')) {
                        e.preventDefault();
                        e.stopPropagation();
                        window.chrome.webview.postMessage('sair');
                    }
                }, true);
                """);

            await Web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(EstiloDosCantos);

            Web.CoreWebView2.WebMessageReceived += (_, mensagem) =>
            {
                if (mensagem.TryGetWebMessageAsString() == "sair") PedirSaida();
            };

            Web.Source     = new Uri(EnderecoGravado());
            Web.Visibility = Visibility.Visible;
            _pronta        = true;
            Web.Focus();
        }
        catch (Exception ex)
        {
            App.LogErro(ex);
            Recado.Text = "Não consegui abrir esta página. O componente WebView2 do Windows " +
                          "parece não estar instalado.";
        }
        finally { _iniciando = false; }
    }

    /// <summary>
    /// Arredonda o pé da página web por dentro. O recorte do WPF não chega ao navegador
    /// (ver <see cref="RecortarCantosDeBaixo"/>), então quem tira os dois cantos de baixo
    /// ali é o CSS.
    ///
    /// clip-path, e não overflow: overflow mexeria na rolagem da página. E o fundo do html
    /// e do body tem de ficar transparente porque o fundo do elemento raiz é pintado na tela
    /// inteira, fora do recorte, e continuaria quadrando os cantos.
    ///
    /// Só no documento de cima: dentro de um iframe não há canto nenhum para arredondar, e
    /// o fundo transparente abriria buraco no meio da página.
    /// </summary>
    private static string EstiloDosCantos => $$"""
        if (window.top === window) {
            var estilo = document.createElement('style');
            estilo.textContent =
                'html, body { background: transparent !important; }' +
                'html { clip-path: inset(0 round 0 0 {{RaioDoPainel}}px {{RaioDoPainel}}px) !important; }';
            document.documentElement.appendChild(estilo);
        }
        """;

    /// <summary>
    /// Grava onde a pessoa estava. Chamado ao sair da página e ao fechar o app: o endereço é a
    /// única parte do estado que não sobrevive sozinha ao fechamento do processo.
    /// </summary>
    public void Gravar()
    {
        try
        {
            var endereco = Web.Source?.ToString();
            if (string.IsNullOrWhiteSpace(endereco) || !endereco.StartsWith("https://", StringComparison.Ordinal))
                return;

            Properties.Settings.Default.PaginaOcultaEndereco = endereco;
            Properties.Settings.Default.Save();
        }
        catch (Exception ex) { App.LogErro(ex); }
    }

    private static string EnderecoGravado()
    {
        try
        {
            var gravado = Properties.Settings.Default.PaginaOcultaEndereco;
            if (!string.IsNullOrWhiteSpace(gravado) &&
                Uri.TryCreate(gravado, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps &&
                uri.Host.EndsWith("spotify.com", StringComparison.OrdinalIgnoreCase))
                return gravado;
        }
        catch (Exception ex) { App.LogErro(ex); }

        return EnderecoPadrao;
    }

    /// <summary>Repassa para fora o pedido de sair da página feito de dentro do navegador.</summary>
    internal void PedirSaida() => SaidaSolicitada?.Invoke();
}
