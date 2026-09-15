using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroHelper.Core.Entities;
using MacroHelper.Services;
using System.Collections.ObjectModel;
using System.IO;

namespace MacroHelper.UI.ViewModels;

public partial class MacrosViewModel : ViewModelComMensagem
{
    private readonly MacroService     _macroService;
    private readonly CategoriaService _catService;
    private CancellationTokenSource? _buscaDebounceCts;

    [ObservableProperty] private ObservableCollection<Macro> _macros = new();
    [ObservableProperty] private ObservableCollection<string> _categorias = new();
    [ObservableProperty] private string  _termoBusca      = string.Empty;
    [ObservableProperty] private string  _categoriaFiltro = "Todas";
    [ObservableProperty] private bool    _isLoading       = false;
    [ObservableProperty] private bool    _mostrarFormulario = false;
    [ObservableProperty] private MacroFormViewModel? _formularioAtual;
    [ObservableProperty] private bool    _somenteFavoritos = false;
    [ObservableProperty] private bool    _mostrarArquivadas = false;

    public MacrosViewModel(MacroService macroService, CategoriaService catService)
    {
        _macroService = macroService;
        _catService   = catService;
    }

    public async Task CarregarAsync()
    {
        IsLoading = true;
        try
        {
            IEnumerable<Macro> resultado;
            if (!string.IsNullOrWhiteSpace(TermoBusca))
                resultado = await _macroService.PesquisarAsync(TermoBusca);
            else if (CategoriaFiltro != "Todas")
                resultado = await _macroService.ObterPorCategoriaAsync(CategoriaFiltro);
            else
                resultado = await _macroService.ObterTodosAsync();

            if (SomenteFavoritos)
                resultado = resultado.Where(m => m.Favorito);

            resultado = MostrarArquivadas ? resultado.Where(m => !m.Ativo) : resultado.Where(m => m.Ativo);

            Macros = new ObservableCollection<Macro>(resultado);

            var cats = await _macroService.ObterCategoriasAsync();
            Categorias = new ObservableCollection<string>(new[] { "Todas" }.Concat(cats));
        }
        finally { IsLoading = false; }
    }

    partial void OnSomenteFavoritosChanged(bool value) { var __ = CarregarAsync(); }
    partial void OnMostrarArquivadasChanged(bool value) { var __ = CarregarAsync(); }

    [RelayCommand]
    public void ToggleSomenteFavoritos() => SomenteFavoritos = !SomenteFavoritos;

    [RelayCommand]
    public void ToggleMostrarArquivadas() => MostrarArquivadas = !MostrarArquivadas;

    [RelayCommand]
    public async Task ArquivarMacro(Macro macro)
    {
        var (ok, msg) = await _macroService.ArquivarAsync(macro.Id, arquivar: true);
        MostrarMsg(msg, ok);
        if (ok) await CarregarAsync();
    }

    [RelayCommand]
    public async Task DesarquivarMacro(Macro macro)
    {
        var (ok, msg) = await _macroService.ArquivarAsync(macro.Id, arquivar: false);
        MostrarMsg(msg, ok);
        if (ok) await CarregarAsync();
    }

    [RelayCommand]
    public async Task ToggleFavorito(Macro macro)
    {
        await _macroService.ToggleFavoritoAsync(macro);
        await CarregarAsync();
    }

    [RelayCommand]
    public async Task ExportarJson()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"macros_{DateTime.Now:yyyyMMdd}.json",
                Filter   = "JSON (*.json)|*.json"
            };
            if (dialog.ShowDialog() != true) return;

            var json = await _macroService.ExportarJsonAsync();
            await File.WriteAllTextAsync(dialog.FileName, json);
            MostrarMsg("Macros exportadas com sucesso!", true);
        }
        catch (Exception ex) { MostrarMsg($"Erro ao exportar: {ex.Message}", false); }
    }

    [RelayCommand]
    public async Task ImportarJson()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "JSON (*.json)|*.json" };
            if (dialog.ShowDialog() != true) return;

            var json = await File.ReadAllTextAsync(dialog.FileName);
            var (ok, msg, _, _) = await _macroService.ImportarJsonAsync(json);
            MostrarMsg(msg, ok);
            if (ok) await CarregarAsync();
        }
        catch (Exception ex) { MostrarMsg($"Erro ao importar: {ex.Message}", false); }
    }

    [RelayCommand]
    public async Task ImportarCsv()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "CSV (*.csv)|*.csv" };
            if (dialog.ShowDialog() != true) return;

            var csv = await File.ReadAllTextAsync(dialog.FileName);
            var (ok, msg, _, _) = await _macroService.ImportarCsvAsync(csv);
            MostrarMsg(msg, ok);
            if (ok) await CarregarAsync();
        }
        catch (Exception ex) { MostrarMsg($"Erro ao importar CSV: {ex.Message}", false); }
    }

    partial void OnTermoBuscaChanged(string value)      { var __ = DebounceCarregarAsync(); }
    partial void OnCategoriaFiltroChanged(string value) { var __ = CarregarAsync(); }

    /// <summary>Espera o usuário parar de digitar (300ms) antes de buscar — evita uma chamada ao banco por tecla.</summary>
    private async Task DebounceCarregarAsync()
    {
        _buscaDebounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _buscaDebounceCts = cts;
        try { await Task.Delay(300, cts.Token); }
        catch (TaskCanceledException) { return; }
        if (cts.IsCancellationRequested) return;
        await CarregarAsync();
    }

    [RelayCommand]
    public void NovaMacro()
    {
        var vm = new MacroFormViewModel(_macroService, _catService, null);
        vm.Salvo     += async () => { MostrarFormulario = false; await CarregarAsync(); MostrarMsg("Macro criada com sucesso!", true); };
        vm.Cancelado += () => MostrarFormulario = false;
        FormularioAtual   = vm;
        MostrarFormulario = true;
    }

    [RelayCommand]
    public async Task EditarMacro(Macro macro)
    {
        // Recarrega a macro completa: as listagens não trazem imagem_base64 (coluna pesada),
        // e abrir o formulário com o objeto da lista faria o salvamento apagar a imagem.
        var completa = await _macroService.ObterPorIdAsync(macro.Id) ?? macro;

        var vm = new MacroFormViewModel(_macroService, _catService, completa);
        vm.Salvo     += async () => { MostrarFormulario = false; await CarregarAsync(); MostrarMsg("Macro atualizada!", true); };
        vm.Cancelado += () => MostrarFormulario = false;
        FormularioAtual   = vm;
        MostrarFormulario = true;
    }

    /// <summary>
    /// Duplica e já abre a cópia para editar.
    ///
    /// Abrir o formulário é metade do recurso: ninguém duplica para ficar com duas macros
    /// idênticas — duplica para fazer a variação. Sair daqui e ter de encontrar a cópia na
    /// lista para então clicar em editar seria devolver o trabalho que a duplicação tirou.
    /// </summary>
    [RelayCommand]
    public async Task DuplicarMacro(Macro macro)
    {
        var (ok, msg, copia) = await _macroService.DuplicarAsync(macro.Id);
        if (!ok || copia == null) { MostrarMsg(msg, false); return; }

        await CarregarAsync();
        await EditarMacro(copia);
        MostrarMsg(msg, true);
    }

    [RelayCommand]
    public async Task ExcluirMacro(Macro macro)
    {
        var (ok, msg) = await _macroService.ExcluirAsync(macro.Id);
        MostrarMsg(msg, ok);
        if (ok) await CarregarAsync();
    }

    [RelayCommand]
    public void CopiarConteudo(Macro macro)
    {
        // O clipboard é um recurso compartilhado do Windows: OpenClipboard falha se outro
        // processo o mantém aberto. Sem o try/catch, uma COMException derrubaria a operação
        // inteira no handler global de exceções.
        try
        {
            System.Windows.Clipboard.SetText(macro.Conteudo);
            MostrarMsg("Conteúdo copiado!", true);
        }
        catch
        {
            MostrarMsg("Não foi possível copiar agora. Tente de novo em um instante.", false);
        }
    }

    /// <summary>
    /// Abre um novo e-mail com o conteúdo da macro, usando o cliente de e-mail padrão do Windows
    /// (Outlook desktop, ou o app web configurado como padrão — incluindo Gmail). Não exige
    /// nenhuma integração de API/OAuth: usa o protocolo "mailto:" do próprio sistema operacional.
    /// </summary>
    [RelayCommand]
    public void EnviarPorEmail(Macro macro)
    {
        try
        {
            var assunto = Uri.EscapeDataString(macro.Titulo);
            var corpo   = Uri.EscapeDataString(macro.Conteudo);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                $"mailto:?subject={assunto}&body={corpo}") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MostrarMsg($"Não foi possível abrir o cliente de e-mail: {ex.Message}", false);
        }
    }

}
