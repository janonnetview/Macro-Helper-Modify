using MacroHelper.Core.Entities;
using MacroHelper.Services;

namespace MacroHelper.Tests;

public class MacroServiceTests
{
    private static MacroService Servico(BancoDeTeste banco) =>
        new(banco.Macros, banco.Versoes, new VariavelGlobalService(banco.Variaveis, banco.Macros));

    // ── Macros aninhadas ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Aninhada_ExpandeAReferencia()
    {
        using var banco = new BancoDeTeste();
        await banco.Macros.InsertAsync(MacroRepositoryTests.Nova("assinatura", conteudo: "Atenciosamente,\nRaul"));
        var servico = Servico(banco);

        var resultado = await servico.ResolverMacrosAninhadasAsync("Segue o retorno.\n\n{macro:assinatura}");

        Assert.Equal("Segue o retorno.\n\nAtenciosamente,\nRaul", resultado);
    }

    [Fact]
    public async Task Aninhada_ExpandeEmCadeia()
    {
        using var banco = new BancoDeTeste();
        await banco.Macros.InsertAsync(MacroRepositoryTests.Nova("nivel-3", conteudo: "fim"));
        await banco.Macros.InsertAsync(MacroRepositoryTests.Nova("nivel-2", conteudo: "b {macro:nivel-3}"));
        await banco.Macros.InsertAsync(MacroRepositoryTests.Nova("nivel-1", conteudo: "a {macro:nivel-2}"));

        Assert.Equal("a b fim", await Servico(banco).ResolverMacrosAninhadasAsync("{macro:nivel-1}"));
    }

    /// <summary>Duas macros que se referenciam parariam o app sem o limite de profundidade.</summary>
    [Fact]
    public async Task Aninhada_ComCiclo_ParaNoLimiteDeProfundidade()
    {
        using var banco = new BancoDeTeste();
        await banco.Macros.InsertAsync(MacroRepositoryTests.Nova("ping", conteudo: "ping {macro:pong}"));
        await banco.Macros.InsertAsync(MacroRepositoryTests.Nova("pong", conteudo: "pong {macro:ping}"));

        var resultado = await Servico(banco).ResolverMacrosAninhadasAsync("{macro:ping}");

        // Termina, e o que sobra é a referência literal do nível que não foi expandido.
        Assert.StartsWith("ping pong ping", resultado);
        Assert.Contains("{macro:", resultado);
    }

    [Fact]
    public async Task Aninhada_ReferenciaInexistente_ViraVazio()
    {
        using var banco = new BancoDeTeste();
        Assert.Equal("antes  depois",
            await Servico(banco).ResolverMacrosAninhadasAsync("antes {macro:nao-existe} depois"));
    }

    /// <summary>
    /// O bug achado nos dados da Onda 0: uma global com valor em branco substituía {nome} por
    /// vazio ANTES de o app perguntar, apagando o nome em quatro macros. Globais sem valor
    /// precisam ser ignoradas para que o placeholder chegue vivo ao diálogo de variáveis.
    /// </summary>
    [Fact]
    public async Task VariavelGlobalEmBranco_NaoApagaOPlaceholder()
    {
        using var banco = new BancoDeTeste();
        await banco.Variaveis.InsertAsync(new VariavelGlobal { Nome = "nome", ValorPadrao = "" });

        Assert.Equal("Olá {nome}", await Servico(banco).ResolverMacrosAninhadasAsync("Olá {nome}"));
    }

    [Fact]
    public async Task VariavelGlobalComValor_EhSubstituida()
    {
        using var banco = new BancoDeTeste();
        await banco.Variaveis.InsertAsync(new VariavelGlobal { Nome = "empresa", ValorPadrao = "Silk" });

        Assert.Equal("Bem-vindo à Silk", await Servico(banco).ResolverMacrosAninhadasAsync("Bem-vindo à {empresa}"));
    }

    // ── Gravação ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Salvar_NormalizaOAtalho()
    {
        using var banco = new BancoDeTeste();
        var (ok, _, macro) = await Servico(banco).SalvarAsync(new Macro
        {
            Atalho = "  Chamado Recebido  ", Titulo = "T", Conteudo = "C",
        });

        Assert.True(ok);
        Assert.Equal("chamado-recebido", macro!.Atalho);
    }

    /// <summary>Atalho repetido é engano comum do usuário — precisa virar mensagem de formulário, não exceção.</summary>
    [Fact]
    public async Task Salvar_AtalhoDuplicado_VoltaComoMensagemENaoComoExcecao()
    {
        using var banco = new BancoDeTeste();
        var servico = Servico(banco);
        await servico.SalvarAsync(new Macro { Atalho = "ola", Titulo = "T", Conteudo = "C" });

        var (ok, msg, macro) = await servico.SalvarAsync(new Macro { Atalho = "ola", Titulo = "Outra", Conteudo = "X" });

        Assert.False(ok);
        Assert.Null(macro);
        Assert.Equal("Já existe uma macro com esse atalho.", msg);
    }

    [Theory]
    [InlineData("", "T", "C")]
    [InlineData("a", "", "C")]
    [InlineData("a", "T", "")]
    public async Task Salvar_CampoObrigatorioVazio_EhRecusado(string atalho, string titulo, string conteudo)
    {
        using var banco = new BancoDeTeste();
        var (ok, _, _) = await Servico(banco).SalvarAsync(new Macro
        {
            Atalho = atalho, Titulo = titulo, Conteudo = conteudo,
        });
        Assert.False(ok);
    }

    /// <summary>Editar guarda a versão ANTERIOR — é o que a tela de Histórico restaura.</summary>
    [Fact]
    public async Task Editar_GuardaAVersaoAnterior()
    {
        using var banco = new BancoDeTeste();
        var servico = Servico(banco);

        var (_, _, macro) = await servico.SalvarAsync(new Macro { Atalho = "v", Titulo = "Versão 1", Conteudo = "texto 1" });
        macro!.Conteudo = "texto 2";
        await servico.SalvarAsync(macro);

        var versao = (await servico.ObterVersoesAsync(macro.Id)).Single();
        Assert.Equal("texto 1", versao.Conteudo);
    }

    /// <summary>Excluir a macro apaga o histórico junto (ON DELETE CASCADE) — a Ajuda diz isso ao usuário.</summary>
    [Fact]
    public async Task Excluir_LevaOHistoricoJunto()
    {
        using var banco = new BancoDeTeste();
        var servico = Servico(banco);

        var (_, _, macro) = await servico.SalvarAsync(new Macro { Atalho = "v", Titulo = "T", Conteudo = "1" });
        macro!.Conteudo = "2";
        await servico.SalvarAsync(macro);

        Assert.Equal(1, banco.Escalar<int>("SELECT COUNT(*) FROM macro_versoes"));

        await servico.ExcluirAsync(macro.Id);
        Assert.Equal(0, banco.Escalar<int>("SELECT COUNT(*) FROM macro_versoes"));
    }

    /// <summary>O log de uso SOBREVIVE à exclusão (ON DELETE SET NULL) — é por isso que titulo e atalho estão copiados nele.</summary>
    [Fact]
    public async Task Excluir_PreservaOHistoricoDeUso()
    {
        using var banco = new BancoDeTeste();
        var id = await banco.Macros.InsertAsync(MacroRepositoryTests.Nova("usada"));
        await banco.Logs.RegistrarAsync(new LogUso
        {
            MacroId = id, MacroTitulo = "usada", MacroAtalho = "usada", Caracteres = 10,
        });

        await Servico(banco).ExcluirAsync(id);

        var log = (await banco.Logs.GetRecentesAsync()).Single();
        Assert.Null(log.MacroId);
        Assert.Equal("usada", log.MacroTitulo);
    }

    // ── Gatilho ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/ola", 1)]     // com prefixo e 2+ caracteres
    [InlineData("ola", 0)]      // sem prefixo
    [InlineData("/", 0)]        // curto demais
    [InlineData("", 0)]
    public async Task BuscarPorGatilho_ExigePrefixoEDoisCaracteres(string digitado, int esperado)
    {
        using var banco = new BancoDeTeste();
        await banco.Macros.InsertAsync(MacroRepositoryTests.Nova("ola"));

        Assert.Equal(esperado, (await Servico(banco).BuscarPorGatilhoAsync(digitado)).Count());
    }

    [Fact]
    public async Task BuscarPorGatilho_RespeitaOPrefixoCustomizado()
    {
        using var banco = new BancoDeTeste();
        await banco.Macros.InsertAsync(MacroRepositoryTests.Nova("ola"));
        var servico = Servico(banco);

        Assert.Single(await servico.BuscarPorGatilhoAsync("\\ola", '\\'));
        Assert.Empty(await servico.BuscarPorGatilhoAsync("/ola", '\\'));
    }

    // ── Duplicata por semelhança ─────────────────────────────────────────────────

    [Fact]
    public async Task DetectarDuplicata_AchaConteudoQuaseIgual()
    {
        using var banco = new BancoDeTeste();
        await banco.Macros.InsertAsync(MacroRepositoryTests.Nova("original",
            conteudo: "Prezado cliente, seu chamado foi recebido e será analisado em breve."));

        var quaseIgual = MacroRepositoryTests.Nova("copia",
            conteudo: "Prezado cliente, seu chamado foi recebido e será analisado em breve!");

        Assert.NotNull(await Servico(banco).DetectarPossivelDuplicataAsync(quaseIgual));
    }

    [Fact]
    public async Task DetectarDuplicata_IgnoraConteudoDiferente()
    {
        using var banco = new BancoDeTeste();
        await banco.Macros.InsertAsync(MacroRepositoryTests.Nova("original",
            conteudo: "Prezado cliente, seu chamado foi recebido."));

        var outra = MacroRepositoryTests.Nova("outra", conteudo: "Segue em anexo a nota fiscal solicitada.");

        Assert.Null(await Servico(banco).DetectarPossivelDuplicataAsync(outra));
    }

    // ── Importação ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportarCsv_LeCamposEntreAspasComVirgulaQuebraEAspaEscapada()
    {
        using var banco = new BancoDeTeste();
        var csv = """
            Atalho,Titulo,Conteudo,Categoria
            saudacao,"Bom dia, tudo bem?","Primeira linha
            segunda linha",Geral
            aspas,Aspas,"Ele disse ""oi"" para mim",
            """;

        var (ok, _, importadas, _) = await Servico(banco).ImportarCsvAsync(csv);

        Assert.True(ok);
        Assert.Equal(2, importadas);

        var saudacao = await banco.Macros.GetByAtalhoAsync("saudacao");
        Assert.Equal("Bom dia, tudo bem?", saudacao!.Titulo);
        Assert.Equal("Primeira linha\nsegunda linha", saudacao.Conteudo);

        var aspas = await banco.Macros.GetByAtalhoAsync("aspas");
        Assert.Equal("Ele disse \"oi\" para mim", aspas!.Conteudo);
    }

    [Fact]
    public async Task ImportarCsv_SemAsColunasObrigatorias_EhRecusado()
    {
        using var banco = new BancoDeTeste();
        var (ok, msg, _, _) = await Servico(banco).ImportarCsvAsync("Nome,Valor\na,b");

        Assert.False(ok);
        Assert.Contains("Atalho", msg);
    }

    [Fact]
    public async Task ImportarCsv_IgnoraDuplicadasEIncompletas()
    {
        using var banco = new BancoDeTeste();
        await banco.Macros.InsertAsync(MacroRepositoryTests.Nova("ja-existe"));

        var csv = """
            Atalho,Titulo,Conteudo
            ja-existe,Repetida,algum texto
            sem-conteudo,Sem conteúdo,
            nova,Nova,conteúdo válido
            """;

        var (ok, _, importadas, ignoradas) = await Servico(banco).ImportarCsvAsync(csv);

        Assert.True(ok);
        Assert.Equal(1, importadas);
        Assert.Equal(2, ignoradas);
    }

    [Fact]
    public async Task ImportarCsv_DuasLinhasComOMesmoAtalho_SoTrazAPrimeira()
    {
        using var banco = new BancoDeTeste();
        var csv = """
            Atalho,Titulo,Conteudo
            repetido,Primeira,texto 1
            repetido,Segunda,texto 2
            """;

        // Sem a checagem em memória, o índice único abortaria o lote inteiro.
        var (ok, _, importadas, ignoradas) = await Servico(banco).ImportarCsvAsync(csv);

        Assert.True(ok);
        Assert.Equal(1, importadas);
        Assert.Equal(1, ignoradas);
        Assert.Equal("Primeira", (await banco.Macros.GetByAtalhoAsync("repetido"))!.Titulo);
    }

    [Fact]
    public async Task ExportarEImportarJson_FechaOCiclo()
    {
        using var origem = new BancoDeTeste();
        await origem.Macros.InsertAsync(MacroRepositoryTests.Nova("uma", categoria: "Jurídico"));
        await origem.Macros.InsertAsync(MacroRepositoryTests.Nova("outra"));

        var json = await Servico(origem).ExportarJsonAsync();

        using var destino = new BancoDeTeste();
        var (ok, _, importadas, _) = await Servico(destino).ImportarJsonAsync(json);

        Assert.True(ok);
        Assert.Equal(2, importadas);
        Assert.Equal("Jurídico", (await destino.Macros.GetByAtalhoAsync("uma"))!.Categoria);
    }

    [Fact]
    public async Task ImportarJson_Invalido_VoltaMensagemENaoExcecao()
    {
        using var banco = new BancoDeTeste();
        var (ok, msg, _, _) = await Servico(banco).ImportarJsonAsync("isto não é json");

        Assert.False(ok);
        Assert.Contains("JSON inválido", msg);
    }

    /// <summary>Arquivar é reversível e some das buscas — diferente de excluir.</summary>
    [Fact]
    public async Task Arquivar_TiraDaBuscaMasMantemAMacro()
    {
        using var banco = new BancoDeTeste();
        var servico = Servico(banco);
        var (_, _, macro) = await servico.SalvarAsync(new Macro { Atalho = "arq", Titulo = "T", Conteudo = "C" });

        await servico.ArquivarAsync(macro!.Id, arquivar: true);
        Assert.Empty(await servico.PesquisarAsync("arq"));
        Assert.NotNull(await servico.ObterPorIdAsync(macro.Id));

        await servico.ArquivarAsync(macro.Id, arquivar: false);
        Assert.Single(await servico.PesquisarAsync("arq"));
    }
}
