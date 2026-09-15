using MacroHelper.Core.Entities;
using MacroHelper.Services;
using MacroHelper.UI.Views;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace MacroHelper.Tests;

/// <summary>
/// A tarefa aberta ao lado da busca rápida — criar, editar e apagar sem abrir o app.
/// A janela não tem ViewModel: lê e escreve nos próprios campos, então o jeito de exercitá-la
/// é montá-la de verdade e mexer nos campos como o teclado mexeria.
/// </summary>
public class TarefaFlutuanteTests
{
    private static readonly DateTime Hoje = new(2026, 8, 20, 9, 0, 0);

    [Fact]
    public async Task Nova_GravaATarefaEscritaAoFechar()
    {
        using var banco = new BancoDeTeste();
        var svc = new TarefaService(banco.Tarefas, new RelogioFalso(Hoje));

        TelaDeTeste.Executar(() =>
        {
            var janela = Montar(banco, svc);
            janela.Nova();

            Campo(janela, "TxtTitulo").Text      = "Ligar para o contador";
            Campo(janela, "TxtObservacoes").Text = "sobre o holerite";
            ((RadioButton)janela.FindName("RbAlta")!).IsChecked = true;

            janela.Fechar();
        });

        var criada = Assert.Single(await EsperarLista(svc, t => t.Any()));
        Assert.Equal("Ligar para o contador", criada.Titulo);
        Assert.Equal("sobre o holerite",      criada.Observacoes);
        Assert.Equal(PrioridadeTarefa.Alta,   criada.Prioridade);
    }

    /// <summary>Sem título não há tarefa: um Ctrl+N que ninguém preencheu não pode virar linha no banco.</summary>
    [Fact]
    public async Task NovaSemTitulo_NaoGravaNada()
    {
        using var banco = new BancoDeTeste();
        var svc = new TarefaService(banco.Tarefas, new RelogioFalso(Hoje));

        TelaDeTeste.Executar(() =>
        {
            var janela = Montar(banco, svc);
            janela.Nova();
            janela.Fechar();
        });

        await Task.Delay(120);
        Assert.Empty(await svc.ObterTodasAsync());
    }

    [Fact]
    public async Task Editar_GravaPrazoELembreteDigitados()
    {
        using var banco = new BancoDeTeste();
        var svc = new TarefaService(banco.Tarefas, new RelogioFalso(Hoje));
        await banco.Tarefas.InsertAsync(new Tarefa { Titulo = "Revisar contrato" });

        var tarefa = (await svc.ObterTodasAsync()).Single();

        TelaDeTeste.Executar(() =>
        {
            var janela = Montar(banco, svc);
            janela.Abrir(tarefa);

            Seletor(janela, "CampoPrazo").Texto        = "28/08/2026";
            Seletor(janela, "CampoLembreteData").Texto = "27/08/2026";
            Campo(janela, "TxtLembreteHora").Text      = "14:30";

            janela.Fechar();
        });

        var gravada = Assert.Single(await EsperarLista(svc, t => t.Any(x => x.Prazo != null)));
        Assert.Equal(new DateTime(2026, 8, 28), gravada.Prazo);
        Assert.Equal(new DateTime(2026, 8, 27, 14, 30, 0), gravada.Lembrete);
    }

    /// <summary>
    /// Lembrete marcado para depois do prazo avisaria de uma tarefa já vencida. A janela grava
    /// o resto da edição e deixa só o lembrete de fora, em vez de recusar a tarefa inteira: ela
    /// grava sozinha ao sair, e uma recusa aqui perderia tudo que foi digitado sem ninguém ver.
    /// </summary>
    [Fact]
    public async Task LembreteDepoisDoPrazo_GravaORestoESoDeixaOLembreteDeFora()
    {
        using var banco = new BancoDeTeste();
        var svc = new TarefaService(banco.Tarefas, new RelogioFalso(Hoje));

        TelaDeTeste.Executar(() =>
        {
            var janela = Montar(banco, svc);
            janela.Nova();

            Campo(janela, "TxtTitulo").Text             = "Entregar o relatório";
            Seletor(janela, "CampoPrazo").Texto         = "25/08/2026";
            Seletor(janela, "CampoLembreteData").Texto  = "26/08/2026";
            Campo(janela, "TxtLembreteHora").Text       = "09:00";

            janela.Fechar();
        });

        var criada = Assert.Single(await EsperarLista(svc, t => t.Any()));
        Assert.Equal("Entregar o relatório", criada.Titulo);
        Assert.Equal(new DateTime(2026, 8, 25), criada.Prazo);
        Assert.Null(criada.Lembrete);
    }

    /// <summary>
    /// Data pela metade — o campo tem máscara, dá para sair dele com "28/0" — não pode apagar
    /// em silêncio o prazo que já existia, nem levar junto o resto da edição.
    /// </summary>
    [Fact]
    public async Task PrazoIlegivel_MantemOAnteriorEGravaOResto()
    {
        using var banco = new BancoDeTeste();
        var svc = new TarefaService(banco.Tarefas, new RelogioFalso(Hoje));
        await banco.Tarefas.InsertAsync(new Tarefa { Titulo = "Com prazo", Prazo = new DateTime(2026, 8, 25) });

        var tarefa = (await svc.ObterTodasAsync()).Single();

        TelaDeTeste.Executar(() =>
        {
            var janela = Montar(banco, svc);
            janela.Abrir(tarefa);

            Seletor(janela, "CampoPrazo").Texto = "28/0";
            Campo(janela, "TxtTitulo").Text     = "Com prazo, renomeada";

            janela.Fechar();
        });

        var gravada = Assert.Single(await EsperarLista(svc, t => t.Any(x => x.Titulo.EndsWith("renomeada"))));
        Assert.Equal("Com prazo, renomeada", gravada.Titulo);
        Assert.Equal(new DateTime(2026, 8, 25), gravada.Prazo);
    }

    [Fact]
    public async Task Apagar_ExigeDoisCliquesEEntaoRemove()
    {
        using var banco = new BancoDeTeste();
        var svc = new TarefaService(banco.Tarefas, new RelogioFalso(Hoje));
        await banco.Tarefas.InsertAsync(new Tarefa { Titulo = "Some daqui" });

        var tarefa = (await svc.ObterTodasAsync()).Single();

        TelaDeTeste.Executar(() =>
        {
            var janela = Montar(banco, svc);
            janela.Abrir(tarefa);

            var apagar = (Button)janela.FindName("BtnApagar")!;
            Clicar(apagar);
            Assert.Equal("Apagar mesmo?", apagar.Content);
            Assert.NotNull(janela.TarefaAtual);

            Clicar(apagar);
        });

        await EsperarLista(svc, t => !t.Any());
        Assert.Empty(await svc.ObterTodasAsync());
    }

    private static void Clicar(Button botao) =>
        botao.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

    private static TextBox Campo(Window janela, string nome) => (TextBox)janela.FindName(nome)!;

    private static MacroHelper.UI.Controls.SeletorDeData Seletor(Window janela, string nome) =>
        (MacroHelper.UI.Controls.SeletorDeData)janela.FindName(nome)!;

    private static TarefaFlutuanteWindow Montar(BancoDeTeste banco, TarefaService svc) =>
        new(svc, new RelogioFalso(Hoje));

    /// <summary>A gravação sai em segundo plano; esperar pelo estado evita depender do relógio.</summary>
    private static async Task<List<Tarefa>> EsperarLista(TarefaService svc, Func<List<Tarefa>, bool> ate)
    {
        var lista = new List<Tarefa>();

        for (var tentativa = 0; tentativa < 60; tentativa++)
        {
            lista = (await svc.ObterTodasAsync()).ToList();
            if (ate(lista)) return lista;
            await Task.Delay(20);
        }

        return lista;
    }
}
