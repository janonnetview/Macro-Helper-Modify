using MacroHelper.Core.Entities;
using MacroHelper.Services;
using MacroHelper.UI.ViewModels;
using MacroHelper.UI.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MacroHelper.Tests;

/// <summary>
/// A tela de Notas montada de verdade, com ViewModel e banco reais. Os XamlTests conferem que
/// todo {Binding} aponta para uma propriedade existente; isso não basta para um comando que
/// mora num lugar de onde o binding não enxerga o resto da tela — o caminho existe, o alvo
/// existe, e mesmo assim nada acontece ao clicar. Aqui a tela é construída e o cartão é
/// acionado como o clique faria.
/// </summary>
public class NotasViewTests
{
    [Fact]
    public async Task OCartaoDaNota_AbreANotaAoSerClicado()
    {
        using var banco = new BancoDeTeste();
        await banco.Notas.InsertAsync(new Nota
        {
            Titulo   = "Accordion Restantes",
            Conteudo = "Accordion - Publicações\nAccordion - Garantias",
        });

        var vm = new NotasViewModel(new NotaService(banco.Notas));
        await vm.CarregarAsync();
        Assert.Single(vm.Notas);

        TelaDeTeste.Executar(() =>
        {
            var tela = new NotasView { DataContext = vm };
            tela.Measure(new Size(1000, 700));
            tela.Arrange(new Rect(new Size(1000, 700)));
            tela.UpdateLayout();

            // O cartão é o único Button da tela que leva uma Nota como parâmetro de comando.
            var cartao = Achar<Button>(tela, b => b.Command != null && b.CommandParameter is Nota);

            Assert.True(cartao != null, "o cartão da nota não está ligado a comando nenhum — clicar nele não faria nada");
            Assert.True(cartao!.Command.CanExecute(cartao.CommandParameter));
            cartao.Command.Execute(cartao.CommandParameter);
        });

        Assert.True(vm.MostrarEditor, "clicar no cartão devia abrir o editor");
        Assert.Equal("Accordion Restantes", vm.EditorTitulo);
    }

    /// <summary>
    /// A caixa de marcar da prévia, clicada como o mouse faria.
    ///
    /// É o teste que prova a ligação inteira: o painel da prévia recebe o texto por propriedade
    /// anexada, desenha uma CheckBox de verdade para cada linha "- [ ]", e o clique dela chega
    /// ao comando que reescreve o CONTEÚDO da nota. Nenhum elo desse caminho aparece como
    /// binding — a tela abriria bonita e a marca não faria nada.
    /// </summary>
    [Fact]
    public async Task ACaixaDeMarcarDaPrevia_EscreveAMarcaNoTextoDaNota()
    {
        using var banco = new BancoDeTeste();
        await banco.Notas.InsertAsync(new Nota
        {
            Titulo   = "Reunião de quarta",
            Conteudo = "## Combinados\n- [ ] enviar a ata\n- [ ] cobrar o retorno",
        });

        var vm = new NotasViewModel(new NotaService(banco.Notas));
        await vm.CarregarAsync();
        vm.AbrirNotaCommand.Execute(vm.Notas.Single());

        Assert.True(vm.EditorVisualizando, "uma nota que já existe abre no desenho, para ser lida");

        TelaDeTeste.Executar(() =>
        {
            var tela = new NotasView { DataContext = vm };
            tela.Measure(new Size(1000, 700));
            tela.Arrange(new Rect(new Size(1000, 700)));
            tela.UpdateLayout();

            var caixas = Todos<CheckBox>(tela).ToList();
            Assert.True(caixas.Count == 2,
                $"a prévia devia desenhar uma caixa para cada linha de tarefa, e desenhou {caixas.Count}");

            var primeira = caixas[0];
            primeira.IsChecked = true;
            primeira.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        });

        Assert.Equal("## Combinados\n- [x] enviar a ata\n- [ ] cobrar o retorno", vm.EditorConteudo);
    }

    /// <summary>A marca é conteúdo da nota, e conteúdo só vale depois de salvo.</summary>
    [Fact]
    public async Task MarcarUmItem_SoChegaAoBancoDepoisDeSalvar()
    {
        using var banco = new BancoDeTeste();
        var servico = new NotaService(banco.Notas);
        await banco.Notas.InsertAsync(new Nota { Titulo = "Combinados", Conteudo = "- [ ] enviar a ata" });

        var vm = new NotasViewModel(servico);
        await vm.CarregarAsync();
        vm.AbrirNotaCommand.Execute(vm.Notas.Single());

        vm.AlternarItemCommand.Execute(0);
        Assert.Equal("- [x] enviar a ata", vm.EditorConteudo);
        Assert.Equal("- [ ] enviar a ata", (await servico.ObterTodasAsync()).Single().Conteudo);

        await vm.SalvarAsync();
        Assert.Equal("- [x] enviar a ata", (await servico.ObterTodasAsync()).Single().Conteudo);
    }

    private static IEnumerable<T> Todos<T>(DependencyObject raiz) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var filho = VisualTreeHelper.GetChild(raiz, i);
            if (filho is T alvo) yield return alvo;

            foreach (var neto in Todos<T>(filho)) yield return neto;
        }
    }

    private static T? Achar<T>(DependencyObject raiz, Func<T, bool> criterio) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var filho = VisualTreeHelper.GetChild(raiz, i);
            if (filho is T alvo && criterio(alvo)) return alvo;

            var achado = Achar(filho, criterio);
            if (achado != null) return achado;
        }

        return null;
    }
}
