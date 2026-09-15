using MacroHelper.Services;

namespace MacroHelper.Tests;

/// <summary>
/// O <c>{|}</c>: quanto o cursor precisa voltar depois que a macro entra.
///
/// O número é o que vira setas para a esquerda no app de destino, então errar por um aqui é o
/// cursor parando no lugar errado em toda macro com lacuna.
/// </summary>
public class MarcadorDeCursorTests
{
    [Fact]
    public void SemMarcador_TextoIntactoEDeslocamentoZero()
    {
        var (texto, deslocamento) = MarcadorDeCursor.Extrair("Bom dia, tudo bem?");

        Assert.Equal("Bom dia, tudo bem?", texto);
        Assert.Equal(0, deslocamento);
        Assert.False(MarcadorDeCursor.Tem("Bom dia"));
    }

    [Fact]
    public void Marcador_SaiDoTextoEViraDeslocamento()
    {
        var (texto, deslocamento) = MarcadorDeCursor.Extrair("Prezado {|}, segue em anexo.");

        Assert.Equal("Prezado , segue em anexo.", texto);
        Assert.Equal(", segue em anexo.".Length, deslocamento);
    }

    /// <summary>Marcador no fim é o mesmo que não ter marcador: o cursor já para ali.</summary>
    [Fact]
    public void MarcadorNoFim_NaoMoveOCursor()
    {
        var (texto, deslocamento) = MarcadorDeCursor.Extrair("Atenciosamente,\n{|}");

        Assert.Equal("Atenciosamente,\n", texto);
        Assert.Equal(0, deslocamento);
    }

    /// <summary><c>{cursor}</c> é apelido do <c>{|}</c>, para quem prefere escrever por extenso.</summary>
    [Fact]
    public void Apelido_PorExtenso_ValeOMesmo()
    {
        var (texto, deslocamento) = MarcadorDeCursor.Extrair("Olá {CURSOR}!");

        Assert.Equal("Olá !", texto);
        Assert.Equal(1, deslocamento);
    }

    /// <summary>
    /// Um cursor só existe. O primeiro marcador manda; os outros são apagados, e não podem
    /// sobrar como texto literal no que vai para o outro programa.
    /// </summary>
    [Fact]
    public void VariosMarcadores_OPrimeiroPosicionaEOsOutrosSomem()
    {
        var (texto, deslocamento) = MarcadorDeCursor.Extrair("a{|}b{|}c");

        Assert.Equal("abc", texto);
        Assert.Equal(2, deslocamento);
    }

    /// <summary>
    /// O <c>\r</c> do CRLF não conta.
    ///
    /// No app de destino o par inteiro é UMA quebra de linha, e uma seta para a esquerda a
    /// atravessa de uma vez. Contar os dois faria o cursor parar um caractere adiante por
    /// linha do texto — o erro cresce com o tamanho da macro.
    /// </summary>
    [Fact]
    public void CrLf_ContaComoUmaPosicaoSo()
    {
        var (texto, deslocamento) = MarcadorDeCursor.Extrair("Olá {|}\r\nabc");

        Assert.Equal("Olá \r\nabc", texto);
        Assert.Equal(4, deslocamento);   // \n + a + b + c — o \r não conta
    }

    /// <summary>
    /// Nota e item copiado entram no outro programa COMO ESTÃO. É a mesma regra que faz um
    /// <c>{campo}</c> dentro de uma nota ser chave e colchete, e não variável a preencher —
    /// e é por isso que a interpretação do marcador é opcional na hora de inserir.
    /// </summary>
    [Fact]
    public void Extrair_NaoEhOCaminhoDeNotaEDeTextoCopiado()
    {
        // O que o caminho de nota/clipboard faz é NADA: nem extrai, nem remove.
        const string copiado = "if (x) { return {cursor}; }";

        var (texto, deslocamento) = MarcadorDeCursor.Extrair(copiado);
        Assert.NotEqual(copiado, texto);          // no caminho de macro, o marcador sairia
        Assert.NotEqual(0, deslocamento);

        // Por isso TextInsertionService.InserirTextoAsync recebe interpretarMarcador: false
        // para nota e para item copiado — ver BuscadorRapidoWindow.InserirTexto.
    }

    [Fact]
    public void Remover_TiraTodosSemCalcularNada()
    {
        Assert.Equal("abc", MarcadorDeCursor.Remover("a{|}b{cursor}c"));
        Assert.Equal(string.Empty, MarcadorDeCursor.Remover(null));
    }

    /// <summary>
    /// O marcador NÃO é uma variável. Sem esta garantia, uma macro com <c>{cursor}</c> abriria
    /// o diálogo de preenchimento pedindo "valor para cursor" — e trocaria o marcador pelo que
    /// fosse digitado ali.
    /// </summary>
    [Fact]
    public void Marcador_NaoEhTratadoComoVariavel()
    {
        Assert.False(VariavelService.TemVariaveis("Prezado {|}, segue."));
        Assert.False(VariavelService.TemVariaveis("Prezado {cursor}, segue."));
        Assert.Empty(VariavelService.ExtrairVariaveis("{cursor}", "Raul"));

        // Mas uma variável de verdade no meio continua sendo detectada.
        Assert.True(VariavelService.TemVariaveis("Prezado {nome}, {|}"));
        Assert.Single(VariavelService.ExtrairVariaveis("Prezado {nome}, {|}", "Raul"));
    }
}
