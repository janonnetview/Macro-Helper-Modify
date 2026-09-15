using MacroHelper.Core.Entities;
using MacroHelper.Services;
using MacroHelper.UI.ViewModels;
using MacroHelper.UI.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MacroHelper.Tests;

/// <summary>
/// As duas grades da tela de Lembretes: a semana e o mês.
///
/// A lista respondia "o que vem agora"; a grade responde "como está o meu dia", e a diferença
/// entre as duas é o CHOQUE DE HORÁRIO, que na lista só existia como frase no aviso de quem
/// acabou de salvar. Por isso boa parte destes testes é sobre choque.
///
/// A tela também é montada de verdade em um deles: um erro de recurso ou de sintaxe no XAML só
/// aparece quando alguém navega até ela, e num teste de compilação isso nunca acontece.
/// </summary>
public class AgendaCalendarioTests
{
    /// <summary>Uma quarta-feira. A semana dela vai de segunda 17 a domingo 23 de agosto de 2026.</summary>
    private static readonly DateTime QuartaAs9 = new(2026, 8, 19, 9, 0, 0);

    private static CompromissosViewModel Montar(BancoDeTeste banco, string modo)
    {
        var relogio = new RelogioFalso(QuartaAs9);
        var vm = new CompromissosViewModel(new CompromissoService(banco.Compromissos, relogio), relogio)
        {
            Modo = modo,
        };
        return vm;
    }

    private static Compromisso Novo(string titulo, DateTime quando, int? duracao = null) =>
        new() { Titulo = titulo, Quando = quando, DuracaoMinutos = duracao };

    [Fact]
    public async Task ASemana_ComecaNaSegundaETemSeteDias()
    {
        using var banco = new BancoDeTeste();
        var vm = Montar(banco, "semana");
        await vm.CarregarAsync();

        Assert.Equal(7, vm.DiasDaSemana.Count);
        Assert.Equal(new DateTime(2026, 8, 17), vm.DiasDaSemana[0].Data);
        Assert.Equal(new DateTime(2026, 8, 23), vm.DiasDaSemana[6].Data);
        Assert.Equal("Seg", vm.DiasDaSemana[0].DiaDaSemanaTexto);
    }

    [Fact]
    public async Task OCompromisso_CaiNoDiaCertoDaSemana()
    {
        using var banco = new BancoDeTeste();
        await banco.Compromissos.InsertAsync(Novo("Visita do técnico", new DateTime(2026, 8, 20, 14, 0, 0), 60));

        var vm = Montar(banco, "semana");
        await vm.CarregarAsync();

        var quinta = vm.DiasDaSemana.Single(d => d.Data == new DateTime(2026, 8, 20));
        var bloco  = Assert.Single(quinta.Blocos);

        Assert.Equal("Visita do técnico", bloco.Compromisso.Titulo);
        Assert.Equal(14 * 60, bloco.InicioMinutos);
        Assert.All(vm.DiasDaSemana.Where(d => d.Data != quinta.Data), d => Assert.Empty(d.Blocos));
    }

    /// <summary>
    /// O que a grade existe para mostrar: dois compromissos no mesmo horário aparecem lado a
    /// lado, marcados, e o dia inteiro ganha o sinal de choque no cabeçalho.
    /// </summary>
    [Fact]
    public async Task DoisNoMesmoHorario_FicamLadoALadoEODiaEhMarcado()
    {
        using var banco = new BancoDeTeste();
        var quinta = new DateTime(2026, 8, 20);
        await banco.Compromissos.InsertAsync(Novo("Visita do técnico", quinta.AddHours(14), 60));
        await banco.Compromissos.InsertAsync(Novo("Reunião do time",   quinta.AddHours(14).AddMinutes(30), 60));

        var vm = Montar(banco, "semana");
        await vm.CarregarAsync();

        var dia = vm.DiasDaSemana.Single(d => d.Data == quinta);

        Assert.True(dia.TemChoque);
        Assert.All(dia.Blocos, b => Assert.Equal(2, b.TotalColunas));
        Assert.All(dia.Blocos, b => Assert.True(b.Choca));
    }

    /// <summary>
    /// A faixa de horas cresce para caber o que existe, e não encolhe abaixo do dia de
    /// trabalho: uma semana com um compromisso só continuaria sendo uma semana.
    /// </summary>
    [Fact]
    public async Task AFaixaDeHoras_AbreParaCaberOQueComecaCedoOuTerminaTarde()
    {
        using var banco = new BancoDeTeste();
        var vm = Montar(banco, "semana");
        await vm.CarregarAsync();

        var primeiraHoraVazia = vm.DiasDaSemana[0].PrimeiraHora;
        var ultimaHoraVazia   = vm.DiasDaSemana[0].UltimaHora;
        Assert.Equal(8,  primeiraHoraVazia);
        Assert.Equal(19, ultimaHoraVazia);

        await banco.Compromissos.InsertAsync(Novo("Entrega da madrugada", new DateTime(2026, 8, 18, 6, 30, 0)));
        await banco.Compromissos.InsertAsync(Novo("Plantão", new DateTime(2026, 8, 18, 21, 0, 0), 60));
        await vm.CarregarAsync();

        Assert.Equal(6,  vm.DiasDaSemana[0].PrimeiraHora);
        Assert.Equal(22, vm.DiasDaSemana[0].UltimaHora);
        Assert.Equal(vm.DiasDaSemana[0].UltimaHora - vm.DiasDaSemana[0].PrimeiraHora, vm.HorasDaGrade.Count);
    }

    [Fact]
    public async Task OMes_TemSeisSemanasCheiasEMarcaOsDiasDeFora()
    {
        using var banco = new BancoDeTeste();
        var vm = Montar(banco, "mes");
        await vm.CarregarAsync();

        Assert.Equal(42, vm.DiasDoMes.Count);

        // Agosto de 2026 começa num sábado: a grade abre na segunda anterior, dia 27 de julho.
        Assert.Equal(new DateTime(2026, 7, 27), vm.DiasDoMes[0].Data);
        Assert.True(vm.DiasDoMes[0].ForaDoMes);
        Assert.False(vm.DiasDoMes.Single(d => d.Data == new DateTime(2026, 8, 19)).ForaDoMes);
    }

    [Fact]
    public async Task ACelulaDoMes_MostraAsPrimeirasEContaOResto()
    {
        using var banco = new BancoDeTeste();
        var quinta = new DateTime(2026, 8, 20);
        for (var i = 0; i < 5; i++)
            await banco.Compromissos.InsertAsync(Novo($"Compromisso {i}", quinta.AddHours(9 + i)));

        var vm = Montar(banco, "mes");
        await vm.CarregarAsync();

        var dia = vm.DiasDoMes.Single(d => d.Data == quinta);

        Assert.Equal(DiaDaAgenda.EtiquetasNoMes, dia.Itens.Count);
        Assert.Equal(5 - DiaDaAgenda.EtiquetasNoMes, dia.Extras);
        Assert.True(dia.TemExtras);
        Assert.Equal("+2", dia.ExtrasTexto);
    }

    [Fact]
    public async Task ASetaDoPeriodo_AndaUmaSemanaOuUmMes()
    {
        using var banco = new BancoDeTeste();
        var vm = Montar(banco, "semana");
        await vm.CarregarAsync();

        vm.PeriodoSeguinteCommand.Execute(null);
        Assert.Equal(new DateTime(2026, 8, 24), vm.DiasDaSemana[0].Data);

        vm.PeriodoAnteriorCommand.Execute(null);
        Assert.Equal(new DateTime(2026, 8, 17), vm.DiasDaSemana[0].Data);

        vm.Modo = "mes";
        vm.PeriodoSeguinteCommand.Execute(null);
        Assert.Contains("Setembro", vm.TituloDoPeriodo);

        vm.IrParaHojeCommand.Execute(null);
        Assert.Contains("Agosto", vm.TituloDoPeriodo);
    }

    [Fact]
    public async Task OTituloDoPeriodo_DizOQueEstaNaTela()
    {
        using var banco = new BancoDeTeste();
        var vm = Montar(banco, "semana");
        await vm.CarregarAsync();

        Assert.Equal("17 a 23 de agosto de 2026", vm.TituloDoPeriodo);

        vm.Modo = "mes";
        Assert.Equal("Agosto de 2026", vm.TituloDoPeriodo);
    }

    /// <summary>A semana que cai entre dois meses precisa dizer os dois.</summary>
    [Fact]
    public async Task OTituloDaSemana_NomeiaOsDoisMesesQuandoASemanaViraOMes()
    {
        using var banco = new BancoDeTeste();
        var vm = Montar(banco, "semana");
        await vm.CarregarAsync();

        vm.Ancora = new DateTime(2026, 9, 2);

        Assert.Equal("31 de agosto a 06 de setembro de 2026", vm.TituloDoPeriodo);
    }

    /// <summary>
    /// O lembrete sem data não cabe em grade nenhuma. Sumir com ele seria pior do que não ter
    /// grade, porque ele é justamente o que espera decisão — então a grade conta quantos são.
    /// </summary>
    [Fact]
    public async Task OQueNaoTemData_EhContadoParaATarjaDoCalendario()
    {
        using var banco = new BancoDeTeste();
        await banco.Compromissos.InsertAsync(new Compromisso { Titulo = "Visita a remarcar" });
        await banco.Compromissos.InsertAsync(Novo("Com hora", new DateTime(2026, 8, 20, 14, 0, 0)));

        var vm = Montar(banco, "semana");
        await vm.CarregarAsync();

        Assert.Equal(1, vm.TotalSemData);
        Assert.All(vm.DiasDaSemana, d => Assert.All(d.Blocos, b => Assert.True(b.Compromisso.TemData)));
    }

    [Fact]
    public async Task ClicarNumDia_AbreOFormularioJaNaqueleDia()
    {
        using var banco = new BancoDeTeste();
        var vm = Montar(banco, "mes");
        await vm.CarregarAsync();

        vm.NovoNoDiaCommand.Execute(new DateTime(2026, 8, 27));

        Assert.True(vm.MostrarFormulario);
        Assert.Equal("27/08/2026", vm.FormData);
        Assert.Equal("09:00", vm.FormHora);
    }

    [Fact]
    public void ModoDesconhecido_CaiNaLista()
    {
        using var banco = new BancoDeTeste();
        var vm = Montar(banco, "lista");

        vm.DefinirModoCommand.Execute("qualquer coisa");

        Assert.Equal("lista", vm.Modo);
        Assert.False(vm.MostrarCalendario);
    }

    /// <summary>
    /// A grade montada de verdade. Prova que o XAML abre (chave de recurso errada só estoura
    /// ao navegar até a tela) e que o bloco da semana chega ao comando de editar: ele mora
    /// dentro de dois ItemTemplate aninhados, e de lá o binding pode simplesmente não achar o
    /// ViewModel — sem erro, sem aviso, sem efeito ao clicar.
    /// </summary>
    [Fact]
    public async Task AGradeDaSemana_MontaEOBlocoAbreOCompromissoAoSerClicado()
    {
        using var banco = new BancoDeTeste();
        await banco.Compromissos.InsertAsync(Novo("Visita do técnico", new DateTime(2026, 8, 20, 14, 0, 0), 60));

        var vm = Montar(banco, "semana");
        await vm.CarregarAsync();

        TelaDeTeste.Executar(() =>
        {
            var tela = new CompromissosView { DataContext = vm };
            tela.Measure(new Size(1200, 800));
            tela.Arrange(new Rect(new Size(1200, 800)));
            tela.UpdateLayout();

            var bloco = Achar<Button>(tela, b =>
                b.CommandParameter is Compromisso &&
                ReferenceEquals(b.Command, vm.EditarCompromissoCommand));

            Assert.True(bloco != null, "o bloco da grade não está ligado ao comando de editar");
            bloco!.Command.Execute(bloco.CommandParameter);
        });

        Assert.True(vm.MostrarFormulario);
        Assert.Equal("Visita do técnico", vm.FormTexto);
        Assert.Equal("20/08/2026", vm.FormData);
    }

    /// <summary>
    /// O corpo da grade é clicável de meia em meia hora. É o que faz a semana valer como
    /// agenda: sem isto ela só servia para olhar, e marcar exigia voltar ao botão do alto e
    /// digitar dia e hora à mão.
    /// </summary>
    [Fact]
    public async Task CadaDia_TemUmaFaixaClicavelPorMeiaHora()
    {
        using var banco = new BancoDeTeste();
        var vm = Montar(banco, "semana");
        await vm.CarregarAsync();

        var segunda = vm.DiasDaSemana[0];
        var horas   = segunda.UltimaHora - segunda.PrimeiraHora;

        Assert.Equal(horas * 2, segunda.Faixas.Count);
        Assert.Equal(new DateTime(2026, 8, 17, 8, 0, 0),  segunda.Faixas[0].Inicio);
        Assert.Equal(new DateTime(2026, 8, 17, 8, 30, 0), segunda.Faixas[1].Inicio);

        // A linha da hora é da faixa cheia; a das e meia não tem linha, só o realce do mouse.
        Assert.True(segunda.Faixas[0].InicioDeHora);
        Assert.False(segunda.Faixas[1].InicioDeHora);

        // A faixa é meia hora, então ela vale metade da altura de uma hora.
        Assert.Equal(CompromissosViewModel.AlturaDaHora / 2, segunda.Faixas[0].Altura);
    }

    [Fact]
    public async Task AsFaixasDeCadaDia_SaoDoDiaDelas()
    {
        using var banco = new BancoDeTeste();
        var vm = Montar(banco, "semana");
        await vm.CarregarAsync();

        foreach (var dia in vm.DiasDaSemana)
            Assert.All(dia.Faixas, f => Assert.Equal(dia.Data, f.Inicio.Date));
    }

    [Fact]
    public async Task ClicarNumaFaixa_AbreOFormularioNaquelaHora()
    {
        using var banco = new BancoDeTeste();
        var vm = Montar(banco, "semana");
        await vm.CarregarAsync();

        vm.NovoNoInstanteCommand.Execute(new DateTime(2026, 8, 20, 14, 30, 0));

        Assert.True(vm.MostrarFormulario);
        Assert.Equal("20/08/2026", vm.FormData);
        Assert.Equal("14:30", vm.FormHora);
    }

    /// <summary>
    /// A grade montada de verdade, do lado de criar. O botão da faixa mora dentro de dois
    /// ItemTemplate aninhados, e de lá o binding pode não achar o ViewModel: a tela abriria
    /// igual e o clique não faria nada.
    /// </summary>
    [Fact]
    public async Task AGradeDaSemana_MarcaUmLembreteNaHoraEmQueSeClicou()
    {
        using var banco = new BancoDeTeste();
        var vm = Montar(banco, "semana");
        await vm.CarregarAsync();

        var alvo = new DateTime(2026, 8, 20, 15, 30, 0);

        TelaDeTeste.Executar(() =>
        {
            var tela = new CompromissosView { DataContext = vm };
            tela.Measure(new Size(1200, 800));
            tela.Arrange(new Rect(new Size(1200, 800)));
            tela.UpdateLayout();

            var faixa = Achar<Button>(tela, b =>
                Equals(b.CommandParameter, alvo) &&
                ReferenceEquals(b.Command, vm.NovoNoInstanteCommand));

            Assert.True(faixa != null, "a faixa das 15h30 não está ligada ao comando de criar");
            faixa!.Command.Execute(faixa.CommandParameter);
        });

        Assert.True(vm.MostrarFormulario);
        Assert.Equal("20/08/2026", vm.FormData);
        Assert.Equal("15:30", vm.FormHora);
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
