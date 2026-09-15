using MacroHelper.Core.Entities;
using MacroHelper.Services;
using MacroHelper.UI.ViewModels;
using MacroHelper.UI.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MacroHelper.Tests;

/// <summary>
/// A tela de Compromissos montada de verdade, pela mesma razão de <see cref="NotasViewTests"/>
/// existir: os XamlTests conferem que todo {Binding} tem destino, e isso não pega um comando
/// que mora dentro de um ItemTemplate e não enxerga o ViewModel da tela. O caminho existe, o
/// alvo existe, e mesmo assim clicar não faz nada.
///
/// Montar a tela também é o que prova que o XAML abre: um erro de recurso ou de sintaxe só
/// aparece quando alguém navega até ela, o que num teste de compilação nunca acontece.
/// </summary>
public class CompromissosViewTests
{
    private static readonly DateTime QuartaAs9 = new(2026, 8, 19, 9, 0, 0);

    /// <summary>
    /// O modo entra explícito porque ele é PREFERÊNCIA GRAVADA da pessoa: quem trabalha na
    /// grade da semana abre o app nela, e aí a lista deste teste não estaria montada na tela.
    /// Estes testes são sobre a lista, então eles pedem a lista. A grade tem os seus, em
    /// <see cref="AgendaCalendarioTests"/>.
    /// </summary>
    private static (CompromissosViewModel Vm, CompromissoService Svc) Montar(BancoDeTeste banco)
    {
        var svc = new CompromissoService(banco.Compromissos, new RelogioFalso(QuartaAs9));
        var vm  = new CompromissosViewModel(svc, new RelogioFalso(QuartaAs9)) { Modo = "lista" };
        return (vm, svc);
    }

    [Fact]
    public async Task OBotaoDeRealizado_MudaASituacaoDoCompromisso()
    {
        using var banco = new BancoDeTeste();
        var (vm, svc) = Montar(banco);

        var (_, _, salvo) = await svc.SalvarAsync(new Compromisso
        {
            Titulo = "Visita do técnico",
            Local  = "Em casa",
            Quando = QuartaAs9.AddDays(1).AddHours(5),
            AvisoAntecipadoMinutos = Antecedencia.PadraoAntecipado,
            AvisoNaHoraMinutos     = Antecedencia.PadraoNaHora,
        });

        await vm.CarregarAsync();
        Assert.Single(vm.Compromissos);

        TelaDeTeste.Executar(() =>
        {
            var tela = new CompromissosView { DataContext = vm };
            tela.Measure(new Size(1200, 800));
            tela.Arrange(new Rect(new Size(1200, 800)));
            tela.UpdateLayout();

            var botao = Achar<Button>(tela, b =>
                b.CommandParameter is Compromisso &&
                ReferenceEquals(b.Command, vm.MarcarRealizadoCommand));

            Assert.True(botao != null,
                "o botão de marcar como realizado não está ligado ao comando — clicar nele não faria nada");
            botao!.Command.Execute(botao.CommandParameter);
        });

        // O comando é assíncrono: o que importa é que ele tenha sido disparado e chegado ao banco.
        await vm.CarregarAsync();
        Assert.True((await svc.ObterPorIdAsync(salvo!.Id))!.Realizado);
    }

    /// <summary>
    /// O filtro padrão mostra o que ainda vem. Um compromisso realizado sai da lista sem sumir
    /// do app — ele continua no Histórico, que é o outro chip.
    /// </summary>
    [Fact]
    public async Task RealizadoSaiDosProximosEEntraNoHistorico()
    {
        using var banco = new BancoDeTeste();
        var (vm, svc) = Montar(banco);

        var (_, _, salvo) = await svc.SalvarAsync(new Compromisso
        {
            Titulo = "Entregar o documento ao gestor",
            Quando = QuartaAs9.AddHours(1),
        });

        await vm.CarregarAsync();
        Assert.Single(vm.Compromissos);
        Assert.Equal(1, vm.TotalProximos);

        await svc.DefinirSituacaoAsync(salvo!.Id, SituacaoCompromisso.Realizado);
        await vm.CarregarAsync();

        Assert.Empty(vm.Compromissos);
        Assert.Equal(0, vm.TotalProximos);
        Assert.Equal(1, vm.TotalHistorico);

        vm.DefinirFiltroCommand.Execute("historico");
        Assert.Single(vm.Compromissos);
    }

    /// <summary>
    /// Um compromisso novo já nasce com os dois avisos marcados no formulário. É o que impede
    /// a agenda de virar uma lista bonita que não avisa nada.
    /// </summary>
    [Fact]
    public void OFormularioDeNovoCompromisso_JaVemComOsAvisosPadrao()
    {
        using var banco = new BancoDeTeste();
        var (vm, _) = Montar(banco);

        vm.NovoCompromissoCommand.Execute(null);

        Assert.True(vm.MostrarFormulario);
        Assert.Equal("1440", vm.FormAvisoAntecipado);
        Assert.Equal("30",   vm.FormAvisoNaHora);
        Assert.Equal("09:00", vm.FormHora);
    }

    [Fact]
    public async Task SalvarPeloFormulario_JuntaDataEHoraNumInstanteSo()
    {
        using var banco = new BancoDeTeste();
        var (vm, svc) = Montar(banco);

        vm.NovoCompromissoCommand.Execute(null);
        vm.FormTexto = "Visita do técnico";
        vm.FormData  = "20/08/2026";
        vm.FormHora  = "14:00";

        await vm.SalvarAsync();

        Assert.Null(vm.FormErro);

        var salvo = (await svc.ObterTodosAsync()).Single();
        Assert.Equal(new DateTime(2026, 8, 20, 14, 0, 0), salvo.Quando);
        Assert.Equal(Antecedencia.PadraoAntecipado, salvo.AvisoAntecipadoMinutos);
        Assert.Equal(Antecedencia.PadraoNaHora,     salvo.AvisoNaHoraMinutos);
    }

    /// <summary>
    /// Hora em branco COM data não vira "às 9h" caladamente. Meia data não marca nada, e o
    /// caminho para gravar sem data é apagar os DOIS campos (ver SemDataNemHora_GravaPorMarcar).
    /// </summary>
    [Fact]
    public async Task SalvarSemHora_ReclamaNoFormulario()
    {
        using var banco = new BancoDeTeste();
        var (vm, svc) = Montar(banco);

        vm.NovoCompromissoCommand.Execute(null);
        vm.FormTexto = "Sem hora";
        vm.FormHora  = string.Empty;

        await vm.SalvarAsync();

        Assert.NotNull(vm.FormErro);
        Assert.True(vm.MostrarFormulario, "o formulário precisa continuar aberto para a correção");
        Assert.Empty(await svc.ObterTodosAsync());
    }

    /// <summary>
    /// Os dois campos vazios são o lembrete remarcado: grava, sem data e sem aviso.
    ///
    /// O formulário abre com a data de hoje preenchida, então "apagar a data" é uma ação
    /// deliberada — não dá para cair aqui por distração.
    /// </summary>
    [Fact]
    public async Task SemDataNemHora_GravaPorMarcar()
    {
        using var banco = new BancoDeTeste();
        var (vm, svc) = Montar(banco);

        vm.NovoCompromissoCommand.Execute(null);
        vm.FormTexto = "Visita do técnico, a remarcar";
        vm.FormData  = string.Empty;
        vm.FormHora  = string.Empty;

        await vm.SalvarAsync();

        Assert.Null(vm.FormErro);
        Assert.False(vm.MostrarFormulario);

        var salvo = Assert.Single(await svc.ObterTodosAsync());
        Assert.Null(salvo.Quando);
        Assert.Null(salvo.AvisoAntecipadoMinutos);
        Assert.Null(salvo.AvisoNaHoraMinutos);
    }

    /// <summary>Hora sem dia é o mesmo problema pelo outro lado: metade de uma marcação.</summary>
    [Fact]
    public async Task HoraSemData_ReclamaNoFormulario()
    {
        using var banco = new BancoDeTeste();
        var (vm, svc) = Montar(banco);

        vm.NovoCompromissoCommand.Execute(null);
        vm.FormTexto = "Só a hora";
        vm.FormData  = string.Empty;
        vm.FormHora  = "14:00";

        await vm.SalvarAsync();

        Assert.NotNull(vm.FormErro);
        Assert.True(vm.MostrarFormulario, "o formulário precisa continuar aberto para a correção");
        Assert.Empty(await svc.ObterTodosAsync());
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
