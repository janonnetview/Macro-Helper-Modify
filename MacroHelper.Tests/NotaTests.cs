using MacroHelper.Core.Entities;
using MacroHelper.Services;

namespace MacroHelper.Tests;

public class NotaTests
{
    /// <summary>
    /// O LIKE do SQLite só é insensível a maiúsculas para ASCII — "Ç" e "ç" são caracteres
    /// distintos para ele. É por isso que existe a coluna `busca`, já minúscula e sem acento.
    /// </summary>
    [Theory]
    [InlineData("acao")]
    [InlineData("AÇÃO")]
    [InlineData("Ação")]
    [InlineData("ACAO")]
    [InlineData("integracao")]
    [InlineData("INTEGRAÇÃO")]
    [InlineData("órgão")]
    [InlineData("orgao")]
    public async Task Busca_AchaAMesmaNotaComOuSemAcento(string termo)
    {
        using var banco = new BancoDeTeste();
        await banco.Notas.InsertAsync(new Nota
        {
            Titulo   = "Ação corretiva",
            Conteudo = "Revisar a INTEGRAÇÃO com o órgão público.",
        });
        await banco.Notas.InsertAsync(new Nota { Titulo = "Compras", Conteudo = "Café e leite" });

        var achadas = await banco.Notas.SearchAsync(termo);
        Assert.Equal("Ação corretiva", achadas.Single().Titulo);
    }

    /// <summary>Sem escapar, procurar "50%" listaria todas as notas — % é curinga do LIKE.</summary>
    [Fact]
    public async Task Busca_TrataPorcentoComoTextoLiteral()
    {
        using var banco = new BancoDeTeste();
        await banco.Notas.InsertAsync(new Nota { Titulo = "Desconto", Conteudo = "Aplicar 50% no contrato" });
        await banco.Notas.InsertAsync(new Nota { Titulo = "Outra", Conteudo = "nada a ver" });

        Assert.Single(await banco.Notas.SearchAsync("50%"));
        Assert.Single(await banco.Notas.SearchAsync("%"));
    }

    [Fact]
    public async Task Busca_TrataSublinhadoComoTextoLiteral()
    {
        using var banco = new BancoDeTeste();
        await banco.Notas.InsertAsync(new Nota { Titulo = "Campo", Conteudo = "usar nome_completo no formulário" });
        await banco.Notas.InsertAsync(new Nota { Titulo = "Outra", Conteudo = "nomeXcompleto" });

        // Sem ESCAPE, "_" casaria com qualquer caractere e as duas notas voltariam.
        Assert.Single(await banco.Notas.SearchAsync("nome_completo"));
    }

    /// <summary>
    /// A coluna `busca` é calculada dentro do MESMO objeto de parâmetros do UPDATE. É a defesa
    /// contra o clássico "a busca não acha a nota que eu acabei de editar" — que é exatamente o
    /// que aconteceria se a sincronia fosse um passo separado.
    /// </summary>
    [Fact]
    public async Task Editar_AtualizaAColunaDeBuscaJunto()
    {
        using var banco = new BancoDeTeste();
        var id = await banco.Notas.InsertAsync(new Nota { Titulo = "Rascunho", Conteudo = "texto antigo" });

        await banco.Notas.UpdateAsync(new Nota { Id = id, Titulo = "Rascunho", Conteudo = "prorrogação do prazo" });

        Assert.Single(await banco.Notas.SearchAsync("prorrogacao"));
        Assert.Empty(await banco.Notas.SearchAsync("antigo"));
    }

    [Fact]
    public async Task Listagem_ColocaFixadasNoTopoEDepoisAMaisRecente()
    {
        using var banco = new BancoDeTeste();

        var primeira = await banco.Notas.InsertAsync(new Nota { Titulo = "Primeira", Conteudo = "a" });
        await banco.Notas.InsertAsync(new Nota { Titulo = "Segunda", Conteudo = "b" });
        var terceira = await banco.Notas.InsertAsync(new Nota { Titulo = "Terceira", Conteudo = "c" });

        // Editar a primeira a joga para o topo das não-fixadas...
        await banco.Notas.UpdateAsync(new Nota { Id = primeira, Titulo = "Primeira", Conteudo = "a editado" });
        // ...mas uma fixada vem antes de qualquer uma delas.
        await banco.Notas.ToggleFixadaAsync(terceira, true);

        var ordem = (await banco.Notas.GetAllAsync()).Select(n => n.Titulo).ToList();
        Assert.Equal("Terceira", ordem[0]);
        Assert.Equal("Primeira", ordem[1]);
    }

    [Fact]
    public async Task BuscaVazia_DevolveTudo()
    {
        using var banco = new BancoDeTeste();
        await banco.Notas.InsertAsync(new Nota { Titulo = "Uma", Conteudo = "a" });
        await banco.Notas.InsertAsync(new Nota { Titulo = "Outra", Conteudo = "b" });

        Assert.Equal(2, (await banco.Notas.SearchAsync("   ")).Count());
    }

    // ── NotaService ──────────────────────────────────────────────────────────────

    /// <summary>Numa tela de anotações, obrigar a preencher dois campos para guardar uma linha de texto atrapalha mais do que ajuda.</summary>
    [Fact]
    public async Task Salvar_SemTitulo_UsaAPrimeiraLinhaDoConteudo()
    {
        using var banco = new BancoDeTeste();
        var servico = new NotaService(banco.Notas);

        var (ok, _, nota) = await servico.SalvarAsync(new Nota
        {
            Conteudo = "\n  Senha do portal fica no cofre  \nsegunda linha",
        });

        Assert.True(ok);
        Assert.Equal("Senha do portal fica no cofre", nota!.Titulo);
    }

    [Fact]
    public async Task Salvar_TituloDeduzido_EhTruncadoComReticencia()
    {
        using var banco = new BancoDeTeste();
        var servico = new NotaService(banco.Notas);

        var (_, _, nota) = await servico.SalvarAsync(new Nota { Conteudo = new string('x', 100) });

        Assert.Equal(61, nota!.Titulo.Length);
        Assert.EndsWith("…", nota.Titulo);
    }

    [Fact]
    public async Task Salvar_TudoVazio_EhRecusado()
    {
        using var banco = new BancoDeTeste();
        var servico = new NotaService(banco.Notas);

        var (ok, msg, nota) = await servico.SalvarAsync(new Nota());

        Assert.False(ok);
        Assert.Null(nota);
        Assert.Contains("título", msg, StringComparison.OrdinalIgnoreCase);
    }
}
