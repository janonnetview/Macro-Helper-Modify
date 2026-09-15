using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroHelper.Core.Entities;
using MacroHelper.Services;
using MacroHelper.UI.Helpers;
using System.Collections.ObjectModel;

namespace MacroHelper.UI.ViewModels;

public partial class TarefasViewModel : ViewModelComMensagem
{
    private readonly TarefaService _svc;
    private readonly IRelogio      _relogio;
    private List<Tarefa> _todas = [];

    [ObservableProperty] private ObservableCollection<Tarefa> _tarefas = new();
    [ObservableProperty] private string _filtro    = "abertas";
    [ObservableProperty] private bool   _isLoading = false;
    [ObservableProperty] private int?   _confirmandoExclusaoId;

    // Contadores dos chips de filtro
    [ObservableProperty] private int _totalAbertas;
    [ObservableProperty] private int _totalHoje;
    [ObservableProperty] private int _totalAtrasadas;
    [ObservableProperty] private int _totalConcluidas;

    // ── Formulário ───────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _mostrarFormulario;
    [ObservableProperty] private string _formTitulo      = string.Empty;
    [ObservableProperty] private int    _formId;
    [ObservableProperty] private string _formTexto       = string.Empty;
    [ObservableProperty] private string _formObservacoes = string.Empty;
    [ObservableProperty] private PrioridadeTarefa  _formPrioridade  = PrioridadeTarefa.Normal;
    [ObservableProperty] private RecorrenciaTarefa _formRecorrencia = RecorrenciaTarefa.Nenhuma;
    [ObservableProperty] private string  _formPrazo        = string.Empty;
    [ObservableProperty] private string  _formLembreteData = string.Empty;
    [ObservableProperty] private string  _formLembreteHora = string.Empty;
    [ObservableProperty] private string? _formErro;

    /// <summary>
    /// As opções da lista "Repetir", na ordem do enum — do intervalo mais curto para o mais
    /// longo. Vem de <c>Enum.GetValues</c> e não de uma lista escrita à mão: acrescentar uma
    /// recorrência nova ao enum a faz aparecer na tela sem ninguém precisar lembrar disso.
    /// </summary>
    public IReadOnlyList<RecorrenciaTarefa> RecorrenciasDisponiveis { get; } =
        Enum.GetValues<RecorrenciaTarefa>();

    public TarefasViewModel(TarefaService svc, IRelogio relogio)
    {
        _svc     = svc;
        _relogio = relogio;
    }

    public async Task CarregarAsync()
    {
        IsLoading = true;
        try
        {
            _todas = (await _svc.ObterTodasAsync()).ToList();
            AplicarFiltro();
        }
        finally { IsLoading = false; }
    }

    partial void OnFiltroChanged(string value) => AplicarFiltro();

    private void AplicarFiltro()
    {
        var agora = _relogio.Agora;

        TotalAbertas    = _todas.Count(t => !t.Concluida);
        TotalHoje       = _todas.Count(t => t.ParaHoje(agora));
        TotalAtrasadas  = _todas.Count(t => t.Atrasada(agora));
        TotalConcluidas = _todas.Count(t => t.Concluida);

        Tarefas = new ObservableCollection<Tarefa>(Filtro switch
        {
            "hoje"       => _todas.Where(t => t.ParaHoje(agora)),
            "atrasadas"  => _todas.Where(t => t.Atrasada(agora)),
            "concluidas" => _todas.Where(t => t.Concluida),
            "todas"      => _todas,
            _            => _todas.Where(t => !t.Concluida),
        });
    }

    [RelayCommand]
    public void DefinirFiltro(string filtro) => Filtro = filtro;

    [RelayCommand]
    public async Task ConcluirAsync(Tarefa tarefa)
    {
        var (ok, msg) = await _svc.ConcluirAsync(tarefa.Id, !tarefa.Concluida);
        MostrarMsg(msg, ok);
        await CarregarAsync();
    }

    [RelayCommand]
    public void ConfirmarExclusao(Tarefa tarefa) => ConfirmandoExclusaoId = tarefa.Id;

    [RelayCommand]
    public void CancelarExclusao() => ConfirmandoExclusaoId = null;

    [RelayCommand]
    public async Task ExcluirAsync(Tarefa tarefa)
    {
        ConfirmandoExclusaoId = null;
        var (ok, msg) = await _svc.ExcluirAsync(tarefa.Id);
        MostrarMsg(msg, ok);
        if (ok) await CarregarAsync();
    }

    // ── Formulário ───────────────────────────────────────────────────────────

    [RelayCommand]
    public void NovaTarefa()
    {
        FormId           = 0;
        FormTitulo       = "Nova tarefa";
        FormTexto        = string.Empty;
        FormObservacoes  = string.Empty;
        FormPrioridade   = PrioridadeTarefa.Normal;
        FormRecorrencia  = RecorrenciaTarefa.Nenhuma;
        FormPrazo        = string.Empty;
        FormLembreteData = string.Empty;
        FormLembreteHora = string.Empty;
        FormErro         = null;
        MostrarFormulario = true;
    }

    [RelayCommand]
    public void EditarTarefa(Tarefa tarefa)
    {
        FormId           = tarefa.Id;
        FormTitulo       = "Editar tarefa";
        FormTexto        = tarefa.Titulo;
        FormObservacoes  = tarefa.Observacoes ?? string.Empty;
        FormPrioridade   = tarefa.Prioridade;
        FormRecorrencia  = tarefa.Recorrencia;
        FormPrazo        = tarefa.Prazo?.ToString("dd/MM/yyyy") ?? string.Empty;
        FormLembreteData = tarefa.Lembrete?.ToString("dd/MM/yyyy") ?? string.Empty;
        FormLembreteHora = tarefa.Lembrete?.ToString("HH:mm") ?? string.Empty;
        FormErro         = null;
        MostrarFormulario = true;
    }

    [RelayCommand]
    public void FecharFormulario() => MostrarFormulario = false;

    [RelayCommand]
    public void DefinirPrioridade(string prioridade)
    {
        if (Enum.TryParse<PrioridadeTarefa>(prioridade, out var p)) FormPrioridade = p;
    }

    /// <summary>
    /// Escolhe de quanto em quanto tempo a tarefa volta. Recebe o NOME do valor, e não o
    /// índice de um chip: reordenar os botões no XAML não pode trocar mensal por semanal.
    /// </summary>
    [RelayCommand]
    public void DefinirRecorrencia(string recorrencia)
    {
        if (Enum.TryParse<RecorrenciaTarefa>(recorrencia, out var r)) FormRecorrencia = r;
    }

    /// <summary>Atalhos de prazo — digitar a data é sempre possível, mas raramente necessário.</summary>
    [RelayCommand]
    public void DefinirPrazoRapido(string quando)
    {
        var hoje = _relogio.Agora.Date;
        FormPrazo = (quando switch
        {
            "amanha" => hoje.AddDays(1),
            _        => hoje,
        }).ToString("dd/MM/yyyy");
    }

    [RelayCommand]
    public void DefinirLembreteRapido(string quando)
    {
        var agora = _relogio.Agora;
        var alvo = quando switch
        {
            "amanha9" => agora.Date.AddDays(1).AddHours(9),
            _         => agora.AddHours(1),
        };
        FormLembreteData = alvo.ToString("dd/MM/yyyy");
        FormLembreteHora = alvo.ToString("HH:mm");
    }

    [RelayCommand]
    public void LimparLembrete()
    {
        FormLembreteData = string.Empty;
        FormLembreteHora = string.Empty;
    }

    [RelayCommand]
    public async Task SalvarAsync()
    {
        FormErro = null;

        if (string.IsNullOrWhiteSpace(FormTexto))
        {
            FormErro = "O que precisa ser feito?";
            return;
        }

        DateTime? prazo = null;
        if (!string.IsNullOrWhiteSpace(FormPrazo))
        {
            prazo = InterpretarData(FormPrazo);
            if (prazo == null) { FormErro = "Prazo inválido. Use dd/mm/aaaa."; return; }
        }

        DateTime? lembrete = null;
        if (!string.IsNullOrWhiteSpace(FormLembreteData))
        {
            var data = InterpretarData(FormLembreteData);
            if (data == null) { FormErro = "Data do lembrete inválida. Use dd/mm/aaaa."; return; }

            var hora = InterpretarHora(FormLembreteHora);
            if (hora == null) { FormErro = "Hora do lembrete inválida. Use hh:mm."; return; }

            lembrete = data.Value.Add(hora.Value);
        }
        else if (!string.IsNullOrWhiteSpace(FormLembreteHora))
        {
            FormErro = "Informe também a data do lembrete.";
            return;
        }

        // Editar mantém o que não está no formulário (data de conclusão, marca de disparo);
        // criar começa do zero.
        var tarefa = FormId == 0
            ? new Tarefa()
            : await _svc.ObterPorIdAsync(FormId) ?? new Tarefa { Id = FormId };

        tarefa.Titulo      = FormTexto;
        tarefa.Observacoes = string.IsNullOrWhiteSpace(FormObservacoes) ? null : FormObservacoes.Trim();
        tarefa.Prioridade  = FormPrioridade;
        tarefa.Recorrencia = FormRecorrencia;
        tarefa.Prazo       = prazo;
        tarefa.Lembrete    = lembrete;

        var (ok, msg, _) = await _svc.SalvarAsync(tarefa);
        if (!ok) { FormErro = msg; return; }

        MostrarFormulario = false;
        MostrarMsg(msg, true);
        await CarregarAsync();
    }

    // ── Interpretação das datas digitadas ────────────────────────────

    // Mora em DataDigitada: o calendário do campo de data lê o mesmo texto para saber que dia
    // abrir marcado, e duas leituras diferentes dariam um calendário abrindo num dia e um
    // "Salvar" gravando outro.
    private DateTime? InterpretarData(string texto) => DataDigitada.Interpretar(texto, _relogio.Agora);

    private static TimeSpan? InterpretarHora(string texto) => DataDigitada.InterpretarHora(texto);
}
