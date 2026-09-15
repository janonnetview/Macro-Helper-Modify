using MacroHelper.Services;
using MacroHelper.UI.Controls;
using MacroHelper.UI.Helpers;
using MacroHelper.UI.ViewModels;
using MacroHelper.UI.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;

namespace MacroHelper.Tests;

/// <summary>
/// O editor de notas com a formatação à vista.
///
/// A nota é escrita como ela fica (negrito é negrito, não dois asteriscos) e gravada como
/// Markdown. Entre as duas coisas há uma ida e uma volta, e é ela que estes testes guardam: o
/// que sai da tela tem de voltar igual ao que entrou, senão a nota se degrada um pouco a cada
/// vez que alguém a abre — o tipo de defeito que só aparece depois de a nota já estar perdida.
///
/// Rodam na thread das telas porque RichTextBox é controle de verdade: documento, seleção e
/// parágrafo só existem num objeto WPF vivo.
/// </summary>
public class MarcacaoDeTextoTests
{
    /// <summary>
    /// A nota inteira, com tudo o que a marcação sabe fazer, indo e voltando sem mudar uma
    /// vírgula. É o teste mais importante do arquivo.
    /// </summary>
    [Fact]
    public void OTextoDaNota_VaiEVoltaIgual()
    {
        // Juntado linha a linha, e não escrito num literal de várias linhas: a quebra de linha é
        // justamente o que este teste afere, e ela não pode depender de como o arquivo de teste
        // foi gravado.
        var nota = string.Join(Environment.NewLine,
            "# Credenciais",
            "Server name: max-eba.us-east-1.rds.amazonaws.com",
            "Senha: **R4UL**, login *raul.janon*",
            "",
            "## Pendências",
            "- conferir o acesso",
            "- [ ] abrir o chamado",
            "- [x] avisar o time",
            "1. primeiro",
            "2. segundo",
            "> anotado na reunião",
            "Um `select * from tabela` no meio da linha.",
            "[o chamado](https://exemplo.com/4821)",
            "---",
            "fim");

        TelaDeTeste.Executar(() =>
            Assert.Equal(nota, DocumentoMarkdown.Ler(DocumentoMarkdown.Montar(nota))));
    }

    [Fact]
    public void Negrito_NoTrechoSelecionado_GravaComAsteriscos()
    {
        TelaDeTeste.Executar(() =>
        {
            var caixa = Editor("o prazo venceu");
            Selecionar(caixa, "prazo");

            MarcacaoDeTexto.Aplicar(caixa, "negrito");

            Assert.Equal("o **prazo** venceu", Texto(caixa));
        });
    }

    /// <summary>O mesmo botão põe e tira, como no Word.</summary>
    [Fact]
    public void Negrito_NoQueJaEstaNegrito_TiraONegrito()
    {
        TelaDeTeste.Executar(() =>
        {
            var caixa = Editor("o **prazo** venceu");
            Selecionar(caixa, "prazo");

            MarcacaoDeTexto.Aplicar(caixa, "negrito");

            Assert.Equal("o prazo venceu", Texto(caixa));
        });
    }

    [Fact]
    public void Italico_EhOMesmoBotaoNosDoisSentidos()
    {
        TelaDeTeste.Executar(() =>
        {
            var caixa = Editor("prazo apertado");
            Selecionar(caixa, "apertado");

            MarcacaoDeTexto.Aplicar(caixa, "italico");
            Assert.Equal("prazo *apertado*", Texto(caixa));

            Selecionar(caixa, "apertado");
            MarcacaoDeTexto.Aplicar(caixa, "italico");
            Assert.Equal("prazo apertado", Texto(caixa));
        });
    }

    [Fact]
    public void Riscado_ViraTilDosDoisLados()
    {
        TelaDeTeste.Executar(() =>
        {
            var caixa = Editor("item cancelado");
            Selecionar(caixa, "cancelado");

            MarcacaoDeTexto.Aplicar(caixa, "riscado");

            Assert.Equal("item ~~cancelado~~", Texto(caixa));
        });
    }

    /// <summary>
    /// Virar lista é quase sempre uma decisão tomada DEPOIS de escrever as linhas: o botão
    /// precisa valer para todas as que a seleção encostou, e não só para a do cursor.
    /// </summary>
    [Fact]
    public void Lista_MarcaTodasAsLinhasDaSelecao_EDesmarcaNaSegundaVez()
    {
        TelaDeTeste.Executar(() =>
        {
            var caixa = Editor("leite\novos\ncafé");
            caixa.Selection.Select(caixa.Document.ContentStart, caixa.Document.ContentEnd);

            MarcacaoDeTexto.Aplicar(caixa, "lista");
            Assert.Equal(Linhas("- leite", "- ovos", "- café"), Texto(caixa));

            caixa.Selection.Select(caixa.Document.ContentStart, caixa.Document.ContentEnd);
            MarcacaoDeTexto.Aplicar(caixa, "lista");
            Assert.Equal(Linhas("leite", "ovos", "café"), Texto(caixa));
        });
    }

    [Fact]
    public void ListaNumerada_ContaAsLinhasEmVezDeRepetirOUm()
    {
        TelaDeTeste.Executar(() =>
        {
            var caixa = Editor("comprar\npagar\nconferir");
            caixa.Selection.Select(caixa.Document.ContentStart, caixa.Document.ContentEnd);

            MarcacaoDeTexto.Aplicar(caixa, "numerada");

            Assert.Equal(Linhas("1. comprar", "2. pagar", "3. conferir"), Texto(caixa));
        });
    }

    /// <summary>Item que vira tarefa é a MESMA linha: troca de marcador, não ganha dois.</summary>
    [Fact]
    public void Tarefa_EmCimaDeUmItemDeLista_TrocaOMarcador()
    {
        TelaDeTeste.Executar(() =>
        {
            var caixa = Editor("- conferir o anexo");
            Selecionar(caixa, "conferir");

            MarcacaoDeTexto.Aplicar(caixa, "tarefa");

            Assert.Equal("- [ ] conferir o anexo", Texto(caixa));
        });
    }

    /// <summary>
    /// A caixinha clicada marca a tarefa no TEXTO da nota, que é onde a marca mora: ela vale
    /// depois de gravar, como qualquer outra edição.
    /// </summary>
    [Fact]
    public void ACaixinha_AlternaEntreFeitaEPorFazer()
    {
        TelaDeTeste.Executar(() =>
        {
            var caixa = Editor("- [ ] abrir o chamado");
            var p = (Paragraph)caixa.Document.Blocks.FirstBlock;

            MarcacaoDeTexto.AlternarTarefa(p);
            Assert.Equal("- [x] abrir o chamado", Texto(caixa));

            MarcacaoDeTexto.AlternarTarefa(p);
            Assert.Equal("- [ ] abrir o chamado", Texto(caixa));
        });
    }

    [Fact]
    public void Titulo_PoeETiraNaMesmaLinha()
    {
        TelaDeTeste.Executar(() =>
        {
            var caixa = Editor("Pendências");
            Selecionar(caixa, "Pendências");

            MarcacaoDeTexto.Aplicar(caixa, "titulo");
            Assert.Equal("## Pendências", Texto(caixa));

            MarcacaoDeTexto.Aplicar(caixa, "titulo");
            Assert.Equal("Pendências", Texto(caixa));
        });
    }

    [Fact]
    public void Citacao_PoeETiraABarraDaLinha()
    {
        TelaDeTeste.Executar(() =>
        {
            var caixa = Editor("anotado na reunião");
            Selecionar(caixa, "anotado");

            MarcacaoDeTexto.Aplicar(caixa, "citacao");
            Assert.Equal("> anotado na reunião", Texto(caixa));

            MarcacaoDeTexto.Aplicar(caixa, "citacao");
            Assert.Equal("anotado na reunião", Texto(caixa));
        });
    }

    /// <summary>
    /// O link é o que mais muda com a formatação à vista: o endereço não aparece em lugar
    /// nenhum da tela, então ele precisa sobreviver na ida e na volta para não se perder.
    /// </summary>
    [Fact]
    public void Link_GuardaOEnderecoQueNaoApareceNaTela()
    {
        TelaDeTeste.Executar(() =>
        {
            var caixa = Editor("ver o chamado");
            Selecionar(caixa, "chamado");

            MarcacaoDeTexto.Ligar(caixa, "https://exemplo.com/4821");

            Assert.Equal("ver o [chamado](https://exemplo.com/4821)", Texto(caixa));
        });
    }

    [Fact]
    public void Inserir_TrocaOQueEstavaSelecionado()
    {
        TelaDeTeste.Executar(() =>
        {
            var caixa = Editor("status: x");
            Selecionar(caixa, "x");

            MarcacaoDeTexto.Inserir(caixa, "✓");

            Assert.Equal("status: ✓", Texto(caixa));
        });
    }

    /// <summary>Marcação que não existe não faz nada, em vez de estragar o parágrafo.</summary>
    [Fact]
    public void MarcacaoDesconhecida_NaoMexeNoTexto()
    {
        TelaDeTeste.Executar(() =>
        {
            var caixa = Editor("intacto");
            Selecionar(caixa, "intacto");

            MarcacaoDeTexto.Aplicar(caixa, "sublinhado");

            Assert.Equal("intacto", Texto(caixa));
        });
    }

    /// <summary>
    /// A barra ligada na caixa: clicar no B formata a caixa apontada por Alvo.
    ///
    /// É o elo que nenhum dos testes acima cobre — os botões ficam num controle e a caixa é de
    /// outra tela. Apontar para lugar nenhum compila, abre e não faz nada ao clicar.
    /// </summary>
    [Fact]
    public void ABarra_FormataACaixaApontadaPorAlvo()
    {
        TelaDeTeste.Executar(() =>
        {
            var caixa = Editor("reunião adiada");
            Selecionar(caixa, "adiada");

            var barra = new BarraDeMarcacao { Alvo = caixa };
            Botao(barra, "negrito").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            Assert.Equal("reunião **adiada**", Texto(caixa));
        });
    }

    /// <summary>
    /// A grade de caracteres especiais: existe, tem os sinais que ela promete, e inserir um
    /// deles fecha o popup. Montada no code-behind — se o laço parar de rodar, ela abre vazia e
    /// nada estoura.
    /// </summary>
    [Fact]
    public void AGradeDeCaracteres_InsereOSinalEFechaOPopup()
    {
        TelaDeTeste.Executar(() =>
        {
            var caixa = Editor(string.Empty);
            var barra = new BarraDeMarcacao { Alvo = caixa };

            var alternador = (ToggleButton)barra.FindName("BotaoCaracteres")!;
            alternador.IsChecked = true;

            var certo = Todos<Button>(barra).FirstOrDefault(b => b.Content as string == "✓");
            Assert.True(certo != null, "a grade não tem o ✓ — ele é a razão de o botão existir");

            certo!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            Assert.Equal("✓", Texto(caixa));
            Assert.False(alternador.IsChecked, "o popup devia fechar depois de inserir");
        });
    }

    /// <summary>
    /// As duas caixas onde se escreve nota — a da tela e a da janela flutuante — precisam ter a
    /// barra da seleção ligada E a barra fixa apontando para elas.
    ///
    /// Nenhuma das duas coisas aparece como erro: sem a propriedade anexada a barra simplesmente
    /// nunca aparece, e com o Alvo nulo os botões da barra fixa não fazem nada ao serem
    /// clicados. As duas falhas são caladas, e é por isso que estão aqui.
    /// </summary>
    [Fact]
    public void ANotaFlutuante_TemAsDuasBarrasLigadasNaCaixaDeConteudo()
    {
        using var banco = new BancoDeTeste();

        TelaDeTeste.Executar(() =>
        {
            var janela = new NotaFlutuanteWindow(new NotaService(banco.Notas));
            var campo  = (RichTextBox)janela.FindName("TxtConteudo")!;

            Assert.True(BarraAoSelecionar.GetLigada(campo),
                "sem isto a barra nunca aparece ao selecionar um trecho na nota flutuante");
            Assert.True(EditorDeMarcacao.GetLigado(campo),
                "sem isto o Enter não continua a lista nem o clique marca a caixinha");

            var barra = Todos<BarraDeMarcacao>(janela).FirstOrDefault();
            Assert.True(barra != null, "a janela da nota ficou sem barra de formatação");
            Assert.Same(campo, barra!.Alvo);
        });
    }

    [Fact]
    public void ATelaDeNotas_TemAsDuasBarrasLigadasNaCaixaDeConteudo()
    {
        using var banco = new BancoDeTeste();

        TelaDeTeste.Executar(() =>
        {
            var tela  = new NotasView { DataContext = new NotasViewModel(new NotaService(banco.Notas)) };
            var campo = (RichTextBox)tela.FindName("CaixaDeConteudo")!;

            Assert.True(BarraAoSelecionar.GetLigada(campo),
                "sem isto a barra nunca aparece ao selecionar um trecho no editor de notas");

            var barra = Todos<BarraDeMarcacao>(tela).FirstOrDefault();
            Assert.True(barra != null, "o editor de notas ficou sem barra de formatação");
            Assert.Same(campo, barra!.Alvo);
        });
    }

    /// <summary>
    /// O editor da tela de Notas ligado ao ViewModel: o que se digita vira Markdown na
    /// propriedade, e o que chega pela propriedade vira documento.
    /// </summary>
    [Fact]
    public void OEditorDaTela_ConversaComOViewModelNosDoisSentidos()
    {
        using var banco = new BancoDeTeste();

        TelaDeTeste.Executar(() =>
        {
            var vm   = new NotasViewModel(new NotaService(banco.Notas));
            var tela = new NotasView { DataContext = vm };
            var campo = (RichTextBox)tela.FindName("CaixaDeConteudo")!;

            // Sem um passo de layout o WPF nem chega a ligar os bindings da tela — eles ficam
            // "Unattached" —, e a caixa nasceria vazia por motivo nenhum.
            tela.Measure(new Size(1000, 700));
            tela.Arrange(new Rect(new Size(1000, 700)));
            tela.UpdateLayout();

            vm.EditorConteudo = Linhas("## Reunião", "- primeiro ponto");
            Assert.Equal(Linhas("## Reunião", "- primeiro ponto"), DocumentoMarkdown.Ler(campo.Document));

            Selecionar(campo, "primeiro");
            MarcacaoDeTexto.Aplicar(campo, "negrito");

            Assert.Equal(Linhas("## Reunião", "- **primeiro** ponto"), vm.EditorConteudo);
        });
    }

    // ── Apoio ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Junta linhas com a quebra do Windows, que é a que a nota gravada tem. Escrita à mão
    /// dentro das expectativas, ela se confundiria com a quebra do próprio arquivo de teste.
    /// </summary>
    private static string Linhas(params string[] linhas) => string.Join(Environment.NewLine, linhas);

    private static RichTextBox Editor(string markdown) =>
        new() { Document = DocumentoMarkdown.Montar(markdown) };

    private static string Texto(RichTextBox caixa) => DocumentoMarkdown.Ler(caixa.Document);

    /// <summary>
    /// Seleciona um trecho pelo que está escrito nele. Por offset seria pior de ler e pior de
    /// manter: o TextPointer conta as bordas dos elementos, não as letras.
    /// </summary>
    private static void Selecionar(RichTextBox caixa, string trecho)
    {
        for (var p = caixa.Document.ContentStart;
             p != null;
             p = p.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (p.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.Text) continue;

            var corrida = p.GetTextInRun(LogicalDirection.Forward);
            var onde    = corrida.IndexOf(trecho, StringComparison.Ordinal);
            if (onde < 0) continue;

            var inicio = p.GetPositionAtOffset(onde);
            caixa.Selection.Select(inicio, inicio!.GetPositionAtOffset(trecho.Length));
            return;
        }

        Assert.Fail($"não achei \"{trecho}\" no documento");
    }

    private static Button Botao(DependencyObject raiz, string marcacao) =>
        Todos<Button>(raiz).First(b => b.Tag as string == marcacao);

    /// <summary>
    /// Varre a árvore LÓGICA: os controles destes testes nunca são mostrados, e sem Show o WPF
    /// não monta a árvore visual de dentro dos templates.
    /// </summary>
    private static IEnumerable<T> Todos<T>(DependencyObject raiz) where T : DependencyObject
    {
        foreach (var filho in LogicalTreeHelper.GetChildren(raiz).OfType<DependencyObject>())
        {
            if (filho is T achado) yield return achado;

            foreach (var neto in Todos<T>(filho)) yield return neto;
        }
    }
}
