using MacroHelper.UI.Controls;

namespace MacroHelper.Tests;

/// <summary>
/// A linha de estado do rodapé das janelas sem ViewModel (a busca rápida e as três soltas).
///
/// O que se testa aqui é a única coisa que importa nela: confirmação, ressalva e recusa não
/// podem terminar com a mesma cara. Antes dela, "não gravei" saía no mesmo cinza de "salva às
/// 14:32", e a janela fechava com a pessoa achando que estava tudo certo.
/// </summary>
public class LinhaDeEstadoTests
{
    [Fact]
    public void CadaChamada_DeixaOTomCorrespondente()
    {
        TelaDeTeste.Executar(() =>
        {
            var linha = new LinhaDeEstado();

            linha.Mostrar("Salva às 14:32");
            Assert.Equal(TomDoEstado.Normal, linha.Tom);
            Assert.Equal("Salva às 14:32", linha.Texto);

            linha.Avisar("Lembrete depois do prazo. Não gravei.");
            Assert.Equal(TomDoEstado.Aviso, linha.Tom);

            linha.Falhar("O título é obrigatório.");
            Assert.Equal(TomDoEstado.Erro, linha.Tom);
            Assert.Equal("O título é obrigatório.", linha.Texto);
        });
    }

    /// <summary>
    /// Voltar ao normal tem de apagar o tom anterior: uma linha que fica vermelha para sempre
    /// depois do primeiro erro para de significar alguma coisa.
    /// </summary>
    [Fact]
    public void DepoisDeUmErro_OProximoEstadoNormalVoltaAoCinza()
    {
        TelaDeTeste.Executar(() =>
        {
            var linha = new LinhaDeEstado();

            linha.Falhar("O título é obrigatório.");
            linha.Limpar();

            Assert.Equal(TomDoEstado.Normal, linha.Tom);
            Assert.Equal(string.Empty, linha.Texto);
        });
    }
}
