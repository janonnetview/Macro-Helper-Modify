using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MacroHelper.UI;

public class TrayService : IDisposable
{
    private NotifyIcon? _tray;
    private bool        _hookAtivo = true;

    public event Action?        AbrirJanela;
    public event Action?        AlternarHook;
    public event Action?        AbrirBuscadorRapido;
    public event Action?        Sair;

    /// <summary>
    /// O que o clique no balão que está na tela deve fazer.
    ///
    /// Guardado JUNTO com o balão, e não num evento único, porque o app mostra balões de
    /// coisas diferentes: lembrete de tarefa, sugestão de macro, aviso de que minimizou. Com
    /// um evento só, quem escutava tinha de adivinhar qual era o último — e adivinhava errado:
    /// clicar num lembrete caía no caminho da sugestão de macro, que saía pela primeira linha
    /// por não haver frase nenhuma sugerida. O balão mais importante do app era clicável e não
    /// fazia nada.
    ///
    /// Cada balão declara a própria ação ao ser mostrado. Balão sem ação — o "minimizei para a
    /// bandeja" — não faz nada ao ser clicado, que é o certo.
    /// </summary>
    private Action? _aoClicarNoBalao;

    public void Iniciar(string nomeUsuario)
    {
        _tray = new NotifyIcon
        {
            Text    = $"SK MacroHelper · {nomeUsuario}",
            Visible = true,
            Icon    = CriarIconeSK()
        };

        _tray.DoubleClick += (_, _) => AbrirJanela?.Invoke();
        _tray.BalloonTipClicked += (_, _) =>
        {
            // Consumida no clique: o balão some depois de clicado, e deixar a ação armada
            // faria o balão SEGUINTE — que pode não ter ação nenhuma — herdar esta.
            var acao = _aoClicarNoBalao;
            _aoClicarNoBalao = null;
            acao?.Invoke();
        };
        _tray.ContextMenuStrip = CriarMenu();
    }

    public void AtualizarStatus(bool hookAtivo)
    {
        _hookAtivo = hookAtivo;
        if (_tray == null) return;
        _tray.Text = $"SK MacroHelper · Hook {(hookAtivo ? "ativo" : "pausado")}";
        // Rebuild menu
        _tray.ContextMenuStrip?.Dispose();
        _tray.ContextMenuStrip = CriarMenu();
    }

    /// <summary>
    /// Mostra um balão na bandeja.
    ///
    /// O NotifyIcon é um controle WinForms criado na thread de UI, e chamar ShowBalloonTip de
    /// outra thread lança InvalidOperationException. Hoje todo chamador já está na thread certa,
    /// mas essa é uma garantia por disciplina: qualquer timer, Task ou callback futuro a quebra
    /// em silêncio. Estas cinco linhas fecham a classe de bug para sempre, aqui e não em cada
    /// ponto de chamada.
    ///
    /// Aviso de plataforma: no Windows 10/11 o balão vira um toast do sistema e obedece ao
    /// Assistente de Foco. Com as notificações do app desligadas, NADA aparece e NENHUM erro é
    /// lançado — daí existir o botão "Testar notificação" em Configurações.
    /// </summary>
    /// <param name="aoClicar">
    /// O que fazer se a pessoa clicar neste balão. Null para os balões que só informam.
    /// </param>
    public void MostrarNotificacao(string titulo, string mensagem,
        ToolTipIcon icone = ToolTipIcon.Info, int ms = 2000, Action? aoClicar = null)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;

        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => MostrarNotificacao(titulo, mensagem, icone, ms, aoClicar));
            return;
        }

        _aoClicarNoBalao = aoClicar;
        _tray?.ShowBalloonTip(ms, titulo, mensagem, icone);
    }

    private ContextMenuStrip CriarMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Renderer = new FlatMenuRenderer();

        // Logo / título (desativado)
        var titulo = new ToolStripLabel("SK MacroHelper")
        {
            Font      = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(162, 155, 254),
            Enabled   = false
        };
        menu.Items.Add(titulo);
        menu.Items.Add(new ToolStripSeparator());

        var corIcone = Color.FromArgb(200, 200, 210);

        // Abrir janela
        var abrir = new ToolStripMenuItem("Abrir janela principal  (Ctrl+Shift+Espaço)") { Image = CriarIconeGlyph("", corIcone) };
        abrir.Click += (_, _) => AbrirJanela?.Invoke();
        menu.Items.Add(abrir);

        // Busca rápida
        var busca = new ToolStripMenuItem("Busca rápida  (Ctrl+Espaço)") { Image = CriarIconeGlyph("", corIcone) };
        busca.Click += (_, _) => AbrirBuscadorRapido?.Invoke();
        menu.Items.Add(busca);

        menu.Items.Add(new ToolStripSeparator());

        // Toggle hook
        var hook = new ToolStripMenuItem(_hookAtivo ? "Pausar monitoramento" : "Retomar monitoramento")
        {
            Image = CriarIconeGlyph(_hookAtivo ? "" : "", corIcone)
        };
        hook.Click += (_, _) => AlternarHook?.Invoke();
        menu.Items.Add(hook);

        menu.Items.Add(new ToolStripSeparator());

        // Sair
        var sair = new ToolStripMenuItem("Sair") { Image = CriarIconeGlyph("", corIcone) };
        sair.Click += (_, _) => Sair?.Invoke();
        menu.Items.Add(sair);

        return menu;
    }

    private static Image CriarIconeGlyph(string glyph, Color cor, int tamanho = 16)
    {
        var bmp = new Bitmap(tamanho, tamanho);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode       = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint   = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var font  = new Font("Segoe MDL2 Assets", tamanho - 4f, FontStyle.Regular);
        using var brush = new SolidBrush(cor);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(glyph, font, brush, new RectangleF(0, 0, tamanho, tamanho), sf);
        return bmp;
    }

    private static Icon CriarIconeSK()
    {
        // Cria ícone SK programaticamente (16x16 roxo)
        using var bmp = new Bitmap(32, 32);
        using var g   = Graphics.FromImage(bmp);

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Fundo gradiente roxo
        using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
            new Rectangle(0, 0, 32, 32),
            Color.FromArgb(139, 92, 246),
            Color.FromArgb(108, 92, 231),
            45f);
        g.FillRoundedRectangle(brush, 1, 1, 30, 30, 7);

        // Texto SK
        using var font = new Font("Segoe UI", 11, FontStyle.Bold);
        var sf = new StringFormat
        {
            Alignment     = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString("SK", font, System.Drawing.Brushes.White,
            new RectangleF(0, 0, 32, 32), sf);

        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _tray?.Dispose();
        GC.SuppressFinalize(this);
    }
}

// Renderer para menu escuro
internal class FlatMenuRenderer : ToolStripProfessionalRenderer
{
    public FlatMenuRenderer() : base(new FlatColorTable()) { }
}

internal class FlatColorTable : ProfessionalColorTable
{
    private static readonly Color _bg   = Color.FromArgb(26, 26, 36);
    private static readonly Color _hover = Color.FromArgb(30, 28, 53);
    private static readonly Color _border = Color.FromArgb(46, 46, 62);

    public override Color MenuItemSelected            => _hover;
    public override Color MenuItemBorder              => _border;
    public override Color MenuBorder                  => _border;
    public override Color ToolStripDropDownBackground => _bg;
    public override Color ImageMarginGradientBegin    => _bg;
    public override Color ImageMarginGradientMiddle   => _bg;
    public override Color ImageMarginGradientEnd      => _bg;
    public override Color MenuItemSelectedGradientBegin => _hover;
    public override Color MenuItemSelectedGradientEnd   => _hover;
    public override Color MenuItemPressedGradientBegin  => _hover;
    public override Color MenuItemPressedGradientEnd    => _hover;
    public override Color SeparatorDark                 => _border;
    public override Color SeparatorLight                => _border;
}

// Extension para DrawRoundedRectangle
internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, System.Drawing.Brush brush,
        int x, int y, int w, int h, int r)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(x, y, r * 2, r * 2, 180, 90);
        path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
        path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
        path.CloseAllFigures();
        g.FillPath(brush, path);
    }
}
