using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Windows;

namespace MacroHelper.Tests;

/// <summary>
/// A thread STA única onde roda todo teste que monta uma tela de verdade.
///
/// Não é luxo de infraestrutura: <see cref="Application"/> só pode ser instanciada UMA vez por
/// AppDomain, e os pincéis do dicionário de recursos pertencem à thread que os criou. Cada
/// teste abrindo a própria thread com o próprio Application funciona enquanto existir um teste
/// só; o segundo estoura com "Cannot create more than one System.Windows.Application", e a
/// mensagem não diz nada sobre qual par de testes brigou.
/// </summary>
internal static class TelaDeTeste
{
    private static readonly BlockingCollection<Action> _fila = new();

    static TelaDeTeste()
    {
        var pronta = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            // O Application existe para que os {StaticResource} das telas achem os recursos do
            // App.xaml — sem eles nenhuma tela monta.
            _ = new Application { Resources = Xaml.RecursosDoApp() };
            pronta.Set();

            foreach (var acao in _fila.GetConsumingEnumerable()) acao();
        })
        {
            // Background: ninguém fecha a fila no fim da suíte, e uma thread de primeiro plano
            // viva seguraria o processo de teste aberto para sempre.
            IsBackground = true,
            Name         = "Telas WPF (STA)",
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        pronta.Wait();
    }

    /// <summary>Roda a ação na thread das telas e repropaga a exceção dela com a pilha intacta.</summary>
    public static void Executar(Action acao)
    {
        ExceptionDispatchInfo? falha = null;
        var terminou = new ManualResetEventSlim();

        _fila.Add(() =>
        {
            try { acao(); }
            catch (Exception ex) { falha = ExceptionDispatchInfo.Capture(ex); }
            finally { terminou.Set(); }
        });

        terminou.Wait();
        falha?.Throw();
    }
}
