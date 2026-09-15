using MacroHelper.Services;

namespace MacroHelper.Tests;

/// <summary>
/// A gramática nova dos <c>{...}</c>: data relativa, formato próprio e lista de opções.
///
/// O instante entra por parâmetro em todos: afirmar que <c>{data+1}</c> devolve o dia seguinte
/// não pode depender de que dia é hoje, nem virar um teste que falha só na virada do mês.
/// </summary>
public class VariavelAvancadaTests
{
    /// <summary>Quinta-feira — para que sábado e domingo apareçam nos casos de dias úteis.</summary>
    private static readonly DateTime Quinta = new(2026, 8, 20, 14, 30, 0);

    private static string Valor(string template, DateTime? agora = null) =>
        VariavelService.ExtrairVariaveis(template, "Raul", agora ?? Quinta).Single().ValorPadrao!;

    // ── Data relativa ────────────────────────────────────────────────────────

    [Fact]
    public void Data_SemAjuste_EhHoje() => Assert.Equal("20/08/2026", Valor("{data}"));

    [Fact]
    public void Data_ComDiasParaFrenteEParaTras()
    {
        Assert.Equal("22/08/2026", Valor("{data+2}"));
        Assert.Equal("19/08/2026", Valor("{data-1}"));
    }

    /// <summary>
    /// "Retorno em 3 dias úteis" é resposta de chamado padrão, e contar isso na mão é onde se
    /// erra. De quinta, três dias úteis é a terça seguinte — o fim de semana não conta.
    /// </summary>
    [Fact]
    public void Data_DiasUteis_PulaSabadoEDomingo()
    {
        Assert.Equal("25/08/2026", Valor("{data+3u}"));
        Assert.Equal(DayOfWeek.Tuesday, new DateTime(2026, 8, 25).DayOfWeek);
    }

    [Fact]
    public void Data_Semanas() => Assert.Equal("03/09/2026", Valor("{data+2s}"));

    /// <summary>A unidade padrão segue a variável: hora soma horas, mês soma meses, data soma dias.</summary>
    [Fact]
    public void UnidadePadrao_DependeDaVariavel()
    {
        Assert.Equal("16:30", Valor("{hora+2}"));
        Assert.Equal("16:00", Valor("{hora+90m}"));
        Assert.Equal("setembro/2026", Valor("{mes+1}"));
        Assert.Equal("22/08/2026", Valor("{data+2}"));
    }

    // ── Formato ──────────────────────────────────────────────────────────────

    [Fact]
    public void Formato_Proprio()
    {
        Assert.Equal("20/08", Valor("{data:dd/MM}"));
        Assert.Equal("2026", Valor("{data:yyyy}"));
    }

    [Fact]
    public void Formato_CombinaComAjuste() => Assert.Equal("21/08", Valor("{data+1:dd/MM}"));

    /// <summary>
    /// Formato malformado não pode derrubar a inserção: <c>ToString</c> lança
    /// <c>FormatException</c>, e o estrago seria uma macro que simplesmente não entra.
    /// </summary>
    [Fact]
    public void Formato_Invalido_CaiNoPadraoEmVezDeEstourar()
        => Assert.Equal("20/08/2026", Valor("{data:Z}"));

    // ── Lista de opções ──────────────────────────────────────────────────────

    /// <summary>
    /// A barra é o que distingue lista de formato. Nas variáveis de data não existe lista —
    /// ali o que vem depois dos dois-pontos é sempre formato.
    /// </summary>
    [Fact]
    public void Lista_DeOpcoes_ViraCampoDeEscolha()
    {
        var v = VariavelService.ExtrairVariaveis("{status:Aberto|Em análise|Concluído}", "Raul", Quinta).Single();

        Assert.True(v.EhLista);
        Assert.False(v.AutoPreencher);
        Assert.Equal(["Aberto", "Em análise", "Concluído"], v.Opcoes);
        Assert.Equal("Aberto", v.ValorPadrao);
    }

    /// <summary>Argumento sem barra é o valor com que o campo já vem preenchido.</summary>
    [Fact]
    public void Argumento_SemBarra_EhValorPadrao()
    {
        var v = VariavelService.ExtrairVariaveis("{cliente:Fulano}", "Raul", Quinta).Single();

        Assert.False(v.EhLista);
        Assert.Equal("Fulano", v.ValorPadrao);
    }

    // ── Substituição por token ───────────────────────────────────────────────

    /// <summary>
    /// A razão de a chave ser o token inteiro: <c>{data}</c> e <c>{data+7}</c> têm o mesmo
    /// NOME e precisam de valores diferentes. Por nome, uma sobrescreveria a outra e as duas
    /// datas sairiam iguais no texto final.
    /// </summary>
    [Fact]
    public void MesmaVariavelComAjustesDiferentes_SaoDoisCampos()
    {
        var lista = VariavelService.ExtrairVariaveis("De {data} até {data+7}", "Raul", Quinta);

        Assert.Equal(2, lista.Count);
        Assert.All(lista, v => Assert.Equal("data", v.Nome));

        var texto = VariavelService.Substituir("De {data} até {data+7}",
            lista.ToDictionary(v => v.Token, v => v.ValorPadrao!));

        Assert.Equal("De 20/08/2026 até 27/08/2026", texto);
    }

    [Fact]
    public void TokenRepetido_ContaUmaVezSoESubstituiEmTodasAsOcorrencias()
    {
        var lista = VariavelService.ExtrairVariaveis("{data} e {data}", "Raul", Quinta);
        Assert.Single(lista);

        var texto = VariavelService.Substituir("{data} e {data}",
            lista.ToDictionary(v => v.Token, v => v.ValorPadrao!));
        Assert.Equal("20/08/2026 e 20/08/2026", texto);
    }

    /// <summary>
    /// <c>{macro:atalho}</c> é referência a outra macro, expandida antes de as variáveis serem
    /// olhadas. Sem a reserva, um <c>{macro:x}</c> que sobrasse viraria um campo pedindo
    /// "valor para macro" — ou, pior, uma lista de opções, por causa da barra.
    /// </summary>
    [Fact]
    public void ReferenciaAMacro_NaoEhVariavel()
    {
        Assert.False(VariavelService.TemVariaveis("{macro:assinatura}"));
        Assert.Empty(VariavelService.ExtrairVariaveis("{macro:assinatura}", "Raul", Quinta));
    }

    // ── Rótulos ──────────────────────────────────────────────────────────────

    /// <summary>
    /// O diálogo mostra o token no badge e o rótulo ao lado. Com <c>{data}</c> e <c>{data+3u}</c>
    /// na mesma macro, é o rótulo que diz qual campo é qual.
    /// </summary>
    [Fact]
    public void Rotulo_ExplicaOAjuste()
    {
        string Rotulo(string t) => VariavelService.ExtrairVariaveis(t, "Raul", Quinta).Single().Rotulo;

        Assert.Equal("Data de agora", Rotulo("{data}"));
        Assert.Equal("Data (daqui a 3 dias úteis)", Rotulo("{data+3u}"));
        Assert.Equal("Data (1 dia atrás)", Rotulo("{data-1}"));
        Assert.Equal("Hora (daqui a 2 horas)", Rotulo("{hora+2}"));
    }

    [Fact]
    public void Usuario_ContinuaVindoDoNomeConfigurado()
    {
        var v = VariavelService.ExtrairVariaveis("{usuario}", "Raul Janon", Quinta).Single();

        Assert.True(v.AutoPreencher);
        Assert.Equal("Raul Janon", v.ValorPadrao);
    }
}
