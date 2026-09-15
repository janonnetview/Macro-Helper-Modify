using MacroHelper.Services;

namespace MacroHelper.Tests;

/// <summary>
/// A paleta de cores de destaque e, principalmente, a tradução do que está gravado no disco.
///
/// Este é o ponto onde uma instalação antiga encontra a paleta nova: até aqui o que ficava
/// salvo era um hexadecimal de uma lista de quinze cores fixas, e hoje é o NOME de uma das
/// doze, com uma versão para cada tema. Ler o formato velho não é retrocompatibilidade
/// decorativa: sem ela toda pessoa que já usa o app perderia a escolha na primeira abertura.
/// </summary>
public class PaletaDeDestaqueTests
{
    [Fact]
    public void ASalvaNoDisco_EhONomeDaCor()
    {
        var cor = PaletaDeDestaque.Resolver("Turquesa");

        Assert.Equal("Turquesa", cor.Nome);
        Assert.Equal("#2D958A", cor.Claro);
        Assert.Equal("#4BC1B5", cor.Escuro);
    }

    [Fact]
    public void CadaCorTemUmaVersaoParaCadaTema()
    {
        foreach (var cor in PaletaDeDestaque.Cores)
        {
            Assert.NotEqual(cor.Claro, cor.Escuro);
            Assert.Equal(cor.Claro,  cor.ParaTema(escuro: false));
            Assert.Equal(cor.Escuro, cor.ParaTema(escuro: true));
        }
    }

    [Fact]
    public void ANomeacaoNaoTemRepetidos()
    {
        var nomes = PaletaDeDestaque.Cores.Select(c => c.Nome).ToList();
        Assert.Equal(nomes.Count, nomes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData("#7FA33D")]
    [InlineData("#9BC653")]
    [InlineData("#9bc653")]
    public void OHexadecimalDeUmaDasDuasVersoes_AchaACorInteira(string hex)
    {
        Assert.Equal(PaletaDeDestaque.NomePadrao, PaletaDeDestaque.Resolver(hex).Nome);
    }

    /// <summary>
    /// Os hexadecimais da paleta antiga. Nenhum deles existe mais, e cada um cai na cor NOVA
    /// mais parecida: quem tinha escolhido o roxo continua com um roxo. Cair no verde da casa
    /// se leria como "o app trocou minha cor sozinho".
    /// </summary>
    [Theory]
    [InlineData("#A7C957", "Verde sálvia")]
    [InlineData("#6C5CE7", "Roxo")]
    [InlineData("#00B894", "Turquesa")]
    [InlineData("#E17055", "Coral")]
    [InlineData("#0984E3", "Azul")]
    [InlineData("#E84393", "Rosa")]
    [InlineData("#FDCB6E", "Âmbar")]
    [InlineData("#D63031", "Vermelho")]
    // O cinza. É o que obrigou a conta a ser em HSV: em RGB ele caía no verde-azulado.
    [InlineData("#636E72", "Azul acinzentado")]
    [InlineData("#E29578", "Coral")]
    [InlineData("#D88C9A", "Rosa")]
    [InlineData("#9C89B8", "Lilás")]
    [InlineData("#6D9DC5", "Azul")]
    [InlineData("#56A3A6", "Verde-azulado")]
    [InlineData("#E0A458", "Âmbar")]
    public void UmHexadecimalDaPaletaAntiga_CaiNaCorNovaMaisParecida(string antigo, string esperado)
    {
        Assert.Equal(esperado, PaletaDeDestaque.Resolver(antigo).Nome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nem cor nem nome")]
    public void SemNadaLegivelGravado_FicaOVerdeDaCasa(string? salvo)
    {
        Assert.Equal(PaletaDeDestaque.NomePadrao, PaletaDeDestaque.Resolver(salvo).Nome);
    }
}
