using MacroHelper.Core;

namespace MacroHelper.Tests;

/// <summary>
/// A rede de segurança contra <c>InvariantGlobalization=true</c>.
///
/// Nesse modo — que é o que se liga para encolher o publish e dispensar o ICU — o
/// <c>String.Normalize</c> devolve a string INTACTA, sem lançar nada. A busca por acento
/// simplesmente pararia de funcionar, em silêncio, e o sintoma seria "não acha a nota que eu
/// acabei de escrever". Estes testes travam o resultado independentemente do modo do runtime.
/// </summary>
public class TextoNormalizadoTests
{
    [Theory]
    [InlineData("Ação", "acao")]
    [InlineData("AÇÃO", "acao")]
    [InlineData("integração", "integracao")]
    [InlineData("Açúcar", "acucar")]
    [InlineData("órgão", "orgao")]
    [InlineData("Ética", "etica")]
    [InlineData("Coração Português", "coracao portugues")]
    [InlineData("Índice", "indice")]
    [InlineData("Ônibus", "onibus")]
    [InlineData("Übung", "ubung")]
    public void Para_TiraAcentoEBaixaACaixa(string entrada, string esperado)
        => Assert.Equal(esperado, TextoNormalizado.Para(entrada));

    [Fact]
    public void Para_NaoMexeEmTextoQueJaEstaSimples()
        => Assert.Equal("chamado recebido 123", TextoNormalizado.Para("Chamado Recebido 123"));

    [Fact]
    public void Para_TextoVazioOuNulo_VoltaVazio()
    {
        Assert.Equal(string.Empty, TextoNormalizado.Para(null));
        Assert.Equal(string.Empty, TextoNormalizado.Para(string.Empty));
    }

    /// <summary>Duas grafias da mesma palavra têm que colidir — é o que faz "acao" achar "Ação".</summary>
    [Fact]
    public void Para_ComEComAcentoProduzemAMesmaChave()
        => Assert.Equal(TextoNormalizado.Para("AÇÃO"), TextoNormalizado.Para("acao"));
}
