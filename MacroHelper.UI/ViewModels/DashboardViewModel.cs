using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroHelper.Core.Entities;
using MacroHelper.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace MacroHelper.UI.ViewModels;

/// <summary>
/// O Início responde a duas perguntas: "o que eu preciso fazer agora" e "o que eu mais uso".
///
/// A lista de atividade recente saiu: num app de um usuário só ela repetia, em outra ordem,
/// a mesma informação do ranking de mais usadas. O espaço passou para as tarefas — o único
/// conteúdo da tela que tem hora marcada e que fica errado se ninguém olhar.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    /// <summary>Além disso a lista viraria uma segunda tela de Tarefas dentro do Início.</summary>
    private const int MaximoTarefasVisiveis = 6;

    private readonly MacroService       _macroService;
    private readonly LogUsoService      _logService;
    private readonly TarefaService      _tarefaService;
    private readonly CompromissoService _compromissoService;
    private readonly IRelogio           _relogio;

    [ObservableProperty] private string _nomeUsuario         = string.Empty;
    [ObservableProperty] private string _dataTexto           = string.Empty;
    [ObservableProperty] private int    _totalMacros         = 0;
    [ObservableProperty] private int    _macrosNovasSemana   = 0;
    [ObservableProperty] private int    _usadasHoje          = 0;
    [ObservableProperty] private string _minutosEconomizados = "0min";
    [ObservableProperty] private bool   _isLoading           = false;

    [ObservableProperty] private ObservableCollection<RankingItem> _maisUsadas = new();
    [ObservableProperty] private ObservableCollection<Tarefa>      _tarefas    = new();

    /// <summary>O que tem hora marcada hoje. O cartão some da tela quando está vazio.</summary>
    [ObservableProperty] private ObservableCollection<Compromisso> _compromissos = new();

    /// <summary>Quantas tarefas ficaram de fora do corte — o rótulo do "Ver todas" as menciona.</summary>
    [ObservableProperty] private int _tarefasOcultas = 0;

    /// <summary>Disparado pelo botão "Nova macro" do hero — MainViewModel decide a navegação.</summary>
    public event EventHandler? NovaMacroSolicitada;

    /// <summary>Disparado por "Ver todas" no cartão de tarefas.</summary>
    public event EventHandler? TarefasSolicitadas;

    /// <summary>Disparado por "Ver agenda" no cartão de compromissos.</summary>
    public event EventHandler? CompromissosSolicitados;

    public DashboardViewModel(
        MacroService macroService, LogUsoService logService,
        TarefaService tarefaService, CompromissoService compromissoService,
        IRelogio relogio)
    {
        _macroService       = macroService;
        _logService         = logService;
        _tarefaService      = tarefaService;
        _compromissoService = compromissoService;
        _relogio            = relogio;
    }

    [RelayCommand]
    public void NovaMacro() => NovaMacroSolicitada?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    public void VerTarefas() => TarefasSolicitadas?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    public void VerCompromissos() => CompromissosSolicitados?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Concluir direto do Início. É a única ação de escrita da tela, e existe porque atravessar
    /// para Tarefas só para marcar um item que já está visível aqui é trabalho à toa.
    /// </summary>
    [RelayCommand]
    private async Task ConcluirAsync(Tarefa? tarefa)
    {
        if (tarefa == null) return;
        await _tarefaService.ConcluirAsync(tarefa.Id, true);
        await CarregarAsync();
    }

    public async Task CarregarAsync()
    {
        IsLoading = true;
        try
        {
            var agora = _relogio.Agora;

            // Relido a cada visita: o nome é editável em Configurações, e a tela precisa
            // refletir a mudança sem exigir que o app seja reaberto.
            NomeUsuario = string.IsNullOrWhiteSpace(App.NomeDoUsuario) ? "Você" : App.NomeDoUsuario;
            DataTexto   = DataPorExtenso(agora);

            var macros = (await _macroService.ObterTodosAsync()).ToList();
            TotalMacros       = macros.Count;
            MacrosNovasSemana = macros.Count(m => m.DataCriacao >= agora.AddDays(-7));
            UsadasHoje        = await _logService.TotalHojeAsync();

            var inicioMes = new DateTime(agora.Year, agora.Month, 1);
            var fimJanela = agora.AddDays(1);

            var top = await _logService.ObterTopMacrosAsync(inicioMes, fimJanela, 5);
            MaisUsadas = new ObservableCollection<RankingItem>(
                top.Select(t => new RankingItem(t.Titulo, t.Atalho, t.Total)));

            var minutos = await _logService.EstimarMinutosEconomizadosAsync(inicioMes, fimJanela);
            MinutosEconomizados = minutos >= 60 ? $"{minutos / 60:0.#}h" : $"{minutos:0}min";

            // Atrasadas primeiro, depois as de hoje; dentro de cada grupo, prioridade alta na
            // frente e prazo mais antigo antes. Quem abre o Início vê o pior caso no topo.
            var pendentes = (await _tarefaService.ObterDeHojeEAtrasadasAsync())
                .OrderByDescending(t => t.Atrasada(agora))
                .ThenByDescending(t => t.Prioridade)
                .ThenBy(t => t.Prazo)
                .ToList();

            Tarefas        = new ObservableCollection<Tarefa>(pendentes.Take(MaximoTarefasVisiveis));
            TarefasOcultas = Math.Max(0, pendentes.Count - MaximoTarefasVisiveis);

            // Os de hoje inteiros, inclusive os que já passaram: às 15h ainda importa saber
            // que a entrega era às 14h. Uma agenda de um dia não precisa de corte.
            Compromissos = new ObservableCollection<Compromisso>(
                (await _compromissoService.ObterDeHojeAsync()).OrderBy(c => c.Quando));
        }
        finally { IsLoading = false; }
    }

    /// <summary>"Terça-feira, 19 de agosto" — o cabeçalho da tela em vez de um status inventado.</summary>
    private static string DataPorExtenso(DateTime data)
    {
        var ptBr = CultureInfo.GetCultureInfo("pt-BR");
        var dia  = ptBr.DateTimeFormat.GetDayName(data.DayOfWeek);
        return $"{char.ToUpper(dia[0], ptBr)}{dia[1..]}, {data.ToString("d 'de' MMMM", ptBr)}";
    }
}

public record RankingItem(string Titulo, string Atalho, int Total);
