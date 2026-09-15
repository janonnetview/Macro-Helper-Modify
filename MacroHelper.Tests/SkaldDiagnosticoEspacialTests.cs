using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using MacroHelper.UI.Views;

namespace MacroHelper.Tests;

public sealed class SkaldDiagnosticoEspacialTests
{
    private static readonly string Pasta = Environment.GetEnvironmentVariable("SKALD_EVIDENCIAS_DIR")
        ?? Path.Combine(Path.GetTempPath(), "MacroHelper-Skald", Guid.NewGuid().ToString("N"));

    public static IEnumerable<object[]> Cenarios()
    {
        foreach (var tela in new[] { "Configuracoes", "Notas", "Categorias", "Dashboard" })
        foreach (var largura in new[] { 1200, 940 })
        foreach (var tema in new[] { "Light", "Dark" })
        foreach (var quantidade in tela is "Notas" or "Categorias" ? new[] { 0, 1, 18 } : new[] { 1, 18 })
            yield return new object[] { tela, largura, tema, quantidade };
    }

    [Theory]
    [MemberData(nameof(Cenarios))]
    public void Medir_e_renderizar_telas_reais(string tela, int largura, string tema, int quantidade) => TelaDeTeste.Executar(() =>
    {
        using var ensaio = new Ensaio(tela, largura, tema, quantidade);
        var id = $"{tela}-{largura}-{tema}-{quantidade:00}";
        ensaio.ValidarInputPassivo();
        if (ensaio.Mapa.Cabe) ensaio.ValidarPercurso();
        ensaio.Registrar(id);
        Assert.True(ensaio.Mapa.Elementos.Count > 2);
        Assert.Equal(940, ensaio.Minimo.Width);
        Assert.Equal(600, ensaio.Minimo.Height);
        Assert.Equal(largura, ensaio.Raiz.ActualWidth);
        Assert.Equal(largura == 1200 ? 760 : 600, ensaio.Raiz.ActualHeight);
        Assert.False(ensaio.Camada.IsHitTestVisible);
        Assert.False(ensaio.Camada.Focusable);

        // Evita um diagnóstico todo verde que só rejeita tudo por erro no carregamento.
        if (tela == "Notas" && quantidade == 1) Assert.True(ensaio.Mapa.Cabe, id);
        if (tela is "Notas" or "Categorias" && quantidade == 18) Assert.False(ensaio.Mapa.Cabe, id);

        if (quantidade == 18 || tela == "Configuracoes")
        {
            var scroll = ensaio.Scroll;
            var versao = ensaio.Camada.Versao;
            scroll.ScrollToEnd();
            ensaio.Estabilizar();
            Assert.True(scroll.VerticalOffset > 0);
            Assert.True(ensaio.Camada.Versao > versao);
            Assert.Null(ensaio.Camada.Corpo);
            ensaio.Remedir();
            ensaio.Registrar(id + "-rolado");
            if (tela is "Notas" or "Categorias") Assert.False(ensaio.Mapa.Cabe);
        }

        if (tela == "Notas" && largura == 1200 && tema == "Dark" && quantidade == 1)
            ensaio.SequenciaR01();
        Assert.All(ensaio.Dados.Comandos, c => Assert.Equal(0, c.Execucoes));
    });

    [Theory]
    [InlineData("Notas", 1200, "Light")]
    [InlineData("Notas", 940, "Dark")]
    [InlineData("Categorias", 1200, "Dark")]
    [InlineData("Categorias", 940, "Light")]
    public void Modal_input_lista_e_navegacao_retiraram_envelope(string tela, int largura, string tema) => TelaDeTeste.Executar(() =>
    {
        using var ensaio = new Ensaio(tela, largura, tema, 1);
        Assert.True(ensaio.Mapa.Cabe);
        var id = $"interrupcoes-{tela}-{largura}-{tema}";
        ensaio.Camada.Mostrar(ensaio.Mapa.Inicio, "entrada");
        ensaio.Registrar(id + "-00-ativo");
        var foco = Keyboard.FocusedElement;
        var eventos = 0;
        ensaio.Raiz.PreviewMouseDown += (_, _) => eventos++;
        ensaio.Raiz.PreviewKeyDown += (_, _) => eventos++;
        ensaio.Raiz.GotKeyboardFocus += (_, _) => eventos++;
        ensaio.ValidarPercurso();
        Assert.Same(foco, Keyboard.FocusedElement);
        Assert.Equal(0, eventos);
        var eventosNoPercurso = eventos;
        Assert.All(ensaio.Dados.Comandos, c => Assert.Equal(0, c.Execucoes));

        // Controle positivo: o botão REAL resolve o comando contado. Só o teste o executa.
        var botoes = SkaldMedidor.Descendentes(ensaio.Vista).OfType<ButtonBase>()
            .Where(b => b.Command is SkaldDadosDeDiagnostico.ComandoContado && b.IsVisible).ToArray();
        Assert.NotEmpty(botoes);
        foreach (var botao in botoes)
        {
            var r = SkaldMedidor.Visivel(botao, ensaio.Raiz);
            if (r.IsEmpty) continue;
            var ponto = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
            ensaio.Camada.Visibility = Visibility.Collapsed;
            var sem = ensaio.Raiz.InputHitTest(ponto);
            ensaio.Camada.Visibility = Visibility.Visible;
            ensaio.Raiz.UpdateLayout();
            var com = ensaio.Raiz.InputHitTest(ponto);
            Assert.NotNull(sem);
            Assert.Same(sem, com);
            Assert.True(DescendeDe(com as DependencyObject, botao));
        }
        var alvo = botoes.First();
        var comando = Assert.IsType<SkaldDadosDeDiagnostico.ComandoContado>(alvo.Command);
        // Calibra só o comando sintético, sem fabricar um clique do usuário.
        alvo.Command.Execute(alvo.CommandParameter);
        Assert.Equal(1, comando.Execucoes);
        var antes = ensaio.Dados.Comandos.Sum(c => c.Execucoes);

        ensaio.Dados.Valores[tela == "Notas" ? "MostrarEditor" : "MostrarFormulario"] = true;
        ensaio.Estabilizar();
        Assert.Null(ensaio.Camada.Corpo);
        var modal = SkaldMedidor.Descendentes(ensaio.Vista).OfType<Grid>()
            .First(g => Panel.GetZIndex(g) == 10);
        Assert.Equal(Visibility.Visible, modal.Visibility);
        ensaio.Registrar(id + "-01-modal-retirado");
        ensaio.Dados.Valores[tela == "Notas" ? "MostrarEditor" : "MostrarFormulario"] = false;
        ensaio.Estabilizar();
        Assert.Null(ensaio.Camada.Corpo);
        ensaio.Remedir();

        ensaio.Camada.Mostrar(ensaio.Mapa.Inicio, "antes-da-mensagem");
        ensaio.Dados.Valores["Mensagem"] = "Mensagem sintética para medição.";
        ensaio.Estabilizar();
        Assert.Null(ensaio.Camada.Corpo);
        ensaio.Remedir();
        ensaio.Registrar(id + "-02-mensagem");

        if (ensaio.Mapa.Cabe) ensaio.Camada.Mostrar(ensaio.Mapa.Inicio, "antes-da-lista");
        var extras = new SkaldDadosDeDiagnostico(tela, 18, tema);
        if (tela == "Notas") foreach (var nota in extras.Notas) ensaio.Dados.Notas.Add(nota);
        else foreach (var categoria in extras.Categorias) ensaio.Dados.Categorias.Add(categoria);
        ensaio.Estabilizar();
        Assert.Null(ensaio.Camada.Corpo);
        ensaio.Remedir();
        Assert.False(ensaio.Mapa.Cabe);
        ensaio.Registrar(id + "-03-lista-cresceu");

        ensaio.Hospedeiro.Content = new DashboardView { DataContext = extras.Contexto };
        ensaio.Estabilizar();
        Assert.Null(ensaio.Camada.Corpo);
        Assert.Null(ensaio.Camada.Mapa);
        ensaio.Registrar(id + "-04-navegacao");
        Assert.Equal(antes, ensaio.Dados.Comandos.Sum(c => c.Execucoes));
        SalvarJson(id + "-input", new { BotoesVerificados = botoes.Length, EventosDurantePercurso = eventosNoPercurso,
            ComandosAntesDoControlePositivo = 0, ComandosNoControlePositivo = antes,
            ComandosDepoisDaNavegacao = ensaio.Dados.Comandos.Sum(c => c.Execucoes),
            FocoPreservado = ReferenceEquals(foco, Keyboard.FocusedElement),
            Limite = "Hit test WPF isolado. Sem injeção de mouse ou teclado do Windows." });
    });

    [Fact]
    public void Resize_proximidade_digitacao_e_contexto_antigo_cancelam_sem_reinicio_automatico() => TelaDeTeste.Executar(() =>
    {
        using var ensaio = new Ensaio("Notas", 1200, "Dark", 1);
        ensaio.Camada.Mostrar(ensaio.Mapa.Inicio, "ativo");
        ensaio.Registrar("retirada-00-ativo-1200");
        ensaio.Dimensionar(940, 600);
        Assert.Null(ensaio.Camada.Corpo);
        ensaio.Remedir();
        ensaio.Registrar("retirada-01-minimo");
        ensaio.Camada.Mostrar(ensaio.Mapa.Inicio, "ativo-minimo");
        var versao = ensaio.Camada.Versao;
        ensaio.ObservarPonteiro(new Point(0, 0));
        Assert.NotNull(ensaio.Camada.Corpo);
        ensaio.ObservarPonteiro(ensaio.Mapa.Inicio.Location);
        ensaio.Registrar("retirada-02-proximidade");
        Assert.True(ensaio.Camada.Versao > versao);
        ensaio.Relogio.Avancar(TimeSpan.FromMilliseconds(499));
        Assert.False(ensaio.PodeIniciar(versao));
        ensaio.Relogio.Avancar(TimeSpan.FromSeconds(10));
        Assert.False(ensaio.PodeIniciar(versao));
        Assert.Null(ensaio.Camada.Corpo);
        ensaio.Remedir();
        ensaio.Camada.Mostrar(ensaio.Mapa.Inicio, "retomada-explicita");
        ensaio.Dados.Valores["TermoBusca"] = "texto sintético";
        ensaio.Estabilizar();
        Assert.Null(ensaio.Camada.Corpo);
        Assert.False(ensaio.PodeIniciar(ensaio.Camada.Versao));
        ensaio.Registrar("retirada-03-digitacao");
        ensaio.Relogio.Avancar(TimeSpan.FromSeconds(5));
        ensaio.Remedir();
        Assert.True(ensaio.PodeIniciar(ensaio.Camada.Versao));
        Assert.Null(ensaio.Camada.Corpo);
        ensaio.Dimensionar(1200, 760);
        ensaio.Remedir();
        ensaio.Registrar("retirada-04-restaurado");
        Assert.All(ensaio.Dados.Comandos, c => Assert.Equal(0, c.Execucoes));
    });

    [Fact]
    public void Geometria_rejeita_obstaculo_recorte_e_tamanho_excessivo()
    {
        var mapa = new SkaldMapa("ensaio", new Rect(0, 0, 400, 300), new Rect(20, 20, 200, 160),
            Array.Empty<SkaldMedida>(), new[] { new Rect(150, 40, 30, 30) });
        Assert.True(mapa.Permite(new Rect(30, 90, 64, 64)));
        Assert.False(mapa.Permite(new Rect(140, 35, 64, 64)));
        Assert.False(mapa.Permite(new Rect(15, 90, 64, 64)));
        Assert.False(mapa.Permite(new Rect(30, 30, 200, 200)));
    }

    private static bool DescendeDe(DependencyObject? elemento, DependencyObject ancestral)
    {
        while (elemento != null)
        {
            if (ReferenceEquals(elemento, ancestral)) return true;
            elemento = elemento is FrameworkContentElement conteudo ? conteudo.Parent : VisualTreeHelper.GetParent(elemento);
        }
        return false;
    }

    private static void SalvarJson(string nome, object dados)
    {
        Directory.CreateDirectory(Pasta);
        File.WriteAllText(Path.Combine(Pasta, nome + ".json"), JsonSerializer.Serialize(dados,
            new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
    }

    private sealed class Ensaio : IDisposable
    {
        private readonly ResourceDictionary recursosAnteriores = Application.Current.Resources;
        private readonly ShutdownMode modoAnterior = Application.Current.ShutdownMode;
        private readonly Window janela;
        private readonly HwndSource superficie;
        private readonly DependencyPropertyDescriptor descritor;
        private readonly string tela;
        private readonly string tema;
        private readonly int quantidade;
        private DateTime ultimaInvalidacao = SkaldDadosDeDiagnostico.Data;
        private DateTime ultimaDigitacao = DateTime.MinValue;
        private int pontosDeInputVerificados;
        public RelogioFalso Relogio { get; } = new(SkaldDadosDeDiagnostico.Data);
        public Grid Raiz { get; }
        public FrameworkElement Vista { get; }
        public ContentControl Hospedeiro { get; }
        public SkaldDadosDeDiagnostico Dados { get; }
        public SkaldCamadaDiagnostica Camada { get; } = new();
        public Size Minimo { get; }
        public SkaldMapa Mapa { get; private set; } = null!;
        public ScrollViewer Scroll { get; }

        public Ensaio(string tela, int largura, string tema, int quantidade)
        {
            this.tela = tela;
            this.tema = tema;
            this.quantidade = quantidade;
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Application.Current.Resources = Xaml.RecursosDoApp(tema);
            var doc = XDocument.Parse(Xaml.Ler("Views/MainWindow.xaml"));
            var nsX = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
            doc.Root!.Attribute(nsX + "Class")!.Remove();
            doc.Root.Attribute("Icon")!.Remove();
            foreach (var atributo in doc.Descendants().Attributes().Where(a =>
                         a.Name.LocalName is "Activated" or "Click" or "MouseLeftButtonDown" or "PreviewMouseDown").ToArray()) atributo.Remove();
            doc.Descendants().Single(e => e.Name.LocalName == "PaginaOcultaView").Remove();
            var texto = Regex.Replace(doc.ToString(), "clr-namespace:MacroHelper.UI.([A-Za-z]+)", "clr-namespace:MacroHelper.UI.$1;assembly=MacroHelper");
            janela = (Window)XamlReader.Parse(texto);
            Minimo = new Size(janela.MinWidth, janela.MinHeight);
            Raiz = (Grid)janela.Content;
            janela.Content = null;
            Raiz.UseLayoutRounding = janela.UseLayoutRounding;
            Raiz.Language = janela.Language;
            TextOptions.SetTextFormattingMode(Raiz, TextOptions.GetTextFormattingMode(janela));
            TextOptions.SetTextRenderingMode(Raiz, TextOptions.GetTextRenderingMode(janela));
            Raiz.Resources.MergedDictionaries.Add(janela.Resources);
            Dados = new(tela, quantidade, tema);
            Raiz.DataContext = Dados.Contexto;
            Vista = tela switch {
                "Configuracoes" => new ConfiguracoesView(), "Notas" => new NotasView(),
                "Categorias" => new CategoriasView(), "Dashboard" => new DashboardView(),
                _ => throw new ArgumentException("Tela não prevista", nameof(tela)) };
            Vista.DataContext = Dados.Contexto;
            Hospedeiro = SkaldMedidor.Descendentes(Raiz).OfType<ContentControl>().Single(c =>
                BindingOperations.GetBinding(c, ContentControl.ContentProperty)?.Path.Path == "CurrentView");
            Hospedeiro.Content = Vista;
            Panel.SetZIndex(Camada, 10000);
            Raiz.Children.Add(Camada);
            // Uma fonte oculta dá IsVisible/Loaded reais à árvore sem exibir/ativar uma janela.
            // Sem WS_VISIBLE; WS_EX_NOACTIVATE e WS_EX_TOOLWINDOW, sem entrada na barra de tarefas.
            superficie = new HwndSource(new HwndSourceParameters("SKald diagnóstico isolado") {
                Width = largura, Height = largura == 1200 ? 760 : 600,
                WindowStyle = unchecked((int)0x80000000), ExtendedWindowStyle = 0x08000080 });
            superficie.RootVisual = Raiz;
            Dimensionar(largura, largura == 1200 ? 760 : 600);
            Scroll = SkaldMedidor.RolagemDaPagina(Vista);
            Remedir();
            Scroll.ScrollChanged += Rolou;
            Raiz.SizeChanged += Redimensionou;
            ((INotifyPropertyChanged)Dados.Contexto).PropertyChanged += ContextoMudou;
            Dados.Notas.CollectionChanged += ListaMudou;
            Dados.Categorias.CollectionChanged += ListaMudou;
            descritor = DependencyPropertyDescriptor.FromProperty(ContentControl.ContentProperty, typeof(ContentControl));
            descritor.AddValueChanged(Hospedeiro, Navegou);
        }

        private void Invalidar(string motivo)
        {
            ultimaInvalidacao = Relogio.Agora;
            Camada.Retirar(motivo);
            Camada.Mapa = null;
        }
        private void Rolou(object sender, ScrollChangedEventArgs e) => Invalidar("rolagem-ou-extensao");
        private void Redimensionou(object sender, SizeChangedEventArgs e) => Invalidar("resize");
        private void ListaMudou(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Invalidar("lista-mudou");
        private void Navegou(object? sender, EventArgs e) { Invalidar("navegacao-C08"); Camada.Mapa = null; }
        private void ContextoMudou(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "TermoBusca") ultimaDigitacao = Relogio.Agora;
            if (e.PropertyName is "MostrarEditor" or "MostrarFormulario" or "Mensagem" or "TermoBusca") Invalidar(e.PropertyName);
        }
        public bool PodeIniciar(int versao) => Camada.Mapa != null && versao == Camada.Versao &&
            Relogio.Agora - ultimaInvalidacao >= TimeSpan.FromMilliseconds(500) &&
            Relogio.Agora - ultimaDigitacao >= TimeSpan.FromSeconds(5) &&
            !Equals(Dados.Valores["MostrarEditor"], true) && !Equals(Dados.Valores["MostrarFormulario"], true);

        public void ObservarPonteiro(Point ponto)
        {
            if (Camada.Corpo is not Rect corpo) return;
            corpo.Inflate(64, 64);
            if (corpo.Contains(ponto)) Invalidar("proximidade-sintetica-C07");
        }

        public void ValidarInputPassivo()
        {
            var alvos = SkaldMedidor.Descendentes(Vista).Where(e => e.IsVisible &&
                (e is ButtonBase or TextBoxBase or Selector || e.InputBindings.Count > 0));
            foreach (var alvo in alvos)
            {
                var r = SkaldMedidor.Visivel(alvo, Raiz);
                if (r.IsEmpty || r.Width <= 1 || r.Height <= 1) continue;
                var ponto = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
                Camada.Visibility = Visibility.Collapsed;
                var antes = Raiz.InputHitTest(ponto);
                Camada.Visibility = Visibility.Visible;
                Raiz.UpdateLayout();
                Assert.NotNull(antes);
                Assert.Same(antes, Raiz.InputHitTest(ponto));
                pontosDeInputVerificados++;
            }
            Assert.True(pontosDeInputVerificados > 0);
        }

        public void Dimensionar(double largura, double altura)
        {
            Raiz.Measure(new Size(largura, altura));
            Raiz.Arrange(new Rect(0, 0, largura, altura));
            Estabilizar();
        }
        public void Estabilizar()
        {
            Raiz.UpdateLayout();
            var tempo = Stopwatch.StartNew();
            do
            {
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.SystemIdle);
                Thread.Sleep(10);
            } while (tempo.ElapsedMilliseconds < 260);
            // Captura o layout assentado; o HWND oculto não tem cadência de composição de tela.
            // O ensaio não valida o storyboard de entrada da página.
            Vista.BeginAnimation(UIElement.OpacityProperty, null);
            Vista.Opacity = 1;
            if (Vista.RenderTransform is TranslateTransform slide)
            {
                slide.BeginAnimation(TranslateTransform.YProperty, null);
                slide.Y = 0;
            }
            Raiz.UpdateLayout();
        }
        public void Remedir() { Mapa = SkaldMedidor.Medir(tela, Vista, Raiz); Camada.Mapa = Mapa; Camada.InvalidateVisual(); }

        public void ValidarPercurso()
        {
            // A união retangular comprova TODO o segmento horizontal, além das amostras visuais.
            var varredura = Rect.Union(Mapa.Inicio, Mapa.Pausa);
            Assert.True(Mapa.Permite(varredura));
            var foco = Keyboard.FocusedElement;
            var antes = Dados.Comandos.Sum(c => c.Execucoes);
            for (var i = 0; i <= 60; i++)
            {
                var t = i / 60d;
                var corpo = new Rect(Mapa.Inicio.X + (Mapa.Pausa.X - Mapa.Inicio.X) * t, Mapa.Inicio.Y, 64, 64);
                Assert.True(Mapa.Permite(corpo));
                Camada.Mostrar(corpo, "caminho");
            }
            Assert.Same(foco, Keyboard.FocusedElement);
            Assert.Equal(antes, Dados.Comandos.Sum(c => c.Execucoes));
        }

        public void SequenciaR01()
        {
            Camada.Visibility = Visibility.Collapsed;
            Registrar("R01-referencia-sem-contornos");
            Camada.Visibility = Visibility.Visible;
            var quadros = new List<object>();
            var tempos = new[] { 0d, 1, 2, 4, 6, 9, 12, 14, 15, 16 };
            var fases = new[] { "antes", "entrada-local", "andar", "parar", "abrir-papel-proposto", "ler-proposto", "guardar-proposto", "sair", "fim-do-percurso", "oculto" };
            for (var i = 0; i < tempos.Length; i++)
            {
                Relogio.Agora = SkaldDadosDeDiagnostico.Data.AddSeconds(tempos[i]);
                var fator = tempos[i] < 4 ? Math.Clamp((tempos[i] - 1) / 3, 0, 1) : tempos[i] <= 12 ? 1 : Math.Clamp((15 - tempos[i]) / 3, 0, 1);
                if (i == 0 || i == tempos.Length - 1) Camada.Retirar(fases[i]);
                else Camada.Mostrar(new Rect(Mapa.Inicio.X + (Mapa.Pausa.X - Mapa.Inicio.X) * fator, Mapa.Inicio.Y, 64, 64), fases[i]);
                var nome = $"R01-{i:00}-{tempos[i]:00}s-{fases[i]}";
                Registrar(nome);
                quadros.Add(new { Arquivo = nome + ".png", Segundos = tempos[i], Fase = fases[i], Corpo = Camada.Corpo?.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            SalvarJson("R01-sequencia", new { Relogio = "virtual controlado, não reprodução em tempo real", Quadros = quadros });
        }

        public void Registrar(string nome)
        {
            Directory.CreateDirectory(Pasta);
            Raiz.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
            var bitmap = new RenderTargetBitmap((int)Raiz.ActualWidth, (int)Raiz.ActualHeight + 42, 96, 96, PixelFormats.Pbgra32);
            var telaBitmap = new RenderTargetBitmap((int)Raiz.ActualWidth, (int)Raiz.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            telaBitmap.Render(Raiz);
            var cena = new DrawingVisual();
            using (var dc = cena.RenderOpen())
            {
                dc.DrawRectangle(Brushes.WhiteSmoke, null, new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
                dc.DrawImage(telaBitmap, new Rect(0, 0, Raiz.ActualWidth, Raiz.ActualHeight));
                dc.DrawRectangle(Brushes.WhiteSmoke, null, new Rect(0, Raiz.ActualHeight, Raiz.ActualWidth, 42));
                dc.DrawText(new FormattedText($"DIAGNÓSTICO WPF • {nome} • {Camada.Fase}\nAzul: candidato | Vermelho: obstáculos | Amarelo: percurso/folga 8 | Retângulo 64 DIP, sem arte final",
                    System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 11, Brushes.Black, 1), new Point(10, Raiz.ActualHeight + 5));
            }
            bitmap.Render(cena);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var arquivo = File.Create(Path.Combine(Pasta, nome + ".png"))) encoder.Save(arquivo);
            string RectTexto(Rect r) => r.IsEmpty ? "vazio" : r.ToString(System.Globalization.CultureInfo.InvariantCulture);
            SalvarJson(nome, new { Tela = tela, Tema = tema, QuantidadeInicial = quantidade,
                Dimensoes = $"{Raiz.ActualWidth}x{Raiz.ActualHeight}", Minimo = Minimo.ToString(), DpiRenderizacao = 96,
                DpiArvore = VisualTreeHelper.GetDpi(Raiz).PixelsPerDip, Mapa.Id, Hospedeiro = RectTexto(Mapa.Hospedeiro),
                Candidato = RectTexto(Mapa.Candidato), Mapa.MaximoEstatico, Mapa.MaximoComPercurso, Mapa.Cabe,
                MapaAtivo = Camada.Mapa != null, Camada.Fase, Camada.Versao,
                Situacao = Camada.Mapa == null ? "suspenso-mapa-invalidado" : Mapa.Cabe ? "aprovado-no-ensaio" : "rejeitado-64-com-percurso",
                PontosDeInputVerificados = pontosDeInputVerificados,
                Corpo = Camada.Corpo is Rect corpo ? RectTexto(corpo) : null,
                Percurso = Mapa.Cabe ? new[] { RectTexto(Mapa.Inicio), RectTexto(Mapa.Pausa), RectTexto(Mapa.Inicio) } : null,
                Rolagem = Scroll.VerticalOffset,
                SobreposicoesVisiveis = SkaldMedidor.Descendentes(Vista).OfType<Grid>()
                    .Where(g => g.IsVisible && Panel.GetZIndex(g) >= 10)
                    .Select(g => RectTexto(SkaldMedidor.Visivel(g, Raiz))),
                Elementos = Mapa.Elementos.Select(e => new { e.Elemento, Original = RectTexto(e.Original), Visivel = RectTexto(e.Visivel), e.Recortado }),
                Comandos = Dados.Comandos.Select(c => new { c.Nome, c.Execucoes }) });
        }

        public void Dispose()
        {
            Scroll.ScrollChanged -= Rolou;
            Raiz.SizeChanged -= Redimensionou;
            ((INotifyPropertyChanged)Dados.Contexto).PropertyChanged -= ContextoMudou;
            Dados.Notas.CollectionChanged -= ListaMudou;
            Dados.Categorias.CollectionChanged -= ListaMudou;
            descritor.RemoveValueChanged(Hospedeiro, Navegou);
            Camada.Retirar("fim-do-ensaio");
            superficie.Dispose();
            janela.Close();
            Application.Current.Resources = recursosAnteriores;
            Application.Current.ShutdownMode = modoAnterior;
        }
    }
}
