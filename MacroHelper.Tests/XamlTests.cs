using MacroHelper.Core.Agenda;
using MacroHelper.Core.Entities;
using MacroHelper.UI.Controls;
using MacroHelper.UI.Helpers;
using MacroHelper.UI.ViewModels;
using System.IO;

namespace MacroHelper.Tests;

/// <summary>
/// A rede contra a classe de defeito mais barata de guardar e mais cara de achar lendo.
///
/// O Início trazia <c>"Olá, {Binding NomeUsuario}"</c> apontando para uma propriedade que o
/// DashboardViewModel não tinha — a saudação mostrava "Olá, " e mais nada, desde sempre. O WPF
/// não reclama de caminho sem destino, e o compilador não olha para dentro de {Binding}.
///
/// Estes testes leem o XAML como texto e conferem por reflexão. Não substituem abrir o app:
/// verificam nome de propriedade e chave de recurso, não aparência.
/// </summary>
public class XamlTests
{
    /// <summary>
    /// Cada tela com os tipos que podem ser DataContext dela: o ViewModel e o que aparece dentro
    /// dos ItemTemplate. Acrescentar uma tela nova aqui é o que a mantém coberta.
    /// </summary>
    public static TheoryData<string, Type[]> Telas => new()
    {
        { @"Views\MainWindow.xaml",           [typeof(MainViewModel)] },
        { @"Views\DashboardView.xaml",        [typeof(DashboardViewModel), typeof(Tarefa), typeof(Compromisso), typeof(RankingItem)] },
        { @"Views\MacrosView.xaml",           [typeof(MacrosViewModel), typeof(MacroFormViewModel), typeof(Macro), typeof(Categoria), typeof(MacroVersao)] },
        { @"Views\CategoriasView.xaml",       [typeof(CategoriasViewModel), typeof(Categoria)] },
        { @"Views\VariaveisGlobaisView.xaml", [typeof(VariaveisGlobaisViewModel), typeof(VariavelGlobal)] },
        { @"Views\TarefasView.xaml",          [typeof(TarefasViewModel), typeof(Tarefa)] },
        { @"Views\CompromissosView.xaml",    [typeof(CompromissosViewModel), typeof(Compromisso)] },

        // A grade da semana e a do mês. Os dois tipos de dentro dos ItemTemplate são o dia da
        // grade e o bloco que a GradeDeAgenda posicionou; string entra por causa do cabeçalho
        // "Seg" a "Dom", que é uma lista de textos.
        { @"Views\AgendaCalendarioView.xaml", [typeof(CompromissosViewModel), typeof(DiaDaAgenda),
                                                typeof(BlocoDaGrade), typeof(HoraDaGrade),
                                                typeof(FaixaDeHora), typeof(Compromisso)] },
        { @"Views\NotasView.xaml",            [typeof(NotasViewModel), typeof(Nota)] },
        { @"Views\AjudaView.xaml",            [typeof(AjudaViewModel), typeof(FaqItem)] },
        { @"Views\ConfiguracoesView.xaml",    [typeof(ConfiguracoesViewModel), typeof(AmostraDeCor)] },

        // O campo de data é um controle: o DataContext dele é ele próprio.
        { @"Controls\SeletorDeData.xaml",     [typeof(SeletorDeData)] },

        // A barra de marcação também. Ela não tem DataContext nenhum — os únicos bindings de
        // dentro dela são por ElementName, que o leitor ignora —, e entra aqui para que o dia
        // em que ganhar um binding de dados ele já nasça coberto.
        { @"Controls\BarraDeMarcacao.xaml",   [typeof(BarraDeMarcacao)] },

        // As janelas flutuantes montam a lista no code-behind, sem ViewModel: o DataContext de
        // cada linha é a própria entidade. O popup do gatilho só mostra macro; a busca rápida
        // tem uma aba para cada um dos três tipos, e um DataTemplate para cada.
        { @"Views\MacroPopupWindow.xaml",     [typeof(Macro)] },
        // LinhaDePreview entra na lista porque a prévia da janela passou a ser uma lista de
        // linhas numeradas: cada linha é um item, com DataContext próprio.
        { @"Views\BuscadorRapidoWindow.xaml", [typeof(Macro), typeof(Tarefa), typeof(Compromisso), typeof(Nota), typeof(ItemClipboard), typeof(LinhaDePreview)] },

        // A nota aberta ao lado da busca: o conteúdo vai para os campos pelo code-behind, então
        // aqui a cobertura que importa é a das chaves de recurso, feita pelo teste de recursos.
        { @"Views\NotaFlutuanteWindow.xaml",  [typeof(Nota)] },
        { @"Views\TarefaFlutuanteWindow.xaml",[typeof(Tarefa)] },
        { @"Views\CompromissoFlutuanteWindow.xaml", [typeof(Compromisso)] },
    };

    [Theory]
    [MemberData(nameof(Telas))]
    public void TodoCaminhoDeBinding_ExisteEmAlgumDosDataContexts(string arquivo, Type[] contextos)
    {
        var xaml = Xaml.Ler(arquivo);

        var semDestino = Xaml.CaminhosDeBinding(xaml)
            // "DataContext.Foo" vem de RelativeSource AncestorType=UserControl: é o DataContext
            // da tela, ou seja, o próprio ViewModel.
            .Select(c => c.StartsWith("DataContext.", StringComparison.Ordinal) ? c["DataContext.".Length..] : c)
            .Select(c => c.Split('.')[0])
            .Distinct(StringComparer.Ordinal)
            .Where(raiz => !contextos.Any(t => Xaml.TemPropriedade(t, raiz)))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(semDestino.Count == 0,
            $"{arquivo} tem binding para propriedade que não existe em " +
            $"{string.Join(" / ", contextos.Select(t => t.Name))}: {string.Join(", ", semDestino)}");
    }

    /// <summary>
    /// Chave errada de recurso não é pega pelo compilador: o XAML vira BAML no build do mesmo
    /// jeito. StaticResource lança XamlParseException ao abrir a tela; DynamicResource
    /// simplesmente não pinta nada.
    /// </summary>
    [Fact]
    public void TodaChaveDeRecursoUsada_EstaDefinida()
    {
        var globais = new[] { "App.xaml", @"Themes\Light.xaml", @"Themes\Dark.xaml",
                              @"Styles\Tokens.xaml" }
            .SelectMany(a => Xaml.ChavesDefinidas(Xaml.Ler(a)))
            .ToHashSet(StringComparer.Ordinal);

        var problemas = new List<string>();

        foreach (var caminho in Xaml.TodosOsArquivos())
        {
            var xaml = File.ReadAllText(caminho);
            // Chaves declaradas no próprio arquivo (UserControl.Resources) também valem.
            var locais = Xaml.ChavesDefinidas(xaml);

            var faltando = Xaml.ChavesUsadas(xaml)
                .Where(k => !globais.Contains(k) && !locais.Contains(k))
                .Order(StringComparer.Ordinal)
                .ToList();

            if (faltando.Count > 0)
                problemas.Add($"{Path.GetFileName(caminho)}: {string.Join(", ", faltando)}");
        }

        Assert.True(problemas.Count == 0, string.Join(Environment.NewLine, problemas));
    }

    /// <summary>
    /// Os dois temas precisam definir exatamente as mesmas chaves. Uma cor que exista só no
    /// claro deixa o escuro sem pintura naquele ponto — e ninguém que trabalhe num tema só
    /// chega a ver o problema.
    /// </summary>
    [Fact]
    public void OsDoisTemas_DefinemAsMesmasChaves()
    {
        var claro  = Xaml.ChavesDefinidas(Xaml.Ler(@"Themes\Light.xaml"));
        var escuro = Xaml.ChavesDefinidas(Xaml.Ler(@"Themes\Dark.xaml"));

        Assert.Equal(claro.Order(StringComparer.Ordinal), escuro.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Dentro de um InputBinding (o MouseBinding de um cartão clicável, por exemplo) o
    /// RelativeSource não resolve: o InputBinding não está na árvore visual, então não há
    /// ancestral para procurar. O XAML compila, a tela abre, e o clique simplesmente não faz
    /// nada — sem erro, sem aviso. Quem precisa do ViewModel a partir de um item de lista tem
    /// de usar outro caminho (um Button com o cartão por template, como em Notas).
    /// </summary>
    [Fact]
    public void NenhumInputBinding_TentaChegarAoViewModelPorRelativeSource()
    {
        var problemas = new List<string>();

        foreach (var caminho in Xaml.TodosOsArquivos())
        {
            var xaml = File.ReadAllText(caminho);

            foreach (var trecho in Xaml.BlocosDeInputBinding(xaml))
                if (trecho.Contains("RelativeSource", StringComparison.Ordinal) ||
                    trecho.Contains("ElementName=", StringComparison.Ordinal))
                    problemas.Add($"{Path.GetFileName(caminho)}: {PrimeiraLinha(trecho)}");
        }

        Assert.True(problemas.Count == 0, string.Join(Environment.NewLine, problemas));
    }

    private static string PrimeiraLinha(string texto)
    {
        var linhas = texto.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        return linhas.Length == 0 ? string.Empty : linhas[0].Trim();
    }

    /// <summary>
    /// Se o leitor de XAML parar de achar bindings — por uma mudança de formato, ou por um bug
    /// no próprio leitor — os testes acima passariam a aprovar tudo em silêncio. Este é o teste
    /// que diz que a rede ainda está pescando.
    /// </summary>
    [Fact]
    public void OLeitorDeXaml_RealmenteAchaBindings()
    {
        var caminhos = Xaml.CaminhosDeBinding(Xaml.Ler(@"Views\DashboardView.xaml"));

        Assert.True(caminhos.Count > 15, $"Só {caminhos.Count} caminhos — o leitor deve ter parado de funcionar.");
        Assert.Contains("NomeUsuario", caminhos);
        Assert.Contains("DataContext.ConcluirCommand", caminhos);
    }

    [Fact]
    public void OLeitorDeXaml_IgnoraBindingPorElementName()
    {
        // Text pertence ao TextBox nomeado, não ao DataContext — incluí-lo seria falso positivo.
        var caminhos = Xaml.CaminhosDeBinding(Xaml.Ler(@"Views\BuscadorRapidoWindow.xaml"));
        Assert.DoesNotContain("Text", caminhos);
    }
}
