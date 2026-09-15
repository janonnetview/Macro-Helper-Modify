using MacroHelper.Core.Entities;
using MacroHelper.Services;
using MacroHelper.UI.Controls;
using MacroHelper.UI.Views;
using System.Windows;
using System.Windows.Controls;

namespace MacroHelper.Tests;

/// <summary>
/// O lembrete aberto ao lado da busca rápida — criar, editar e apagar sem abrir o app, que é
/// justamente o que faltava na aba do Ctrl+Espaço.
///
/// Irmã de <see cref="TarefaFlutuanteTests"/>: a janela não tem ViewModel, lê e escreve nos
/// próprios campos, então o jeito de exercitá-la é montá-la de verdade e mexer nos campos
/// como o teclado mexeria.
/// </summary>
public class CompromissoFlutuanteTests
{
    private static readonly DateTime QuartaAs9 = new(2026, 8, 19, 9, 0, 0);

    [Fact]
    public async Task Nova_GravaOLembreteEscritoAoFechar()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        TelaDeTeste.Executar(() =>
        {
            var janela = Montar(svc);
            janela.Nova();

            Campo(janela, "TxtTitulo").Text = "Visita do técnico";
            Campo(janela, "TxtLocal").Text  = "Em casa";
            Seletor(janela, "CampoData").Texto = "20/08/2026";
            Campo(janela, "TxtHora").Text   = "14:00";

            janela.Fechar();
        });

        var criado = Assert.Single(await EsperarLista(svc, l => l.Any()));

        Assert.Equal("Visita do técnico", criado.Titulo);
        Assert.Equal("Em casa",           criado.Local);
        Assert.Equal(new DateTime(2026, 8, 20, 14, 0, 0), criado.Quando);

        // Os dois avisos padrão vêm marcados no formulário e precisam chegar ao banco: sem
        // eles o lembrete seria uma linha bonita que não avisa nada.
        Assert.Equal(Antecedencia.PadraoAntecipado, criado.AvisoAntecipadoMinutos);
        Assert.Equal(Antecedencia.PadraoNaHora,     criado.AvisoNaHoraMinutos);
    }

    /// <summary>Um Ctrl+N que ninguém preencheu não pode virar linha no banco.</summary>
    [Fact]
    public async Task NovaSemTitulo_NaoGravaNada()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        TelaDeTeste.Executar(() =>
        {
            var janela = Montar(svc);
            janela.Nova();
            janela.Fechar();
        });

        await Task.Delay(120);
        Assert.Empty(await svc.ObterTodosAsync());
    }

    [Fact]
    public async Task Editar_GravaONovoHorarioEAAntecedencia()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        var (_, _, existente) = await svc.SalvarAsync(new Compromisso
        {
            Titulo                 = "Entregar o documento",
            Quando                 = new DateTime(2026, 8, 19, 16, 0, 0),
            AvisoAntecipadoMinutos = Antecedencia.PadraoAntecipado,
            AvisoNaHoraMinutos     = Antecedencia.PadraoNaHora,
        });

        TelaDeTeste.Executar(() =>
        {
            var janela = Montar(svc);
            janela.Abrir(existente!);

            Seletor(janela, "CampoData").Texto = "20/08/2026";
            Campo(janela, "TxtHora").Text      = "10:30";
            Escolher(janela, "CmbAvisoHora", "10");

            janela.Fechar();
        });

        var gravado = Assert.Single(await EsperarLista(svc, l => l.Any(c => c.AvisoNaHoraMinutos == 10)));

        Assert.Equal(new DateTime(2026, 8, 20, 10, 30, 0), gravado.Quando);
        Assert.Equal(10, gravado.AvisoNaHoraMinutos);
    }

    /// <summary>
    /// Remarcar pela janelinha tem de rearmar os avisos, como remarcar pela tela. A regra mora
    /// no serviço, e este teste é a prova de que este caminho passa por lá.
    /// </summary>
    [Fact]
    public async Task Remarcar_RearmaOsAvisos()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        var (_, _, existente) = await svc.SalvarAsync(new Compromisso
        {
            Titulo                 = "Visita do técnico",
            Quando                 = new DateTime(2026, 8, 20, 14, 0, 0),
            AvisoAntecipadoMinutos = Antecedencia.PadraoAntecipado,
            AvisoNaHoraMinutos     = Antecedencia.PadraoNaHora,
        });

        await svc.MarcarAvisoDisparadoAsync(existente!.Id, antecipado: true);
        await svc.MarcarAvisoDisparadoAsync(existente.Id, antecipado: false);

        var recarregado = await svc.ObterPorIdAsync(existente.Id);

        TelaDeTeste.Executar(() =>
        {
            var janela = Montar(svc);
            janela.Abrir(recarregado!);
            Seletor(janela, "CampoData").Texto = "27/08/2026";
            janela.Fechar();
        });

        var gravado = Assert.Single(
            await EsperarLista(svc, l => l.Any(c => c.Quando?.Day == 27)));

        Assert.False(gravado.AvisoAntecipadoDisparado);
        Assert.False(gravado.AvisoNaHoraDisparado);
    }

    /// <summary>
    /// Hora ilegível não grava — e não grava a antiga em silêncio, que é o que a janela de
    /// tarefa faz com o prazo. Aqui a data É o item: um horário errado faz o aviso sair na
    /// hora errada.
    /// </summary>
    [Fact]
    public async Task HoraIlegivel_NaoGravaEAvisaNaJanela()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        var (_, _, existente) = await svc.SalvarAsync(new Compromisso
        {
            Titulo = "Reunião", Quando = new DateTime(2026, 8, 20, 14, 0, 0),
        });

        TelaDeTeste.Executar(() =>
        {
            var janela = Montar(svc);
            janela.Abrir(existente!);

            Campo(janela, "TxtHora").Text = "abc";
            janela.Fechar();

            // Vermelho e com ícone, não o cinza de "salvo": a recusa precisa se distinguir
            // de uma confirmação para alguém que só passou o olho no rodapé.
            var estado = (LinhaDeEstado)janela.FindName("Estado")!;
            Assert.Contains("não gravei", estado.Texto, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(TomDoEstado.Erro, estado.Tom);
        });

        await Task.Delay(120);
        Assert.Equal(new DateTime(2026, 8, 20, 14, 0, 0),
            (await svc.ObterPorIdAsync(existente!.Id))!.Quando);
    }

    [Fact]
    public async Task Apagar_ExigeDoisCliquesEEntaoRemove()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        var (_, _, existente) = await svc.SalvarAsync(new Compromisso
        {
            Titulo = "Some daqui", Quando = new DateTime(2026, 8, 20, 14, 0, 0),
        });

        TelaDeTeste.Executar(() =>
        {
            var janela = Montar(svc);
            janela.Abrir(existente!);

            var apagar = (Button)janela.FindName("BtnApagar")!;
            Clicar(apagar);
            Assert.Equal("Apagar mesmo?", apagar.Content);
            Assert.NotNull(janela.LembreteAtual);

            Clicar(apagar);
        });

        await EsperarLista(svc, l => !l.Any());
        Assert.Empty(await svc.ObterTodosAsync());
    }

    /// <summary>Marcar como feito pela janelinha cala os avisos: a varredura só vê os agendados.</summary>
    [Fact]
    public async Task MarcarComoFeito_TiraOLembreteDaAgenda()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        var (_, _, existente) = await svc.SalvarAsync(new Compromisso
        {
            Titulo                 = "Já resolvido",
            Quando                 = new DateTime(2026, 8, 20, 14, 0, 0),
            AvisoNaHoraMinutos     = Antecedencia.PadraoNaHora,
        });

        TelaDeTeste.Executar(() =>
        {
            var janela = Montar(svc);
            janela.Abrir(existente!);
            Clicar((Button)janela.FindName("BtnFeito")!);
            janela.Fechar();
        });

        var gravado = Assert.Single(await EsperarLista(svc, l => l.Any(c => c.Realizado)));
        Assert.True(gravado.Realizado);
    }

    private static void Clicar(Button botao) =>
        botao.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

    private static TextBox Campo(Window janela, string nome) => (TextBox)janela.FindName(nome)!;

    private static MacroHelper.UI.Controls.SeletorDeData Seletor(Window janela, string nome) =>
        (MacroHelper.UI.Controls.SeletorDeData)janela.FindName(nome)!;

    /// <summary>Escolhe pelo Tag, que é como o code-behind lê os minutos de antecedência.</summary>
    private static void Escolher(Window janela, string combo, string tag)
    {
        var caixa = (System.Windows.Controls.ComboBox)janela.FindName(combo)!;
        caixa.SelectedItem = caixa.Items.OfType<ComboBoxItem>().Single(i => (string?)i.Tag == tag);
    }

    private static CompromissoService Servico(BancoDeTeste banco) =>
        new(banco.Compromissos, new RelogioFalso(QuartaAs9));

    private static CompromissoFlutuanteWindow Montar(CompromissoService svc) =>
        new(svc, new RelogioFalso(QuartaAs9));

    /// <summary>A gravação sai em segundo plano; esperar pelo estado evita depender do relógio.</summary>
    private static async Task<List<Compromisso>> EsperarLista(
        CompromissoService svc, Func<List<Compromisso>, bool> ate)
    {
        var lista = new List<Compromisso>();

        for (var tentativa = 0; tentativa < 60; tentativa++)
        {
            lista = (await svc.ObterTodosAsync()).ToList();
            if (ate(lista)) return lista;
            await Task.Delay(20);
        }

        return lista;
    }
}
