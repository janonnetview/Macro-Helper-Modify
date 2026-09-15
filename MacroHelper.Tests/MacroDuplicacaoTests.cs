using MacroHelper.Core.Entities;
using MacroHelper.Services;

namespace MacroHelper.Tests;

/// <summary>
/// Duplicar uma macro. O que interessa aqui é o que NÃO é copiado: os três campos que, copiados
/// junto, fariam a gravação da cópia ser recusada pelo banco ou produziriam uma segunda macro
/// disputando o mesmo atalho de teclado.
/// </summary>
public class MacroDuplicacaoTests
{
    private static MacroService Servico(BancoDeTeste banco) => new(banco.Macros);

    private static Macro Base() => new()
    {
        Atalho    = "boas-vindas",
        Titulo    = "Boas-vindas",
        Conteudo  = "Olá {nome}, seja bem-vindo!",
        Categoria = "Atendimento",
    };

    [Fact]
    public async Task Duplicar_CopiaOConteudoESufixaOAtalho()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        var original = Base();
        await svc.SalvarAsync(original);

        var (ok, _, copia) = await svc.DuplicarAsync(original.Id);

        Assert.True(ok);
        Assert.Equal("boas-vindas-copia", copia!.Atalho);
        Assert.Equal("Boas-vindas (cópia)", copia.Titulo);
        Assert.Equal(original.Conteudo, copia.Conteudo);
        Assert.Equal("Atendimento", copia.Categoria);
        Assert.NotEqual(original.Id, copia.Id);
    }

    /// <summary>Duplicar a cópia não colide: o sufixo ganha número até achar um atalho livre.</summary>
    [Fact]
    public async Task Duplicar_DuasVezes_NumeraOSufixo()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        var original = Base();
        await svc.SalvarAsync(original);

        await svc.DuplicarAsync(original.Id);
        var (ok, _, segunda) = await svc.DuplicarAsync(original.Id);

        Assert.True(ok);
        Assert.Equal("boas-vindas-copia-2", segunda!.Atalho);
        Assert.Equal(3, (await svc.ObterTodosAsync()).Count());
    }

    /// <summary>
    /// O atalho de teclado NÃO vem junto. Ctrl+Alt+3 tem de resolver para exatamente uma macro,
    /// e o índice único parcial do banco recusaria a gravação se a cópia o trouxesse.
    /// </summary>
    [Fact]
    public async Task Duplicar_NaoCopiaOAtalhoDeTeclado()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        var original = Base();
        original.AtalhoTecla = "3";
        await svc.SalvarAsync(original);

        var (ok, _, copia) = await svc.DuplicarAsync(original.Id);

        Assert.True(ok);
        Assert.Null(copia!.AtalhoTecla);

        // Ctrl+Alt+3 continua resolvendo para a original, e para uma só.
        Assert.Equal(original.Id, (await svc.ObterPorAtalhoTeclaAsync("3"))!.Id);
    }

    /// <summary>
    /// Favoritar é uma decisão sobre a macro que se usa, e a cópia ainda vai ser editada —
    /// nasce fora dos favoritos para não empurrar a original para baixo na busca rápida.
    /// </summary>
    [Fact]
    public async Task Duplicar_NaoCopiaOFavorito()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        var original = Base();
        original.Favorito = true;
        await svc.SalvarAsync(original);

        var (_, _, copia) = await svc.DuplicarAsync(original.Id);

        Assert.False(copia!.Favorito);
    }

    /// <summary>
    /// A imagem vem junto — e é por isso que a duplicação lê a macro pelo id, e não recebe o
    /// objeto da lista: as listagens não trazem <c>imagem_base64</c>, e duplicar a partir de
    /// uma delas produziria uma cópia sem imagem, em silêncio.
    /// </summary>
    [Fact]
    public async Task Duplicar_TrazAImagemQueAsListagensNaoCarregam()
    {
        using var banco = new BancoDeTeste();
        var svc = Servico(banco);

        var original = Base();
        original.ImagemBase64 = "iVBORw0KGgo=";
        await svc.SalvarAsync(original);

        var daListagem = (await svc.ObterTodosAsync()).Single();
        Assert.Null(daListagem.ImagemBase64);   // a listagem realmente não traz

        var (_, _, copia) = await svc.DuplicarAsync(original.Id);
        Assert.Equal("iVBORw0KGgo=", copia!.ImagemBase64);
    }

    [Fact]
    public async Task Duplicar_MacroInexistente_FalhaSemEstourar()
    {
        using var banco = new BancoDeTeste();

        var (ok, msg, copia) = await Servico(banco).DuplicarAsync(9999);

        Assert.False(ok);
        Assert.Null(copia);
        Assert.Contains("não encontrada", msg);
    }
}
