using CommunityToolkit.Mvvm.ComponentModel;

namespace MacroHelper.UI.ViewModels;

/// <summary>
/// Base das telas que mostram um aviso temporário ("Macro salva!", "Nome é obrigatório.").
///
/// O bloco de ~10 linhas com CancellationTokenSource e Task.Delay estava copiado em cinco
/// ViewModels. Junto com a duplicação vinha um bug: quatro das cinco telas pintavam o aviso
/// sempre de verde, porque o XAML usava SuccessBrush fixo em vez de reagir a
/// <see cref="MensagemSucesso"/> — mensagens de erro apareciam como se fossem confirmações.
/// O estilo compartilhado <c>ToastBorder</c>/<c>ToastTexto</c> em App.xaml resolve o outro lado.
/// </summary>
public abstract partial class ViewModelComMensagem : ObservableObject
{
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool    _mensagemSucesso = true;

    private CancellationTokenSource? _msgCts;

    /// <summary>Quanto tempo o aviso fica na tela antes de sumir sozinho.</summary>
    private static readonly TimeSpan Duracao = TimeSpan.FromSeconds(3.5);

    protected void MostrarMsg(string mensagem, bool sucesso)
    {
        Mensagem        = mensagem;
        MensagemSucesso = sucesso;

        // Cancela o sumiço agendado pela mensagem anterior: sem isso, uma mensagem nova
        // exibida no segundo 3 seria apagada 0,5s depois pelo timer da anterior.
        _msgCts?.Cancel();
        var cts = _msgCts = new CancellationTokenSource();

        // Sem SynchronizationContext (em teste, fora da UI) não há para onde voltar, e a
        // mensagem simplesmente fica — o teste lê o valor e não depende de tempo.
        if (SynchronizationContext.Current == null) return;

        _ = Task.Delay(Duracao, cts.Token).ContinueWith(
            _ => Mensagem = null,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>Tira o aviso da tela na hora — usado ao trocar de contexto dentro da mesma tela.</summary>
    protected void LimparMsg()
    {
        _msgCts?.Cancel();
        Mensagem = null;
    }
}
