using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroHelper.Core.Entities;
using MacroHelper.Services;
using System.Collections.ObjectModel;

namespace MacroHelper.UI.ViewModels;

public partial class VariaveisGlobaisViewModel : ViewModelComMensagem
{
    private readonly VariavelGlobalService _svc;

    [ObservableProperty] private ObservableCollection<VariavelGlobal> _variaveis = new();
    [ObservableProperty] private bool    _mostrarFormulario = false;
    [ObservableProperty] private bool    _isLoading = false;
    [ObservableProperty] private int?    _confirmandoExclusaoId;
    [ObservableProperty] private int     _usoVariavelConfirmacao;

    [ObservableProperty] private int     _formId = 0;
    [ObservableProperty] private string  _formNome = string.Empty;
    [ObservableProperty] private string  _formValorPadrao = string.Empty;
    [ObservableProperty] private string  _formDescricao = string.Empty;
    [ObservableProperty] private string  _formTitulo = "Nova Variável";
    [ObservableProperty] private string? _formErro;

    public VariaveisGlobaisViewModel(VariavelGlobalService svc) => _svc = svc;

    public async Task CarregarAsync()
    {
        IsLoading = true;
        try { Variaveis = new ObservableCollection<VariavelGlobal>(await _svc.ObterTodasAsync()); }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public void NovaVariavel()
    {
        FormId = 0; FormNome = string.Empty; FormValorPadrao = string.Empty; FormDescricao = string.Empty;
        FormErro = null; FormTitulo = "Nova Variável"; MostrarFormulario = true;
    }

    [RelayCommand]
    public void EditarVariavel(VariavelGlobal v)
    {
        FormId = v.Id; FormNome = v.Nome; FormValorPadrao = v.ValorPadrao; FormDescricao = v.Descricao ?? string.Empty;
        FormErro = null; FormTitulo = "Editar Variável"; MostrarFormulario = true;
    }

    [RelayCommand]
    public void CancelarForm() => MostrarFormulario = false;

    [RelayCommand]
    public async Task SalvarVariavelAsync()
    {
        FormErro = null;
        var v = new VariavelGlobal { Id = FormId, Nome = FormNome, ValorPadrao = FormValorPadrao, Descricao = FormDescricao };
        var (ok, msg) = await _svc.SalvarAsync(v);
        if (!ok) { FormErro = msg; return; }
        MostrarFormulario = false;
        await CarregarAsync();
        MostrarMsg(msg, true);
    }

    [RelayCommand]
    public async Task ExcluirVariavelAsync(VariavelGlobal v)
    {
        if (ConfirmandoExclusaoId != v.Id)
        {
            UsoVariavelConfirmacao = await _svc.ContarUsoAsync(v.Nome);
            ConfirmandoExclusaoId = v.Id;
            return;
        }

        ConfirmandoExclusaoId = null;
        await _svc.ExcluirAsync(v.Id);
        await CarregarAsync();
        MostrarMsg("Variável excluída.", true);
    }

    [RelayCommand]
    public void CancelarExclusao() => ConfirmandoExclusaoId = null;

}
