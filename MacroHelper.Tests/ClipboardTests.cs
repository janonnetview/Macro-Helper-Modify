using MacroHelper.Services;

namespace MacroHelper.Tests;

/// <summary>
/// O histórico da área de transferência sobre o banco de verdade.
///
/// A captura em si (a mensagem do Windows, os formatos de exclusão dos gerenciadores de senha)
/// mora na UI e não cabe num teste offline. O que cabe — e é onde estão as decisões — é o que
/// acontece depois que o texto chega: o que entra, o que sobe, o que é podado e o que fica.
/// </summary>
public class ClipboardTests
{
    private static ClipboardService Servico(BancoDeTeste banco) => new(banco.Clipboard);

    [Fact]
    public async Task Registrar_GuardaOTextoEAOrigem()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        await svc.RegistrarAsync("Chamado 4412", "Portal de chamados — Chrome");

        var item = (await svc.ObterTodosAsync()).Single();
        Assert.Equal("Chamado 4412", item.Conteudo);
        Assert.Equal("Portal de chamados — Chrome", item.Origem);
    }

    /// <summary>
    /// Copiar de novo o mesmo texto NÃO cria linha nova: sobe a data e volta ao topo, como
    /// fazem o Win+V e o Ditto. Sem isso, uma tarde recopiando o mesmo número de chamado
    /// encheria a lista com oito cópias dele e empurraria todo o resto para fora da retenção.
    /// </summary>
    [Fact]
    public async Task Registrar_TextoRepetido_NaoDuplicaESobeParaOTopo()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        await svc.RegistrarAsync("primeiro", null);
        await svc.RegistrarAsync("segundo", null);
        await svc.RegistrarAsync("primeiro", "Outra janela");

        var todos = (await svc.ObterTodosAsync()).ToList();

        Assert.Equal(2, todos.Count);
        Assert.Equal("primeiro", todos[0].Conteudo);
        Assert.Equal("Outra janela", todos[0].Origem);
    }

    /// <summary>Caixa diferente é texto diferente: "OK" e "ok" são duas coisas para colar.</summary>
    [Fact]
    public async Task Registrar_DiferenciaMaiusculaDeMinuscula()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        await svc.RegistrarAsync("OK", null);
        await svc.RegistrarAsync("ok", null);

        Assert.Equal(2, (await svc.ObterTodosAsync()).Count());
    }

    /// <summary>
    /// Selecionar sem querer e apertar Ctrl+C é comum, e uma linha em branco no histórico não
    /// serve para nada. Texto gigante também não entra: copiar um arquivo inteiro é operação
    /// de passagem, e guardá-la empurraria para fora as dez cópias curtas que se quer de volta.
    /// </summary>
    [Fact]
    public async Task Registrar_IgnoraVazioEExcessivamenteGrande()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        Assert.Null(await svc.RegistrarAsync(null, null));
        Assert.Null(await svc.RegistrarAsync("   \r\n  ", null));
        Assert.Null(await svc.RegistrarAsync(new string('x', ClipboardService.TamanhoMaximo + 1), null));

        Assert.Empty(await svc.ObterTodosAsync());
    }

    /// <summary>
    /// A poda é por CONTAGEM: o histórico serve para voltar algumas cópias atrás, e quantas
    /// cabem é o que se consegue percorrer numa lista — não quantos dias se passaram.
    /// </summary>
    [Fact]
    public async Task Registrar_PodaOsMaisAntigosAlemDoLimite()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        for (var i = 0; i < ClipboardService.LimiteHistorico + 5; i++)
            await svc.RegistrarAsync($"texto {i}", null);

        var todos = (await svc.ObterTodosAsync()).ToList();

        Assert.Equal(ClipboardService.LimiteHistorico, todos.Count);
        Assert.Equal($"texto {ClipboardService.LimiteHistorico + 4}", todos[0].Conteudo);
        Assert.DoesNotContain(todos, t => t.Conteudo == "texto 0");
    }

    /// <summary>
    /// Fixado é o que separa "copiei agora" de "isto eu uso todo dia". A poda não pode
    /// alcançá-lo — é o único conteúdo dessa lista que alguém escolheu a dedo.
    /// </summary>
    [Fact]
    public async Task Fixado_SobreviveAPodaEFicaNoTopo()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        var antigo = await svc.RegistrarAsync("assinatura de e-mail", null);
        await svc.FixarAsync(antigo!.Id, true);

        for (var i = 0; i < ClipboardService.LimiteHistorico + 5; i++)
            await svc.RegistrarAsync($"texto {i}", null);

        var todos = (await svc.ObterTodosAsync()).ToList();

        Assert.Equal("assinatura de e-mail", todos[0].Conteudo);
        Assert.True(todos[0].Fixado);
        Assert.Equal(ClipboardService.LimiteHistorico + 1, todos.Count);
    }

    [Fact]
    public async Task Limpar_EsvaziaMasPreservaOsFixados()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        var fixado = await svc.RegistrarAsync("guardar", null);
        await svc.FixarAsync(fixado!.Id, true);
        await svc.RegistrarAsync("descartável 1", null);
        await svc.RegistrarAsync("descartável 2", null);

        var (ok, msg) = await svc.LimparAsync();

        Assert.True(ok);
        Assert.Contains("2 item", msg);
        Assert.Equal("guardar", (await svc.ObterTodosAsync()).Single().Conteudo);
    }

    /// <summary>Mesma busca das notas: acha "Ação" procurando "acao".</summary>
    [Fact]
    public async Task Busca_IgnoraAcentoECaixa()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        await svc.RegistrarAsync("Plano de Ação do trimestre", null);
        await svc.RegistrarAsync("Lista de compras", null);

        Assert.Single(await svc.PesquisarAsync("acao"));
        Assert.Single(await svc.PesquisarAsync("AÇÃO"));
        Assert.Empty(await svc.PesquisarAsync("orçamento"));
    }

    /// <summary>O % é curinga do LIKE: sem escapar, procurar "50%" listaria o histórico inteiro.</summary>
    [Fact]
    public async Task Busca_NaoTrataPorcentoComoCuringa()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        await svc.RegistrarAsync("desconto de 50% no plano", null);
        await svc.RegistrarAsync("nada a ver", null);

        Assert.Single(await svc.PesquisarAsync("50%"));
    }

    [Fact]
    public async Task Excluir_TiraSoOItemPedido()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        var alvo = await svc.RegistrarAsync("some", null);
        await svc.RegistrarAsync("fica", null);

        await svc.ExcluirAsync(alvo!.Id);

        Assert.Equal("fica", (await svc.ObterTodosAsync()).Single().Conteudo);
    }

    /// <summary>
    /// O resumo é o que identifica o item na lista: uma linha só, com as corridas de espaço
    /// colapsadas — texto copiado de tabela ou de PDF vem cheio delas, e na lista elas viram
    /// buracos sem significado. O conteúdo que vai para o outro app continua intacto.
    /// </summary>
    [Fact]
    public void Resumo_ViraUmaLinhaSemDeformarOConteudo()
    {
        var item = new MacroHelper.Core.Entities.ItemClipboard
        {
            Conteudo = "Primeira linha\r\n\r\nSegunda    com     espaços",
        };

        Assert.Equal("Primeira linha Segunda com espaços", item.Resumo);
        Assert.Contains("\r\n", item.Conteudo);
        Assert.Equal($"3 linhas · {item.Conteudo.Length} caracteres", item.Medida);
    }
}
