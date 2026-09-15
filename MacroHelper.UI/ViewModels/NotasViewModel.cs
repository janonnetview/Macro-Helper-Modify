using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroHelper.Core.Entities;
using MacroHelper.Core.Texto;
using MacroHelper.Services;
using System.Collections.ObjectModel;

namespace MacroHelper.UI.ViewModels;

public partial class NotasViewModel : ViewModelComMensagem
{
    private readonly NotaService _svc;
    private CancellationTokenSource? _buscaDebounceCts;

    [ObservableProperty] private ObservableCollection<Nota> _notas = new();
    [ObservableProperty] private string _termoBusca = string.Empty;
    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private int?   _confirmandoExclusaoId;

    // ── Editor ───────────────────────────────────────────────────────────────
    [ObservableProperty] private bool    _mostrarEditor;
    [ObservableProperty] private int     _editorId;
    [ObservableProperty] private string  _editorTitulo   = string.Empty;
    [ObservableProperty] private string  _editorConteudo = string.Empty;
    [ObservableProperty] private bool    _editorFixada;
    [ObservableProperty] private string? _editorErro;

    /// <summary>
    /// Se o editor está mostrando a nota desenhada em vez do texto com a marcação.
    ///
    /// Duas telas alternadas, e não as duas lado a lado: partido ao meio, nenhum dos lados
    /// ficaria largo o bastante para a linha comprida que as notas daqui costumam ter — e foi
    /// justamente por causa dessas linhas que o editor deixou de ser uma caixa de 640px e
    /// passou a ocupar o painel inteiro.
    /// </summary>
    [ObservableProperty] private bool _editorVisualizando;

    public NotasViewModel(NotaService svc) => _svc = svc;

    public async Task CarregarAsync()
    {
        IsLoading = true;
        try { Notas = new ObservableCollection<Nota>(await _svc.PesquisarAsync(TermoBusca)); }
        finally { IsLoading = false; }
    }

    partial void OnTermoBuscaChanged(string value) { _ = DebounceCarregarAsync(); }

    /// <summary>Espera o usuário parar de digitar antes de consultar — igual à tela de Macros.</summary>
    private async Task DebounceCarregarAsync()
    {
        _buscaDebounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _buscaDebounceCts = cts;

        try { await Task.Delay(250, cts.Token); }
        catch (TaskCanceledException) { return; }

        if (cts.IsCancellationRequested) return;
        await CarregarAsync();
    }

    [RelayCommand]
    public void NovaNota()
    {
        EditorId       = 0;
        EditorTitulo   = string.Empty;
        EditorConteudo = string.Empty;
        EditorFixada   = false;
        EditorErro     = null;
        // Nota nova abre para escrever: não há o que visualizar de uma folha em branco.
        EditorVisualizando = false;
        MostrarEditor      = true;
    }

    [RelayCommand]
    public void AbrirNota(Nota nota)
    {
        EditorId       = nota.Id;
        EditorTitulo   = nota.Titulo;
        EditorConteudo = nota.Conteudo;
        EditorFixada   = nota.Fixada;
        EditorErro     = null;

        // Nota que já existe abre no desenho: quem abre uma nota de reunião quer LER a lista,
        // e a marcação atrapalha isso. Um clique em "Escrever" volta para o texto.
        EditorVisualizando = true;
        MostrarEditor      = true;
    }

    [RelayCommand]
    public void FecharEditor() => MostrarEditor = false;

    [RelayCommand]
    public void AlternarVisualizacao() => EditorVisualizando = !EditorVisualizando;

    /// <summary>
    /// Marca ou desmarca uma caixa clicada na prévia.
    ///
    /// A marca vai para o TEXTO da nota, porque é lá que ela mora. O que fica pendente é o
    /// salvamento, igual a qualquer outra edição: clicar na caixa é escrever na nota, e não um
    /// estado à parte que sumiria ao fechar.
    /// </summary>
    [RelayCommand]
    public void AlternarItem(int linha) =>
        EditorConteudo = Markdown.AlternarTarefa(EditorConteudo, linha);

    [RelayCommand]
    public async Task SalvarAsync()
    {
        EditorErro = null;

        var nota = new Nota
        {
            Id       = EditorId,
            Titulo   = EditorTitulo,
            Conteudo = EditorConteudo,
            Fixada   = EditorFixada,
        };

        var (ok, msg, _) = await _svc.SalvarAsync(nota);
        if (!ok) { EditorErro = msg; return; }

        MostrarEditor = false;
        MostrarMsg(msg, true);
        await CarregarAsync();
    }

    [RelayCommand]
    public async Task FixarAsync(Nota nota)
    {
        var (ok, msg) = await _svc.FixarAsync(nota.Id, !nota.Fixada);
        MostrarMsg(msg, ok);
        await CarregarAsync();
    }

    [RelayCommand]
    public void ConfirmarExclusao(Nota nota) => ConfirmandoExclusaoId = nota.Id;

    [RelayCommand]
    public void CancelarExclusao() => ConfirmandoExclusaoId = null;

    [RelayCommand]
    public async Task ExcluirAsync(Nota nota)
    {
        ConfirmandoExclusaoId = null;
        var (ok, msg) = await _svc.ExcluirAsync(nota.Id);
        MostrarMsg(msg, ok);
        if (ok) await CarregarAsync();
    }

    [RelayCommand]
    public void CopiarConteudo(Nota nota)
    {
        // Mesmo cuidado da tela de Macros: o clipboard é compartilhado e OpenClipboard falha
        // se outro processo o mantém aberto.
        try
        {
            System.Windows.Clipboard.SetText(nota.Conteudo);
            MostrarMsg("Conteúdo copiado!", true);
        }
        catch (Exception ex)
        {
            App.LogErro(ex);
            MostrarMsg("Não foi possível copiar agora. Tente de novo em um instante.", false);
        }
    }
}
