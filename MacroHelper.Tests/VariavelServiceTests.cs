using MacroHelper.Services;

namespace MacroHelper.Tests;

public class VariavelServiceTests
{
    [Fact]
    public void TemVariaveis_DetectaPlaceholder()
    {
        Assert.True(VariavelService.TemVariaveis("Olá {nome}!"));
        Assert.False(VariavelService.TemVariaveis("Olá mundo!"));
    }

    [Fact]
    public void ExtrairVariaveis_DataHoraEUsuario_SaoAutoPreenchidas()
    {
        var lista = VariavelService.ExtrairVariaveis("{data} {hora} {usuario} {protocolo}", "Aline");

        var usuario = lista.Single(v => v.Nome == "usuario");
        Assert.True(usuario.AutoPreencher);
        Assert.Equal("Aline", usuario.ValorPadrao);

        var protocolo = lista.Single(v => v.Nome == "protocolo");
        Assert.False(protocolo.AutoPreencher);
    }

    [Fact]
    public void ExtrairVariaveis_NaoDuplicaMesmaVariavel()
    {
        var lista = VariavelService.ExtrairVariaveis("{nome} disse oi para {nome}", "Aline");
        Assert.Single(lista);
    }

    /// <summary>
    /// A chave é o TOKEN inteiro, e não o nome cru: <c>{data}</c> e <c>{data+7}</c> têm o mesmo
    /// nome e precisam de valores diferentes, então por nome uma sobrescreveria a outra.
    /// </summary>
    [Fact]
    public void Substituir_TrocaTodasAsOcorrencias()
    {
        var valores = new Dictionary<string, string> { ["{nome}"] = "Carlos" };
        var resultado = VariavelService.Substituir("Olá {nome}, tudo bem {nome}?", valores);
        Assert.Equal("Olá Carlos, tudo bem Carlos?", resultado);
    }

    [Fact]
    public void Substituir_MantemPlaceholderQuandoValorNaoInformado()
    {
        var resultado = VariavelService.Substituir("Olá {nome}", new Dictionary<string, string>());
        Assert.Equal("Olá {nome}", resultado);
    }
}
