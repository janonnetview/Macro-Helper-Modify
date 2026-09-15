using MacroHelper.Core.Entities;
using MacroHelper.Services;
using MacroHelper.UI.Helpers;
using MacroHelper.UI.Views;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace MacroHelper.Tests;

/// <summary>
/// A janela do Ctrl+Espaço montada de verdade. Ela não tem ViewModel: a lista recebe macro,
/// tarefa ou nota conforme a aba, e quem desenha cada linha é o DataTemplate que o WPF acha
/// pelo TIPO do item. Um template que falte não quebra nada — a linha simplesmente aparece
/// como "MacroHelper.Core.Entities.Tarefa", e só quem abrir a aba descobre.
/// </summary>
public class BuscadorRapidoTests
{
    /// <summary>O que o usuário pediu: o atalho continua caindo em Macros, não na última aba usada.</summary>
    [Fact]
    public void AJanela_AbreNaAbaDeMacros()
    {
        using var banco = new BancoDeTeste();

        TelaDeTeste.Executar(() =>
        {
            var janela = Montar(banco);

            Assert.True(Aba(janela, "RbMacros").IsChecked,  "a aba de Macros devia vir marcada");
            Assert.False(Aba(janela, "RbTarefas").IsChecked);
            Assert.False(Aba(janela, "RbLembretes").IsChecked);
            Assert.False(Aba(janela, "RbNotas").IsChecked);
            Assert.False(Aba(janela, "RbClipboard").IsChecked);
        });
    }

    /// <summary>As abas dividem uma lista só; ela precisa saber desenhar todos os tipos.</summary>
    [Theory]
    [InlineData(typeof(Macro))]
    [InlineData(typeof(Tarefa))]
    [InlineData(typeof(Compromisso))]
    [InlineData(typeof(Nota))]
    [InlineData(typeof(ItemClipboard))]
    public void AListaSabeDesenhar(Type tipo)
    {
        using var banco = new BancoDeTeste();

        TelaDeTeste.Executar(() =>
        {
            var lista = (ListBox)Montar(banco).FindName("ListResultados")!;

            Assert.True(lista.TryFindResource(new DataTemplateKey(tipo)) is DataTemplate,
                $"falta o DataTemplate de {tipo.Name} — a linha sairia como o nome da classe");
        });
    }

    /// <summary>
    /// As abas precisam estar no mesmo GroupName, ou marcar uma deixaria a anterior acesa e a
    /// janela mostraria duas categorias ativas ao mesmo tempo.
    /// </summary>
    [Fact]
    public void MarcarUmaAba_ApagaAsOutras()
    {
        using var banco = new BancoDeTeste();

        TelaDeTeste.Executar(() =>
        {
            var janela = Montar(banco);

            Aba(janela, "RbNotas").IsChecked = true;

            Assert.True(Aba(janela, "RbNotas").IsChecked);
            Assert.False(Aba(janela, "RbMacros").IsChecked, "Macros devia ter apagado ao marcar Notas");
        });
    }


    /// <summary>
    /// O pedido: a nota abre numa SEGUNDA janela, ao lado. A busca continua montada — é o que
    /// permite clicar em outra nota da lista sem antes voltar de algum lugar.
    /// </summary>
    [Fact]
    public async Task AbrirUmaNota_CarregaOTextoInteiroNaJanelaAoLado()
    {
        using var banco = new BancoDeTeste();
        var conteudo = string.Join(Environment.NewLine, Enumerable.Range(1, 40).Select(i => $"linha {i}"));
        await banco.Notas.InsertAsync(new Nota { Titulo = "Roteiro", Conteudo = conteudo });

        var nota = (await banco.Notas.GetAllAsync()).Single();

        TelaDeTeste.Executar(() =>
        {
            var janela = new NotaFlutuanteWindow(new NotaService(banco.Notas));
            Chamar(janela, "Abrir", nota);

            Assert.Equal("Roteiro", ((TextBox)janela.FindName("TxtTitulo")!).Text);
            Assert.Equal(conteudo,  ConteudoDaNota(janela));
            Assert.Same(nota, janela.NotaAtual);
        });
    }

    /// <summary>
    /// "caso eu clique em outra nota, altera para ela" — sem perder o que foi escrito na
    /// anterior, que é o erro fácil de cometer aqui.
    /// </summary>
    [Fact]
    public async Task TrocarDeNota_GravaAAnteriorECarregaANova()
    {
        using var banco = new BancoDeTeste();
        await banco.Notas.InsertAsync(new Nota { Titulo = "Primeira", Conteudo = "conteudo um" });
        await banco.Notas.InsertAsync(new Nota { Titulo = "Segunda",  Conteudo = "conteudo dois" });

        var svc     = new NotaService(banco.Notas);
        var todas   = (await banco.Notas.GetAllAsync()).ToList();
        var primeira = todas.First(n => n.Titulo == "Primeira");
        var segunda  = todas.First(n => n.Titulo == "Segunda");

        TelaDeTeste.Executar(() =>
        {
            var janela = new NotaFlutuanteWindow(svc);
            Chamar(janela, "Abrir", primeira);

            EscreverNaNota(janela, "conteudo um, editado");

            Chamar(janela, "Abrir", segunda);

            Assert.Equal("Segunda",       ((TextBox)janela.FindName("TxtTitulo")!).Text);
            Assert.Equal("conteudo dois", ConteudoDaNota(janela));
        });

        var gravada = await EsperarConteudo(svc, primeira.Id, c => c.EndsWith("editado", StringComparison.Ordinal));
        Assert.Equal("conteudo um, editado", gravada);
    }

    /// <summary>
    /// "caso seja uma nota maior": o campo precisa rolar sozinho e caber na janela.
    ///
    /// Quebra de linha deixou de ser questão quando o campo virou RichTextBox — lá todo Enter é
    /// um parágrafo novo. O que continua sendo fácil de perder num ajuste de layout é o teto de
    /// altura, e é ele que este teste guarda.
    /// </summary>
    [Fact]
    public void OCampoDaNota_AceitaVariasLinhasERola()
    {
        using var banco = new BancoDeTeste();

        TelaDeTeste.Executar(() =>
        {
            var janela = new NotaFlutuanteWindow(new NotaService(banco.Notas));
            var campo  = (RichTextBox)janela.FindName("TxtConteudo")!;

            Assert.Equal(ScrollBarVisibility.Auto, campo.VerticalScrollBarVisibility);

            // O teto mudou de lugar quando a prévia entrou: quem o carrega agora é a caixa que
            // os dois modos dividem, para o desenho da marcação e o texto não terem alturas
            // diferentes. O que precisa continuar valendo é o teto EXISTIR em algum ponto entre
            // o campo e a janela — sem ele, uma nota longa empurra o rodapé para fora da tela.
            var comTeto = Ancestrais(campo)
                .FirstOrDefault(e => e.MaxHeight > 0 && !double.IsInfinity(e.MaxHeight));

            Assert.True(comTeto != null,
                "nada entre o campo e a janela limita a altura: uma nota longa empurra o rodapé para fora da tela");
        });
    }

    /// <summary>
    /// As duas janelas somem ao clicar em outro programa. Se fechar a nota descartasse o
    /// texto, bastaria clicar fora no meio de uma frase para perdê-la.
    /// </summary>
    [Fact]
    public async Task FecharANota_GravaOQueFoiEscrito()
    {
        using var banco = new BancoDeTeste();
        await banco.Notas.InsertAsync(new Nota { Titulo = "Rascunho", Conteudo = "primeira linha" });

        var nota = (await banco.Notas.GetAllAsync()).Single();
        var svc  = new NotaService(banco.Notas);

        TelaDeTeste.Executar(() =>
        {
            var janela = new NotaFlutuanteWindow(svc);
            Chamar(janela, "Abrir", nota);

            EscreverNaNota(janela, "primeira linha" + Environment.NewLine + "segunda linha");

            janela.Fechar();
        });

        var gravada = await EsperarConteudo(svc, nota.Id, c => c.Contains("segunda linha", StringComparison.Ordinal));
        Assert.Contains("segunda linha", gravada, StringComparison.Ordinal);
    }

    /// <summary>
    /// Abrir uma nota só para ler não pode avançar a data de edição dela — é o que jogaria a
    /// nota para o topo da lista, que se ordena pela última edição.
    /// </summary>
    [Fact]
    public async Task AbrirEFecharSemEditar_NaoRegravaANota()
    {
        using var banco = new BancoDeTeste();
        await banco.Notas.InsertAsync(new Nota { Titulo = "So leitura", Conteudo = "nada muda aqui" });

        var svc  = new NotaService(banco.Notas);
        var nota = (await svc.ObterTodasAsync()).Single();
        Assert.Null(nota.DataAtualizacao);

        TelaDeTeste.Executar(() =>
        {
            var janela = new NotaFlutuanteWindow(svc);
            Chamar(janela, "Abrir", nota);
            janela.Fechar();
        });

        await Task.Delay(120);

        Assert.Null((await svc.ObterTodasAsync()).Single().DataAtualizacao);
    }


    [Fact]
    public async Task NovaNota_GravaOQueFoiEscritoAoFechar()
    {
        using var banco = new BancoDeTeste();
        var svc = new NotaService(banco.Notas);

        TelaDeTeste.Executar(() =>
        {
            var janela = new NotaFlutuanteWindow(svc);
            janela.Nova();

            EscreverNaNota(janela, "Comprar cabo HDMI" + Environment.NewLine + "de 2 metros");

            janela.Fechar();
        });

        var criada = await EsperarNota(svc, n => n != null);
        Assert.NotNull(criada);

        // Título em branco: o serviço deduz da primeira linha, e é o que a lista vai mostrar.
        Assert.Equal("Comprar cabo HDMI", criada!.Titulo);
        Assert.Contains("2 metros", criada.Conteudo, StringComparison.Ordinal);
    }

    /// <summary>Um Ctrl+N que ninguém preencheu não pode virar linha no banco.</summary>
    [Fact]
    public async Task NovaNotaEmBranco_NaoGravaNada()
    {
        using var banco = new BancoDeTeste();
        var svc = new NotaService(banco.Notas);

        TelaDeTeste.Executar(() =>
        {
            var janela = new NotaFlutuanteWindow(svc);
            janela.Nova();
            janela.Fechar();
        });

        await Task.Delay(120);
        Assert.Empty(await svc.ObterTodasAsync());
    }

    [Fact]
    public async Task ApagarNota_ExigeDoisCliquesEEntaoRemove()
    {
        using var banco = new BancoDeTeste();
        await banco.Notas.InsertAsync(new Nota { Titulo = "Some daqui", Conteudo = "tchau" });

        var svc  = new NotaService(banco.Notas);
        var nota = (await svc.ObterTodasAsync()).Single();

        TelaDeTeste.Executar(() =>
        {
            var janela = new NotaFlutuanteWindow(svc);
            Chamar(janela, "Abrir", nota);

            var apagar = (Button)janela.FindName("BtnApagar")!;
            apagar.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.Equal("Apagar mesmo?", apagar.Content);
            Assert.NotNull(janela.NotaAtual);

            apagar.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });

        for (var i = 0; i < 60 && (await svc.ObterTodasAsync()).Any(); i++) await Task.Delay(20);
        Assert.Empty(await svc.ObterTodasAsync());
    }

    private static async Task<Nota?> EsperarNota(NotaService svc, Func<Nota?, bool> ate)
    {
        for (var tentativa = 0; tentativa < 60; tentativa++)
        {
            var nota = (await svc.ObterTodasAsync()).FirstOrDefault();
            if (ate(nota)) return nota;
            await Task.Delay(20);
        }

        return (await svc.ObterTodasAsync()).FirstOrDefault();
    }

    private static async Task<string> EsperarConteudo(NotaService svc, int id, Func<string, bool> ate)
    {
        for (var tentativa = 0; tentativa < 50; tentativa++)
        {
            var nota = (await svc.ObterTodasAsync()).FirstOrDefault(n => n.Id == id);
            if (nota != null && ate(nota.Conteudo)) return nota.Conteudo;
            await Task.Delay(20);
        }

        return (await svc.ObterTodasAsync()).First(n => n.Id == id).Conteudo;
    }

    private static void Chamar(Window janela, string metodo, params object[] argumentos) =>
        janela.GetType()
            .GetMethod(metodo, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)!
            .Invoke(janela, argumentos);

    private static RadioButton Aba(Window janela, string nome) => (RadioButton)janela.FindName(nome)!;

    /// <summary>
    /// A nota da janela flutuante como TEXTO. Na tela ela é um documento formatado; o Markdown
    /// é o que entra no banco, e é nele que estes testes falam.
    /// </summary>
    private static string ConteudoDaNota(Window janela) =>
        DocumentoMarkdown.Ler(((RichTextBox)janela.FindName("TxtConteudo")!).Document);

    private static void EscreverNaNota(Window janela, string markdown) =>
        ((RichTextBox)janela.FindName("TxtConteudo")!).Document = DocumentoMarkdown.Montar(markdown);

    /// <summary>
    /// Sobe de pai em pai pela árvore LÓGICA. Visual não serve: a janela nunca é mostrada
    /// nestes testes, e sem Show o WPF não monta a árvore visual dos templates.
    /// </summary>
    private static IEnumerable<FrameworkElement> Ancestrais(FrameworkElement elemento)
    {
        for (var pai = LogicalTreeHelper.GetParent(elemento); pai != null; pai = LogicalTreeHelper.GetParent(pai))
            if (pai is FrameworkElement fe) yield return fe;
    }

    /// <summary>
    /// A janela com serviços reais sobre um banco descartável. Construir já exercita o XAML
    /// inteiro: um StaticResource errado nas abas estoura aqui, e não na primeira vez que
    /// alguém apertar Ctrl+Espaço.
    /// </summary>
    private static Window Montar(BancoDeTeste banco)
    {
        var macros  = new MacroService(banco.Macros);
        var logs    = new LogUsoService(banco.Logs);
        var texto   = new TextInsertionService();
        var relogio = new RelogioFalso(new DateTime(2026, 8, 20, 9, 0, 0));

        return new BuscadorRapidoWindow(
            macros,
            new InsercaoDeMacroService(macros, texto, logs),
            logs,
            new TarefaService(banco.Tarefas, relogio),
            new CompromissoService(banco.Compromissos, relogio),
            new NotaService(banco.Notas),
            new ClipboardService(banco.Clipboard),
            texto,
            relogio);
    }
}
