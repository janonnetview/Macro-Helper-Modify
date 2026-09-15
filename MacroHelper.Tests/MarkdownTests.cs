using MacroHelper.Core.Entities;
using MacroHelper.Core.Texto;

namespace MacroHelper.Tests;

/// <summary>
/// A leitura do Markdown das notas.
///
/// O que estes testes guardam não é a especificação do Markdown: é o subconjunto que a nota de
/// reunião usa, e principalmente as duas decisões que se afastam dela de propósito (cada linha
/// é um bloco; a formatação não aninha). Sem isso escrito aqui, a primeira pessoa a mexer no
/// leitor vai "consertar" as duas.
/// </summary>
public class MarkdownTests
{
    [Fact]
    public void CadaLinhaEhUmBloco_MesmoSemLinhaEmBrancoEntreElas()
    {
        // No Markdown de verdade estas duas viram um parágrafo só, e a quebra digitada some.
        // Numa caixa de anotação isso se lê como defeito.
        var blocos = Markdown.Analisar("primeira linha\nsegunda linha");

        Assert.Equal(2, blocos.Count);
        Assert.All(blocos, b => Assert.Equal(TipoDeBloco.Paragrafo, b.Tipo));
    }

    [Theory]
    [InlineData("# Título",     TipoDeBloco.Titulo1)]
    [InlineData("## Título",    TipoDeBloco.Titulo2)]
    [InlineData("### Título",   TipoDeBloco.Titulo3)]
    [InlineData("- item",       TipoDeBloco.Item)]
    [InlineData("* item",       TipoDeBloco.Item)]
    [InlineData("+ item",       TipoDeBloco.Item)]
    [InlineData("1. item",      TipoDeBloco.ItemNumerado)]
    [InlineData("2) item",      TipoDeBloco.ItemNumerado)]
    [InlineData("> citação",    TipoDeBloco.Citacao)]
    [InlineData("- [ ] tarefa", TipoDeBloco.Tarefa)]
    [InlineData("- [x] tarefa", TipoDeBloco.Tarefa)]
    [InlineData("---",          TipoDeBloco.Divisor)]
    [InlineData("texto solto",  TipoDeBloco.Paragrafo)]
    public void CadaMarcaDeInicioDeLinha_ViraOBlocoCerto(string linha, TipoDeBloco esperado)
    {
        Assert.Equal(esperado, Markdown.Analisar(linha).Single().Tipo);
    }

    /// <summary>Sem o espaço depois da marca não é lista: "-1 grau" é um número negativo.</summary>
    [Theory]
    [InlineData("-1 grau")]
    [InlineData("#hashtag")]
    [InlineData("1.5 metros")]
    public void MarcaSemEspacoDepois_ContinuaSendoTexto(string linha)
    {
        Assert.Equal(TipoDeBloco.Paragrafo, Markdown.Analisar(linha).Single().Tipo);
    }

    [Fact]
    public void ACaixaDeMarcar_GuardaSeEstaMarcada()
    {
        var blocos = Markdown.Analisar("- [ ] ligar\n- [x] enviar");

        Assert.False(blocos[0].Marcada);
        Assert.True(blocos[1].Marcada);
        Assert.Equal("ligar",  blocos[0].Texto);
        Assert.Equal("enviar", blocos[1].Texto);
    }

    [Fact]
    public void OItemRecuado_GuardaONivel()
    {
        var blocos = Markdown.Analisar("- pai\n  - filho\n    - neto");

        Assert.Equal(0, blocos[0].Nivel);
        Assert.Equal(1, blocos[1].Nivel);
        Assert.Equal(2, blocos[2].Nivel);
    }

    [Fact]
    public void CadaBloco_SabeDeQualLinhaVeio()
    {
        // É o que faz a caixa clicada reescrever a linha certa.
        var blocos = Markdown.Analisar("# Reunião\n\n- [ ] ligar\n- [ ] enviar");

        Assert.Equal(0, blocos[0].Linha);
        Assert.Equal(2, blocos[2].Linha);
        Assert.Equal(3, blocos[3].Linha);
    }

    [Fact]
    public void AFormatacaoDeDentroDaLinha_SaiEmTrechos()
    {
        var trechos = Markdown.Trechos("um **negrito**, um *itálico* e um `código`");

        Assert.Contains(trechos, t => t.Texto == "negrito" && t.Negrito);
        Assert.Contains(trechos, t => t.Texto == "itálico" && t.Italico);
        Assert.Contains(trechos, t => t.Texto == "código"  && t.Codigo);
    }

    [Fact]
    public void OLink_GuardaOTextoEOEndereco()
    {
        var link = Markdown.Trechos("veja o [chamado](https://exemplo.com/1) aberto")
            .Single(t => t.Link != null);

        Assert.Equal("chamado", link.Texto);
        Assert.Equal("https://exemplo.com/1", link.Link);
    }

    /// <summary>
    /// A formatação não aninha, e isso é escolha, não esquecimento: aninhar pediria uma árvore
    /// de trechos, e a nota de reunião não paga esse preço.
    /// </summary>
    [Fact]
    public void FormatacaoAninhada_NaoEhInterpretada()
    {
        var trechos = Markdown.Trechos("**negrito com *itálico* dentro**");

        Assert.DoesNotContain(trechos, t => t.Negrito);
    }

    [Fact]
    public void OBlocoDeCodigo_VaiDaCercaAteAOutra()
    {
        var blocos = Markdown.Analisar("antes\n```\nSELECT 1\nSELECT 2\n```\ndepois");

        var codigo = blocos.Single(b => b.Tipo == TipoDeBloco.Codigo);
        Assert.Equal("SELECT 1\nSELECT 2", codigo.Texto);
        Assert.Equal(3, blocos.Count);
    }

    [Fact]
    public void CercaSemFechamento_ValeAteOFimDoTexto()
    {
        var blocos = Markdown.Analisar("```\nficou aberto");

        Assert.Equal("ficou aberto", blocos.Single().Texto);
    }

    // ── Texto simples ────────────────────────────────────────────────────────

    [Fact]
    public void ParaTextoSimples_TiraAMarcacaoEJuntaAsLinhas()
    {
        var texto = Markdown.ParaTextoSimples("## Reunião\n\n- falar com o **cliente**\n- [ ] enviar a ata");

        Assert.Equal("Reunião falar com o cliente enviar a ata", texto);
    }

    /// <summary>Feito e por fazer não podem virar a mesma frase no cartão da lista.</summary>
    [Fact]
    public void ParaTextoSimples_MarcaOQueJaFoiFeito()
    {
        Assert.Equal("✓ enviar a ata", Markdown.ParaTextoSimples("- [x] enviar a ata"));
        Assert.Equal("enviar a ata",   Markdown.ParaTextoSimples("- [ ] enviar a ata"));
    }

    [Fact]
    public void OResumoDaNota_NaoMostraAMarcacao()
    {
        var nota = new Nota { Conteudo = "# Reunião de quarta\n- [x] **ata** enviada" };

        Assert.Equal("Reunião de quarta ✓ ata enviada", nota.Resumo);
    }

    // ── Marcar e desmarcar ───────────────────────────────────────────────────

    [Fact]
    public void AlternarTarefa_MarcaEDesmarcaAMesmaLinha()
    {
        const string conteudo = "- [ ] ligar\n- [ ] enviar";

        var marcada = Markdown.AlternarTarefa(conteudo, 1);
        Assert.Equal("- [ ] ligar\n- [x] enviar", marcada);

        Assert.Equal(conteudo, Markdown.AlternarTarefa(marcada, 1));
    }

    [Fact]
    public void AlternarTarefa_PreservaORecuoDoItem()
    {
        Assert.Equal("  - [x] filho", Markdown.AlternarTarefa("  - [ ] filho", 0));
    }

    /// <summary>
    /// Reescrever um texto que veio com CRLF em LF puro mudaria toda linha da nota por causa de
    /// uma marca só — e o banco guardaria um conteúdo diferente do que a pessoa digitou.
    /// </summary>
    [Fact]
    public void AlternarTarefa_MantemAQuebraDeLinhaDoTexto()
    {
        var resultado = Markdown.AlternarTarefa("- [ ] um\r\n- [ ] dois", 0);

        Assert.Equal("- [x] um\r\n- [ ] dois", resultado);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    [InlineData(1)]
    public void AlternarTarefa_EmLinhaQueNaoEhCaixa_NaoMexeEmNada(int linha)
    {
        const string conteudo = "- [ ] ligar\ntexto solto";

        Assert.Equal(conteudo, Markdown.AlternarTarefa(conteudo, linha));
    }
}
