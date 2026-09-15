using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;

namespace MacroHelper.Services;

public enum TemaApp { Sistema, Claro, Escuro }

public class ThemeService
{
    public TemaApp TemaAtual { get; private set; } = TemaApp.Sistema;
    public Func<string>?   LerPreferencia    { get; set; }
    public Action<string>? SalvarPreferencia { get; set; }

    /// <summary>
    /// Se o tema em vigor é o escuro. É o tema RESOLVIDO, e não o escolhido: em "Sistema" quem
    /// decide é o Windows, e a cor de destaque precisa saber qual das duas versões usar.
    /// </summary>
    public bool EscuroAtivo { get; private set; }

    public CorDeDestaque CorAtual { get; private set; } = PaletaDeDestaque.Padrao;

    public void AplicarTema(TemaApp tema)
    {
        TemaAtual   = tema;
        EscuroAtivo = tema == TemaApp.Escuro || (tema == TemaApp.Sistema && IsSystemDark());
        AplicarRecursos(EscuroAtivo);

        // O tema novo traz a cor de destaque de fábrica dele. Reaplicar a escolhida logo depois
        // é o que impede a troca de tema de desfazer a escolha da pessoa — e é também o que
        // troca a cor pela versão certa, já que cada uma tem uma para o claro e outra para o
        // escuro.
        AplicarCorAtual();

        SalvarPreferencia?.Invoke(tema.ToString());
    }

    /// <summary>
    /// Escolhe a cor de destaque. Recebe o NOME dela (o que fica salvo) e aceita também o
    /// hexadecimal solto que as versões antigas gravavam — quem resolve os dois formatos é
    /// <see cref="PaletaDeDestaque.Resolver"/>.
    /// </summary>
    public void AplicarCorAccent(string nomeOuHex)
    {
        CorAtual = PaletaDeDestaque.Resolver(nomeOuHex);
        AplicarCorAtual();
    }

    /// <summary>
    /// Escreve a cor em vigor sobre TODA a família de recursos que deriva dela. São nove
    /// entradas, e não quatro, porque a linguagem visual passou a usar o accent em mais papéis:
    /// o halo de foco, o tinte de fundo do item ativo, a borda do campo focado e — o mais
    /// importante — a cor do TEXTO ESCRITO EM CIMA do accent.
    ///
    /// Esse último é o que evita o bug clássico do seletor de cor: com um destaque claro
    /// (o verde da identidade, o âmbar), texto branco em cima do botão primário simplesmente
    /// some. Aqui a cor do texto é decidida pelo brilho da cor escolhida.
    ///
    /// Qual das duas versões da cor entra depende do tema em vigor, e é por isso que trocar de
    /// tema chama este método de novo.
    /// </summary>
    private void AplicarCorAtual()
    {
        try
        {
            var app = Application.Current;
            if (app == null) return;
            var cor = (Color)ColorConverter.ConvertFromString(CorAtual.ParaTema(EscuroAtivo));

            // A cor "crua" também entra: o brilho dos botões e os gradientes de marca leem
            // AccentColor, não AccentBrush.
            app.Resources["AccentColor"]             = cor;
            app.Resources["AccentBrush"]             = new SolidColorBrush(cor);
            app.Resources["AccentHoverBrush"]        = new SolidColorBrush(AjustarBrilho(cor, -20));
            app.Resources["AccentTextBrush"]         = new SolidColorBrush(CorParaEscrever(cor));
            app.Resources["AccentLightBrush"]        = new SolidColorBrush(Color.FromArgb(30,  cor.R, cor.G, cor.B));
            app.Resources["AccentSoftBrush"]         = new SolidColorBrush(Color.FromArgb(18,  cor.R, cor.G, cor.B));
            app.Resources["AccentGlowBrush"]         = new SolidColorBrush(Color.FromArgb(89,  cor.R, cor.G, cor.B));
            app.Resources["AccentForegroundBrush"]   = new SolidColorBrush(CorDeTextoSobre(cor));
            app.Resources["InputFocusBorderBrush"]   = new SolidColorBrush(cor);
        }
        catch { /* cor inválida — ignora */ }
    }

    /// <summary>
    /// A versão da cor que serve para ESCREVER, e não para preencher.
    ///
    /// No tema claro elas não são a mesma: a cor que preenche um botão precisa de corpo, e a
    /// mesma tinta virando texto de 11px sobre branco fica ilegível. Aqui ela é escurecida até
    /// um brilho que se lê. No tema escuro o fundo é que é escuro, e a cor da paleta já foi
    /// escolhida clara o bastante — mexer nela ali só tiraria saturação à toa.
    /// </summary>
    private Color CorParaEscrever(Color c)
    {
        const double BrilhoMaximoNoClaro = 120;

        var brilho = Brilho(c);
        if (EscuroAtivo || brilho <= BrilhoMaximoNoClaro) return c;

        var fator = BrilhoMaximoNoClaro / brilho;
        return Color.FromRgb(Escurecer(c.R, fator), Escurecer(c.G, fator), Escurecer(c.B, fator));

        static byte Escurecer(byte v, double fator) => (byte)Math.Clamp(v * fator, 0, 255);
    }

    private static double Brilho(Color c) => (0.299 * c.R) + (0.587 * c.G) + (0.114 * c.B);

    private static Color AjustarBrilho(Color c, int delta)
    {
        byte Ajustar(byte v) => (byte)Math.Clamp(v + delta, 0, 255);
        return Color.FromRgb(Ajustar(c.R), Ajustar(c.G), Ajustar(c.B));
    }

    /// <summary>
    /// Preto ou branco sobre a cor dada, pelo brilho percebido (a fórmula ITU-R BT.601, que
    /// pesa o verde muito mais que o azul — é por isso que um verde claro pede texto escuro
    /// e um azul do mesmo "valor" ainda aceita branco).
    /// </summary>
    private static Color CorDeTextoSobre(Color c)
    {
        return Brilho(c) > 150
            ? Color.FromRgb(0x0A, 0x0F, 0x04)   // quase preto, com um resto de verde
            : Color.FromRgb(0xFF, 0xFF, 0xFF);
    }

    public void CarregarPreferencia()
    {
        var salvo = LerPreferencia?.Invoke() ?? "Sistema";
        var tema  = salvo switch
        {
            "Claro"  => TemaApp.Claro,
            "Escuro" => TemaApp.Escuro,
            _        => TemaApp.Sistema
        };
        AplicarTema(tema);
    }

    private static void AplicarRecursos(bool isDark)
    {
        var app = Application.Current;
        if (app == null) return;
        var uri = new Uri(isDark
            ? "/MacroHelper;component/Themes/Dark.xaml"
            : "/MacroHelper;component/Themes/Light.xaml",
            UriKind.RelativeOrAbsolute);
        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source?.OriginalString.Contains("/Themes/") == true);
        if (existing != null) app.Resources.MergedDictionaries.Remove(existing);
        app.Resources.MergedDictionaries.Insert(0, new ResourceDictionary { Source = uri });
    }

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }
}
