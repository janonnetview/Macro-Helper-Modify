using MacroHelper.Core;
using MacroHelper.Core.Entities;

namespace MacroHelper.Tests;

public class MacroRepositoryTests
{
    // ── Índices únicos: o banco é quem recusa, não o C# ──────────────────────────

    [Fact]
    public async Task Atalho_Duplicado_EhRecusadoPeloIndiceUnico()
    {
        using var banco = new BancoDeTeste();
        await banco.Macros.InsertAsync(Nova("saudacao"));

        var erro = await Assert.ThrowsAsync<RegistroDuplicadoException>(
            () => banco.Macros.InsertAsync(Nova("saudacao")));

        Assert.Equal("atalho", erro.Campo);
        Assert.Equal("Já existe uma macro com esse atalho.", erro.Message);
    }

    /// <summary>
    /// A coluna é COLLATE NOCASE. Sem isso o banco aceitaria "/OLA" e "/ola" como macros
    /// distintas, enquanto o C# — que compara com OrdinalIgnoreCase — trataria as duas como a
    /// mesma: o gatilho resolveria para uma delas ao acaso.
    /// </summary>
    [Fact]
    public async Task Atalho_QueSoDifereNaCaixa_TambemColide()
    {
        using var banco = new BancoDeTeste();
        await banco.Macros.InsertAsync(Nova("ola"));

        await Assert.ThrowsAsync<RegistroDuplicadoException>(
            () => banco.Macros.InsertAsync(Nova("OLA")));
    }

    [Fact]
    public async Task AtalhoDeTecla_Duplicado_EhRecusadoComMensagemPropria()
    {
        using var banco = new BancoDeTeste();
        await banco.Macros.InsertAsync(Nova("um",   tecla: "1"));

        var erro = await Assert.ThrowsAsync<RegistroDuplicadoException>(
            () => banco.Macros.InsertAsync(Nova("outra", tecla: "1")));

        // "macros.atalho" é prefixo de "macros.atalho_tecla" — se a ordem do casamento em
        // ErrosSqlite inverter, esta mensagem vira a do atalho comum.
        Assert.Equal("atalho_tecla", erro.Campo);
        Assert.Contains("atalho de teclado", erro.Message);
    }

    /// <summary>O índice de atalho_tecla é PARCIAL: a esmagadora maioria das macros não tem um, e NULL não pode colidir com NULL.</summary>
    [Fact]
    public async Task VariasMacrosSemAtalhoDeTecla_Convivem()
    {
        using var banco = new BancoDeTeste();

        await banco.Macros.InsertAsync(Nova("a"));
        await banco.Macros.InsertAsync(Nova("b"));
        await banco.Macros.InsertAsync(Nova("c"));

        Assert.Equal(3, banco.Escalar<int>("SELECT COUNT(*) FROM macros"));
    }

    // ── B1: a imagem só vem quando alguém vai editar ──────────────────────────────

    /// <summary>
    /// A listagem exclui imagem_base64 (podem ser centenas de KB por macro), e é justamente por
    /// isso que editar precisa carregar a macro completa: salvar um objeto vindo da listagem
    /// gravaria imagem = NULL por cima da imagem existente. Foi o bug B1.
    /// </summary>
    [Fact]
    public async Task Listagem_NaoTrazImagem_MasGetByIdTraz()
    {
        using var banco = new BancoDeTeste();
        var id = await banco.Macros.InsertAsync(Nova("com-imagem", imagem: "data:image/png;base64,AAAA"));

        var daLista = (await banco.Macros.GetAllAsync()).Single();
        var completa = await banco.Macros.GetByIdAsync(id);

        Assert.Null(daLista.ImagemBase64);
        Assert.Equal("data:image/png;base64,AAAA", completa!.ImagemBase64);
    }

    // ── Datas: nenhum deslocamento de fuso na ida e na volta ─────────────────────

    /// <summary>
    /// datetime('now') do SQLite devolve UTC e o banco guarda hora local. Se o
    /// DataSqliteHandler deixar de fixar o formato, a leitura passa a converter e todo horário
    /// grava certo e lê 3 horas deslocado.
    /// </summary>
    [Fact]
    public async Task DataDeCriacao_VoltaComOMesmoHorarioDeParede()
    {
        using var banco = new BancoDeTeste();

        var antes = DateTime.Now;
        var id = await banco.Macros.InsertAsync(Nova("agora"));
        var depois = DateTime.Now;

        var lida = await banco.Macros.GetByIdAsync(id);

        Assert.InRange(lida!.DataCriacao, antes.AddSeconds(-1), depois.AddSeconds(1));

        // Unspecified e não Utc/Local: qualquer comparação com DateTime.Now precisa ser direta,
        // sem o runtime achando que deve converter alguma coisa.
        Assert.Equal(DateTimeKind.Unspecified, lida.DataCriacao.Kind);
    }

    [Fact]
    public async Task DataGravadaNoPassado_NaoEhReinterpretada()
    {
        using var banco = new BancoDeTeste();

        banco.Executar("""
            INSERT INTO macros (atalho, titulo, conteudo, ativo, favorito, data_criacao)
            VALUES ('antiga', 'Antiga', 'texto', 1, 0, '2026-06-23 23:37:26')
            """);

        var macro = (await banco.Macros.GetAllAsync()).Single();
        Assert.Equal(new DateTime(2026, 6, 23, 23, 37, 26), macro.DataCriacao);
    }

    // ── Ordenação e busca ────────────────────────────────────────────────────────

    /// <summary>
    /// No SQLite o NULL ordena PRIMEIRO; no Postgres, por último. Sem o "categoria IS NULL"
    /// explícito no ORDER BY, as macros sem categoria pulariam do fim para o topo da lista só
    /// por causa da troca de banco.
    /// </summary>
    [Fact]
    public async Task Listagem_ColocaMacrosSemCategoriaNoFim()
    {
        using var banco = new BancoDeTeste();

        await banco.Macros.InsertAsync(Nova("sem-cat"));
        await banco.Macros.InsertAsync(Nova("com-cat", categoria: "Jurídico"));

        var titulos = (await banco.Macros.GetAllAsync()).Select(m => m.Atalho).ToList();
        Assert.Equal(["com-cat", "sem-cat"], titulos);
    }

    /// <summary>
    /// O caso que motivou filtrar em C# em vez de no SQL: o LIKE do SQLite só ignora maiúsculas
    /// para ASCII, então "AÇÃO" não acharia "ação".
    /// </summary>
    [Fact]
    public async Task Busca_IgnoraCaixaEmPalavraAcentuada()
    {
        using var banco = new BancoDeTeste();
        await banco.Macros.InsertAsync(Nova("plano", conteudo: "Solicito a especificação da ação corretiva."));

        Assert.Single(await banco.Macros.SearchAsync("AÇÃO"));
        Assert.Single(await banco.Macros.SearchAsync("ação"));
        Assert.Single(await banco.Macros.SearchAsync("ESPECIFICAÇÃO"));
    }

    /// <summary>
    /// Procurar SEM acento tem de achar o que foi escrito COM acento.
    ///
    /// Filtrar em C# resolveu metade do problema: <c>OrdinalIgnoreCase</c> dobra maiúscula e
    /// minúscula, inclusive das acentuadas, mas não equipara "a" a "á". Na prática, digitar
    /// "acao" no Ctrl+Espaço achava a NOTA "Ação" — que passa por <c>TextoNormalizado</c> — e
    /// não achava a MACRO "Ação", na mesma janela, com o mesmo termo. É a única forma de
    /// digitar rápido, e era a única que não funcionava.
    /// </summary>
    [Fact]
    public async Task Busca_AchaComAcentoProcurandoSemAcento()
    {
        using var banco = new BancoDeTeste();
        await banco.Macros.InsertAsync(Nova("ac", titulo: "Ação corretiva",
            conteudo: "Segue a especificação solicitada.", categoria: "Jurídico"));

        Assert.Single(await banco.Macros.SearchAsync("acao"));            // pelo título
        Assert.Single(await banco.Macros.SearchAsync("ACAO"));
        Assert.Single(await banco.Macros.SearchAsync("especificacao"));   // pelo conteúdo
        Assert.Single(await banco.Macros.SearchAsync("juridico"));        // pela categoria
    }

    /// <summary>O ranking continua valendo com o texto achatado: atalho exato vem antes.</summary>
    [Fact]
    public async Task Busca_SemAcento_MantemAOrdemDeRelevancia()
    {
        using var banco = new BancoDeTeste();
        await banco.Macros.InsertAsync(Nova("no-conteudo", conteudo: "trata da ação corretiva"));
        await banco.Macros.InsertAsync(Nova("acao"));

        var ordem = (await banco.Macros.SearchAsync("acao")).Select(m => m.Atalho).ToList();

        Assert.Equal(["acao", "no-conteudo"], ordem);
    }

    [Fact]
    public async Task Busca_RanqueiaAtalhoExatoAntesDeTituloEConteudo()
    {
        using var banco = new BancoDeTeste();

        await banco.Macros.InsertAsync(Nova("zzz-conteudo", conteudo: "menciona prazo no meio do texto"));
        await banco.Macros.InsertAsync(new Macro { Atalho = "aaa-titulo", Titulo = "Prazo de resposta", Conteudo = "x" });
        await banco.Macros.InsertAsync(Nova("prazo"));

        var ordem = (await banco.Macros.SearchAsync("prazo")).Select(m => m.Atalho).ToList();
        Assert.Equal(["prazo", "aaa-titulo", "zzz-conteudo"], ordem);
    }

    [Fact]
    public async Task Busca_IgnoraMacrosArquivadas()
    {
        using var banco = new BancoDeTeste();
        var id = await banco.Macros.InsertAsync(Nova("arquivada"));

        Assert.Single(await banco.Macros.SearchAsync("arquivada"));

        await banco.Macros.ToggleAtivoAsync(id, false);
        Assert.Empty(await banco.Macros.SearchAsync("arquivada"));
    }

    [Fact]
    public async Task GetByAtalho_IgnoraCaixaEIgnoraArquivadas()
    {
        using var banco = new BancoDeTeste();
        var id = await banco.Macros.InsertAsync(Nova("chamado-recebido"));

        Assert.NotNull(await banco.Macros.GetByAtalhoAsync("CHAMADO-RECEBIDO"));

        await banco.Macros.ToggleAtivoAsync(id, false);
        Assert.Null(await banco.Macros.GetByAtalhoAsync("chamado-recebido"));
    }

    // ── Escrita ──────────────────────────────────────────────────────────────────

    /// <summary>Uma coluna só. Antes eram duas viagens (ler a linha inteira, devolver a linha inteira), o que sobrescrevia com dados possivelmente velhos.</summary>
    [Fact]
    public async Task ToggleFavorito_NaoTocaNasOutrasColunas()
    {
        using var banco = new BancoDeTeste();
        var id = await banco.Macros.InsertAsync(Nova("fav", conteudo: "conteúdo original"));

        await banco.Macros.ToggleFavoritoAsync(id, true);

        var macro = await banco.Macros.GetByIdAsync(id);
        Assert.True(macro!.Favorito);
        Assert.Equal("conteúdo original", macro.Conteudo);
    }

    /// <summary>O lote inteiro numa transação: se o banco recusar UMA linha, nada é gravado.</summary>
    [Fact]
    public async Task LoteComDuplicata_NaoGravaNenhumaLinha()
    {
        using var banco = new BancoDeTeste();
        await banco.Macros.InsertAsync(Nova("ja-existe"));

        await Assert.ThrowsAsync<RegistroDuplicadoException>(() =>
            banco.Macros.BatchInsertAsync([Nova("nova-1"), Nova("ja-existe"), Nova("nova-2")]));

        Assert.Equal(1, banco.Escalar<int>("SELECT COUNT(*) FROM macros"));
    }

    [Fact]
    public async Task Categorias_VemDaColunaDesnormalizadaSemRepetir()
    {
        using var banco = new BancoDeTeste();

        await banco.Macros.InsertAsync(Nova("a", categoria: "Jurídico"));
        await banco.Macros.InsertAsync(Nova("b", categoria: "Jurídico"));
        await banco.Macros.InsertAsync(Nova("c", categoria: "Financeiro"));
        await banco.Macros.InsertAsync(Nova("d"));

        Assert.Equal(["Financeiro", "Jurídico"], (await banco.Macros.GetCategoriasAsync()).ToArray());
    }

    internal static Macro Nova(
        string atalho, string? conteudo = null, string? categoria = null,
        string? tecla = null, string? imagem = null, string? titulo = null) => new()
    {
        Atalho       = atalho,
        Titulo       = titulo ?? atalho,
        Conteudo     = conteudo ?? $"conteúdo de {atalho}",
        Categoria    = categoria,
        AtalhoTecla  = tecla,
        ImagemBase64 = imagem,
    };
}
