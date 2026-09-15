using MacroHelper.UI.Helpers;
using System.Runtime.ExceptionServices;
using System.Windows.Controls;

namespace MacroHelper.Tests;

/// <summary>
/// A máscara dos campos de data e hora do formulário de tarefas. O que se testa aqui é a conta
/// de texto — o que vira "24/08/2026" e onde o cursor para —, não o TextBox: os eventos do WPF
/// pedem uma janela, e o defeito que se quer evitar mora na montagem do texto.
/// </summary>
public class MascaraTextoTests
{
    [Theory]
    [InlineData("24082026", "24/08/2026")]  // como se digita: só os números
    [InlineData("24/08/2026", "24/08/2026")] // já formatado (o quê o botão "Hoje" escreve) fica igual
    [InlineData("2408", "24/08")]            // sem ano — o formulário aceita e completa
    [InlineData("2", "2")]
    [InlineData("240820269", "24/08/2026")]  // dígito sobrando não entra
    [InlineData("24abc08", "24/08")]         // colar texto sujo aproveita só os números
    [InlineData("", "")]
    public void Data_GanhaAsBarrasSozinha(string digitado, string esperado) =>
        Assert.Equal(esperado, MascaraTexto.Formatar(digitado, MascaraTexto.Formato.Data));

    [Theory]
    [InlineData("1244", "12:44")]
    [InlineData("12:44", "12:44")]
    [InlineData("12", "12")]
    [InlineData("12445", "12:44")]
    public void Hora_GanhaOsDoisPontosSozinha(string digitado, string esperado) =>
        Assert.Equal(esperado, MascaraTexto.Formatar(digitado, MascaraTexto.Formato.Hora));

    /// <summary>
    /// "2424" era exatamente o que dava para digitar antes num campo de data: quatro números
    /// soltos, sem nada indicando onde acabava o dia. Agora sai "24/24" — ainda uma data que não
    /// existe, mas visível como tal antes de salvar.
    /// </summary>
    [Fact]
    public void DigitarSeguido_MostraAFormaDaDataDesdeOSegundoNumero()
    {
        var passos = new List<string>();
        var texto = string.Empty;

        foreach (var tecla in "24082026")
        {
            texto = MascaraTexto.Formatar(texto + tecla, MascaraTexto.Formato.Data);
            passos.Add(texto);
        }

        Assert.Equal(
            ["2", "24", "24/0", "24/08", "24/08/2", "24/08/20", "24/08/202", "24/08/2026"],
            passos);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 2)]   // depois de "24", antes da barra
    [InlineData(3, 4)]   // o terceiro dígito está depois da barra
    [InlineData(9, 10)]  // pedido maior que o texto para no fim
    public void OCursor_VoltaParaDepoisDoMesmoDigito(int digitos, int posicao) =>
        Assert.Equal(posicao, MascaraTexto.PosicaoDepoisDe("24/08/2026", digitos));

    [Fact]
    public void ContarDigitos_IgnoraOsSeparadoresAntesDoCursor()
    {
        Assert.Equal(3, MascaraTexto.ContarDigitos("24/08/2026", 4));
        Assert.Equal(0, MascaraTexto.ContarDigitos("24/08/2026", 0));
        Assert.Equal(8, MascaraTexto.ContarDigitos("24/08/2026", 99));
    }

    /// <summary>Sem formato, o campo é um TextBox comum — a máscara não pode mexer no texto.</summary>
    [Fact]
    public void SemFormato_NaoMexeNoTexto() =>
        Assert.Equal("qualquer coisa", MascaraTexto.Formatar("qualquer coisa", MascaraTexto.Formato.Nenhum));

    // ── O campo de verdade ───────────────────────────────────────────────────

    /// <summary>
    /// Os testes acima conferem a conta; estes conferem que ela está ligada no TextBox. Sem
    /// isto, trocar o nome da propriedade anexada deixaria os campos sem máscara nenhuma e
    /// todos os outros testes continuariam verdes.
    /// </summary>
    [Fact]
    public void OCampoComMascaraDeData_SeFormataSozinho() => EmSta(() =>
    {
        var campo = new TextBox();
        MascaraTexto.SetFormato(campo, MascaraTexto.Formato.Data);

        campo.Text = "24082026";

        Assert.Equal("24/08/2026", campo.Text);
    });

    [Fact]
    public void OCampoComMascaraDeHora_SeFormataSozinho() => EmSta(() =>
    {
        var campo = new TextBox();
        MascaraTexto.SetFormato(campo, MascaraTexto.Formato.Hora);

        campo.Text = "1244";

        Assert.Equal("12:44", campo.Text);
    });

    [Fact]
    public void OCampoSemMascara_NaoEMexido() => EmSta(() =>
    {
        var campo = new TextBox { Text = "24082026" };
        Assert.Equal("24082026", campo.Text);
    });

    /// <summary>Todo controle do WPF exige o apartamento STA, que a thread do xUnit não é.</summary>
    private static void EmSta(Action acao)
    {
        ExceptionDispatchInfo? falha = null;

        var thread = new Thread(() =>
        {
            try { acao(); }
            catch (Exception ex) { falha = ExceptionDispatchInfo.Capture(ex); }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        falha?.Throw();
    }
}
