using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroHelper.Core.Agenda;
using MacroHelper.Core.Entities;
using MacroHelper.Services;
using MacroHelper.UI.Helpers;
using MacroHelper.UI.Properties;
using System.Collections.ObjectModel;
using System.Globalization;

namespace MacroHelper.UI.ViewModels;

/// <summary>
/// A agenda: o que está marcado, em ordem de relógio.
///
/// Os campos do formulário são strings, inclusive os que viram número — a mesma escolha dos
/// chips de filtro e de prioridade em Tarefas. Um DataTrigger comparando string com string é
/// a única forma de "chip aceso" que não depende de conversão implícita do WPF, e a tradução
/// para minutos acontece num lugar só, no Salvar.
/// </summary>
public partial class CompromissosViewModel : ViewModelComMensagem
{
    private readonly CompromissoService _svc;
    private readonly IRelogio           _relogio;
    private List<Compromisso> _todos = [];

    [ObservableProperty] private ObservableCollection<Compromisso> _compromissos = new();
    [ObservableProperty] private string _filtro    = "proximos";
    [ObservableProperty] private bool   _isLoading = false;
    [ObservableProperty] private int?   _confirmandoExclusaoId;

    // ── Calendário ───────────────────────────────────────────────────────────

    /// <summary>"lista", "semana" ou "mes". A lista continua sendo o que abre por padrão.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarCalendario))]
    private string _modo = "lista";

    /// <summary>Se a tela está numa das grades. É o que troca o corpo inteiro da página.</summary>
    public bool MostrarCalendario => Modo != "lista";

    /// <summary>
    /// O dia de referência do período mostrado. A semana é a dele; o mês é o dele.
    ///
    /// Um dia só, e não um par de datas, porque as duas grades derivam o período inteiro dele
    /// — e assim trocar de semana para mês mantém a pessoa perto de onde ela estava.
    /// </summary>
    [ObservableProperty] private DateTime _ancora;

    [ObservableProperty] private string _tituloDoPeriodo = string.Empty;

    /// <summary>Os sete dias da semana mostrada, com os compromissos já postos na grade.</summary>
    public ObservableCollection<DiaDaAgenda> DiasDaSemana { get; } = new();

    /// <summary>As seis linhas de sete dias do mês, numa lista só.</summary>
    public ObservableCollection<DiaDaAgenda> DiasDoMes { get; } = new();

    /// <summary>As faixas de hora da grade semanal: a régua da esquerda e as linhas do fundo.</summary>
    public ObservableCollection<HoraDaGrade> HorasDaGrade { get; } = new();

    /// <summary>"Seg" a "Dom", para o cabeçalho do mês. A semana começa na segunda.</summary>
    public IReadOnlyList<string> TitulosDosDias { get; } =
        ["Seg", "Ter", "Qua", "Qui", "Sex", "Sáb", "Dom"];

    /// <summary>
    /// Quantos lembretes estão sem data.
    ///
    /// A grade não tem onde desenhá-los, e some com eles seria pior do que não ter grade: o
    /// que está por marcar é justamente o que precisa de decisão. Por isso o calendário mostra
    /// uma tarja com a contagem, que leva de volta para a lista.
    /// </summary>
    [ObservableProperty] private int _totalSemData;

    // Contadores dos chips de filtro
    [ObservableProperty] private int _totalProximos;
    [ObservableProperty] private int _totalHoje;
    [ObservableProperty] private int _totalSemana;
    [ObservableProperty] private int _totalHistorico;

    // ── Formulário ───────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _mostrarFormulario;
    [ObservableProperty] private string _formTitulo      = string.Empty;
    [ObservableProperty] private int    _formId;
    [ObservableProperty] private string _formTexto       = string.Empty;
    [ObservableProperty] private string _formLocal       = string.Empty;
    [ObservableProperty] private string _formObservacoes = string.Empty;
    [ObservableProperty] private string _formData        = string.Empty;
    [ObservableProperty] private string _formHora        = string.Empty;
    [ObservableProperty] private string _formDuracao     = string.Empty;

    /// <summary>Minutos antes, como texto. Vazio = este aviso não existe.</summary>
    [ObservableProperty] private string  _formAvisoAntecipado = string.Empty;
    [ObservableProperty] private string  _formAvisoNaHora     = string.Empty;
    [ObservableProperty] private string? _formErro;

    public CompromissosViewModel(CompromissoService svc, IRelogio relogio)
    {
        _svc     = svc;
        _relogio = relogio;
        _ancora  = relogio.Agora.Date;

        // Ler a preferência não pode derrubar a tela: em máquina sem o arquivo de configuração
        // gravado ainda, o padrão é a lista.
        try { Modo = Normalizar(Settings.Default.ModoAgenda); }
        catch (Exception ex) { App.LogErro(ex); }
    }

    private static string Normalizar(string? modo) =>
        modo is "semana" or "mes" ? modo : "lista";

    public async Task CarregarAsync()
    {
        IsLoading = true;
        try
        {
            _todos = (await _svc.ObterTodosAsync()).ToList();
            AplicarFiltro();
            MontarCalendario();
        }
        finally { IsLoading = false; }
    }

    partial void OnFiltroChanged(string value) => AplicarFiltro();

    partial void OnModoChanged(string value)
    {
        MontarCalendario();

        try
        {
            Settings.Default.ModoAgenda = value;
            Settings.Default.Save();
        }
        catch (Exception ex) { App.LogErro(ex); }
    }

    partial void OnAncoraChanged(DateTime value) => MontarCalendario();

    /// <summary>
    /// Verdadeiro enquanto o campo de data está vazio, ou seja, enquanto o lembrete é um
    /// "a marcar". O formulário usa isto para AVISAR que os avisos não vão sair: escolher
    /// "1 dia antes" e ver a escolha sumir ao salvar se lê como defeito, não como regra.
    /// </summary>
    [ObservableProperty] private bool _formSemData;

    partial void OnFormDataChanged(string value) =>
        FormSemData = string.IsNullOrWhiteSpace(value);

    private void AplicarFiltro()
    {
        var agora = _relogio.Agora;

        bool AindaVem(Compromisso c) => c.Agendado && !c.Passou(agora);

        // "Nesta semana" é uma janela de calendário, e o que não tem data não cabe em janela
        // nenhuma: ele aparece em "Próximos", que é onde se decide o que fazer com ele.
        bool NaSemana(Compromisso c) => c.Quando is { } q && q.Date <= agora.Date.AddDays(7);

        TotalProximos  = _todos.Count(AindaVem);
        TotalHoje      = _todos.Count(c => c.Agendado && c.ParaHoje(agora));
        TotalSemana    = _todos.Count(c => AindaVem(c) && NaSemana(c));
        TotalHistorico = _todos.Count(c => !AindaVem(c));

        // O histórico é o único que se lê de trás para frente: ali a pergunta é "o que acabou
        // de acontecer", e não "o que vem primeiro".
        Compromissos = new ObservableCollection<Compromisso>(Filtro switch
        {
            "hoje"      => _todos.Where(c => c.Agendado && c.ParaHoje(agora)),
            "semana"    => _todos.Where(c => AindaVem(c) && NaSemana(c)),
            "historico" => _todos.Where(c => !AindaVem(c)).OrderByDescending(c => c.Quando),
            _           => _todos.Where(AindaVem),
        });
    }

    [RelayCommand]
    public void DefinirFiltro(string filtro) => Filtro = filtro;

    // ── Calendário ───────────────────────────────────────────────────────────

    /// <summary>Troca entre a lista, a semana e o mês.</summary>
    [RelayCommand]
    public void DefinirModo(string modo) => Modo = Normalizar(modo);

    [RelayCommand]
    public void PeriodoAnterior() => Ancora = Modo == "mes" ? Ancora.AddMonths(-1) : Ancora.AddDays(-7);

    [RelayCommand]
    public void PeriodoSeguinte() => Ancora = Modo == "mes" ? Ancora.AddMonths(1) : Ancora.AddDays(7);

    [RelayCommand]
    public void IrParaHoje() => Ancora = _relogio.Agora.Date;

    /// <summary>
    /// Novo lembrete no dia clicado, às 9h.
    ///
    /// É o que o cabeçalho do dia (na semana) e a célula do mês fazem: ali o clique diz o dia,
    /// e não a hora. As 9h são o mesmo palpite que o botão "Novo lembrete" já dava.
    /// </summary>
    [RelayCommand]
    public void NovoNoDia(DateTime dia) => NovoNoInstante(dia.Date.AddHours(9));

    /// <summary>
    /// Novo lembrete na hora exata em que se clicou na grade da semana.
    ///
    /// É o que faz a grade valer como agenda e não como relatório: clicar no espaço das 14h30
    /// da quinta é o gesto de marcar alguma coisa ali. Sem isto, o corpo da grade só servia
    /// para olhar, e marcar exigia voltar ao botão do alto e digitar dia e hora à mão.
    /// </summary>
    [RelayCommand]
    public void NovoNoInstante(DateTime quando)
    {
        NovoCompromisso();
        FormData = quando.ToString("dd/MM/yyyy");
        FormHora = quando.ToString("HH:mm");
    }

    [RelayCommand]
    public async Task MarcarRealizadoAsync(Compromisso compromisso)
        => await MudarSituacaoAsync(compromisso, SituacaoCompromisso.Realizado);

    [RelayCommand]
    public async Task CancelarCompromissoAsync(Compromisso compromisso)
        => await MudarSituacaoAsync(compromisso, SituacaoCompromisso.Cancelado);

    /// <summary>Devolve um realizado ou cancelado para a agenda — inclusive rearmando os avisos.</summary>
    [RelayCommand]
    public async Task ReabrirAsync(Compromisso compromisso)
        => await MudarSituacaoAsync(compromisso, SituacaoCompromisso.Agendado);

    private async Task MudarSituacaoAsync(Compromisso compromisso, SituacaoCompromisso situacao)
    {
        var (ok, msg) = await _svc.DefinirSituacaoAsync(compromisso.Id, situacao);
        MostrarMsg(msg, ok);
        await CarregarAsync();
    }

    [RelayCommand]
    public void ConfirmarExclusao(Compromisso compromisso) => ConfirmandoExclusaoId = compromisso.Id;

    [RelayCommand]
    public void CancelarExclusao() => ConfirmandoExclusaoId = null;

    [RelayCommand]
    public async Task ExcluirAsync(Compromisso compromisso)
    {
        ConfirmandoExclusaoId = null;
        var (ok, msg) = await _svc.ExcluirAsync(compromisso.Id);
        MostrarMsg(msg, ok);
        if (ok) await CarregarAsync();
    }


    // ── A montagem das duas grades ───────────────────────────────────────────

    /// <summary>
    /// Pixels de uma hora na grade da semana.
    ///
    /// Sessenta, e não um número qualquer: assim UM MINUTO É UM PIXEL, e a altura de um bloco é
    /// a duração dele lida direto. Era 52, e nesse tamanho um compromisso de meia hora ficava
    /// com 26px, dos quais 6 iam para o respiro da caixa: sobrava uma linha apertada, e o
    /// título mal se lia. A grade rola, então altura de hora custa quantas horas cabem na tela
    /// de uma vez, e não informação.
    /// </summary>
    public const double AlturaDaHora = 60;

    /// <summary>
    /// A janela de horas que a grade mostra quando nada obriga o contrário: das 8h às 19h.
    ///
    /// Ela CRESCE para caber o que existe (um compromisso às 6h30 puxa o topo para as 6h), mas
    /// não encolhe: uma semana com um compromisso só não pode virar uma faixa de duas horas,
    /// porque aí o dia inteiro deixa de ser legível como dia.
    /// </summary>
    private const int PrimeiraHoraPadrao = 8;
    private const int UltimaHoraPadrao   = 19;

    private void MontarCalendario()
    {
        TotalSemData = _todos.Count(c => c.Agendado && !c.TemData);

        if (Modo == "semana") MontarSemana();
        if (Modo == "mes")    MontarMes();

        TituloDoPeriodo = Modo == "mes" ? TituloDoMes() : TituloDaSemana();
    }

    private void MontarSemana()
    {
        var inicio = InicioDaSemana(Ancora);
        var dias   = Enumerable.Range(0, 7).Select(i => inicio.AddDays(i)).ToList();

        var (primeira, ultima) = FaixaDeHoras(dias);

        HorasDaGrade.Clear();
        for (var hora = primeira; hora < ultima; hora++)
            HorasDaGrade.Add(new HoraDaGrade { Hora = hora, Altura = AlturaDaHora });

        var hoje = _relogio.Agora.Date;

        DiasDaSemana.Clear();
        foreach (var dia in dias)
        {
            var doDia = DoDia(dia);
            DiasDaSemana.Add(new DiaDaAgenda
            {
                Data         = dia,
                EhHoje       = dia == hoje,
                Faixas       = FaixasDe(dia, primeira, ultima),
                Blocos       = GradeDeAgenda.Montar(doDia),
                TemChoque    = GradeDeAgenda.TemChoque(doDia),
                PrimeiraHora = primeira,
                UltimaHora   = ultima,
                AlturaDaHora = AlturaDaHora,
            });
        }
    }

    private void MontarMes()
    {
        // Seis semanas sempre, e não as que o mês precisa: uma grade que muda de altura entre
        // março e abril pula na tela a cada clique na seta.
        var primeiroDoMes = new DateTime(Ancora.Year, Ancora.Month, 1);
        var inicio        = InicioDaSemana(primeiroDoMes);
        var hoje          = _relogio.Agora.Date;

        DiasDoMes.Clear();
        for (var i = 0; i < 42; i++)
        {
            var dia   = inicio.AddDays(i);
            var doDia = DoDia(dia).Where(c => !c.Cancelado).OrderBy(c => c.Quando).ToList();

            DiasDoMes.Add(new DiaDaAgenda
            {
                Data         = dia,
                EhHoje       = dia == hoje,
                ForaDoMes    = dia.Month != Ancora.Month,
                Itens        = doDia.Take(DiaDaAgenda.EtiquetasNoMes).ToList(),
                Extras       = Math.Max(0, doDia.Count - DiaDaAgenda.EtiquetasNoMes),
                TemExtras    = doDia.Count > DiaDaAgenda.EtiquetasNoMes,
                TemChoque    = GradeDeAgenda.TemChoque(doDia),
                PrimeiraHora = PrimeiraHoraPadrao,
                UltimaHora   = UltimaHoraPadrao,
                AlturaDaHora = AlturaDaHora,
            });
        }
    }

    /// <summary>
    /// As meias horas clicáveis de um dia.
    ///
    /// De meia em meia hora, e não de hora em hora, porque compromisso às 14h30 é tão comum
    /// quanto às 14h, e clicar numa grade que só entende hora cheia obrigaria a corrigir o
    /// campo em seguida toda vez.
    /// </summary>
    private static List<FaixaDeHora> FaixasDe(DateTime dia, int primeira, int ultima)
    {
        var faixas = new List<FaixaDeHora>((ultima - primeira) * 2);

        for (var hora = primeira; hora < ultima; hora++)
            for (var meia = 0; meia < 2; meia++)
                faixas.Add(new FaixaDeHora
                {
                    Inicio       = dia.Date.AddHours(hora).AddMinutes(meia * 30),
                    Altura       = AlturaDaHora / 2,
                    InicioDeHora = meia == 0,
                });

        return faixas;
    }

    private List<Compromisso> DoDia(DateTime dia) =>
        _todos.Where(c => c.Quando?.Date == dia).ToList();

    /// <summary>
    /// A faixa de horas que a semana precisa mostrar.
    ///
    /// Sem isto, ou a grade tem 24 horas de altura (e o dia de trabalho fica espremido no meio
    /// de um deserto), ou tem horário comercial fixo (e o compromisso das 7h desaparece).
    /// </summary>
    private (int Primeira, int Ultima) FaixaDeHoras(IEnumerable<DateTime> dias)
    {
        var blocos = dias.SelectMany(d => GradeDeAgenda.Montar(DoDia(d))).ToList();
        if (blocos.Count == 0) return (PrimeiraHoraPadrao, UltimaHoraPadrao);

        var primeira = Math.Min(PrimeiraHoraPadrao, blocos.Min(b => b.InicioMinutos) / 60);
        var ultima   = Math.Max(UltimaHoraPadrao, (int)Math.Ceiling(blocos.Max(b => b.FimMinutos) / 60.0));

        return (Math.Clamp(primeira, 0, 23), Math.Clamp(ultima, primeira + 1, 24));
    }

    /// <summary>A segunda-feira da semana do dia dado. A semana comeca na segunda, como a agenda de trabalho.</summary>
    private static DateTime InicioDaSemana(DateTime dia) =>
        dia.Date.AddDays(-(((int)dia.DayOfWeek + 6) % 7));

    private string TituloDaSemana()
    {
        var inicio = InicioDaSemana(Ancora);
        var fim    = inicio.AddDays(6);

        return inicio.Month == fim.Month
            ? $"{inicio:dd} a {fim:dd} de {Mes(fim)} de {fim:yyyy}"
            : $"{inicio:dd} de {Mes(inicio)} a {fim:dd} de {Mes(fim)} de {fim:yyyy}";
    }

    private string TituloDoMes()
    {
        var nome = Mes(Ancora);
        return $"{char.ToUpper(nome[0], PtBr)}{nome[1..]} de {Ancora:yyyy}";
    }

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static string Mes(DateTime data) => PtBr.DateTimeFormat.GetMonthName(data.Month);

    // ── Formulário ───────────────────────────────────────────────────────────

    [RelayCommand]
    public void NovoCompromisso()
    {
        var agora = _relogio.Agora;

        FormId       = 0;
        FormTitulo   = "Novo lembrete";
        FormTexto    = string.Empty;
        FormLocal    = string.Empty;
        FormObservacoes = string.Empty;

        // Amanhã às 9h: um compromisso quase nunca é para daqui a cinco minutos, e um campo de
        // data em branco obriga a digitar o óbvio toda vez.
        FormData    = agora.Date.AddDays(1).ToString("dd/MM/yyyy");
        FormHora    = "09:00";
        FormDuracao = string.Empty;

        FormAvisoAntecipado = Antecedencia.PadraoAntecipado.ToString(CultureInfo.InvariantCulture);
        FormAvisoNaHora     = Antecedencia.PadraoNaHora.ToString(CultureInfo.InvariantCulture);

        FormErro          = null;
        MostrarFormulario = true;
    }

    [RelayCommand]
    public void EditarCompromisso(Compromisso compromisso)
    {
        FormId          = compromisso.Id;
        FormTitulo      = "Editar lembrete";
        FormTexto       = compromisso.Titulo;
        FormLocal       = compromisso.Local ?? string.Empty;
        FormObservacoes = compromisso.Observacoes ?? string.Empty;
        // Sem data, os dois campos abrem vazios: é assim que o formulário oferece
        // "marcar agora" sem sugerir uma data que ninguém escolheu.
        FormData        = compromisso.Quando?.ToString("dd/MM/yyyy") ?? string.Empty;
        FormHora        = compromisso.Quando?.ToString("HH:mm")      ?? string.Empty;
        FormDuracao     = Texto(compromisso.DuracaoMinutos);

        FormAvisoAntecipado = Texto(compromisso.AvisoAntecipadoMinutos);
        FormAvisoNaHora     = Texto(compromisso.AvisoNaHoraMinutos);

        FormErro          = null;
        MostrarFormulario = true;
    }

    [RelayCommand]
    public void FecharFormulario() => MostrarFormulario = false;

    /// <summary>
    /// Os dois atalhos de data do formulário. Hora não tem atalho: o campo hh:mm está ao lado,
    /// e uma fileira de horários fixos era só uma segunda forma de escrever o que já dava para
    /// escrever ali.
    /// </summary>
    [RelayCommand]
    public void DefinirDataRapida(string quando)
    {
        var hoje = _relogio.Agora.Date;
        FormData = (quando switch
        {
            "amanha" => hoje.AddDays(1),
            _        => hoje,
        }).ToString("dd/MM/yyyy");
    }

    [RelayCommand]
    public void DefinirDuracao(string minutos) => FormDuracao = minutos;

    [RelayCommand]
    public void DefinirAvisoAntecipado(string minutos) => FormAvisoAntecipado = minutos;

    [RelayCommand]
    public void DefinirAvisoNaHora(string minutos) => FormAvisoNaHora = minutos;

    [RelayCommand]
    public async Task SalvarAsync()
    {
        FormErro = null;

        if (string.IsNullOrWhiteSpace(FormTexto))
        {
            FormErro = "Do que é o lembrete?";
            return;
        }

        // Os dois campos em branco significam "ainda não sei quando": grava sem data, e o
        // lembrete fica na lista esperando ser marcado. Qualquer um dos dois preenchido volta
        // a exigir os dois, porque meia data não marca nada.
        var quando = InterpretarQuando();
        if (quando.Erro != null) { FormErro = quando.Erro; return; }

        // Editar mantém o que não está no formulário (situação, marcas de aviso já dado);
        // criar começa do zero.
        var compromisso = FormId == 0
            ? new Compromisso()
            : await _svc.ObterPorIdAsync(FormId) ?? new Compromisso { Id = FormId };

        compromisso.Titulo         = FormTexto;
        compromisso.Local          = FormLocal;
        compromisso.Observacoes    = FormObservacoes;
        compromisso.Quando         = quando.Instante;
        compromisso.DuracaoMinutos = Minutos(FormDuracao);

        compromisso.AvisoAntecipadoMinutos = Minutos(FormAvisoAntecipado);
        compromisso.AvisoNaHoraMinutos     = Minutos(FormAvisoNaHora);

        var (ok, msg, _) = await _svc.SalvarAsync(compromisso);
        if (!ok) { FormErro = msg; return; }

        MostrarFormulario = false;
        MostrarMsg(msg, true);
        await CarregarAsync();
    }

    /// <summary>
    /// O que os campos de data e hora querem dizer juntos: um instante, um erro, ou nenhum
    /// dos dois (que é o lembrete por marcar).
    ///
    /// Hora em branco COM data é erro, e não "às 9h" como num lembrete de tarefa: o horário é
    /// metade do que faz disto um compromisso, e adivinhá-lo criaria um aviso para uma hora
    /// que ninguém escolheu. Data em branco COM hora é o mesmo problema pelo outro lado.
    /// </summary>
    private (DateTime? Instante, string? Erro) InterpretarQuando()
    {
        var semData = string.IsNullOrWhiteSpace(FormData);
        var semHora = string.IsNullOrWhiteSpace(FormHora);

        if (semData && semHora) return (null, null);

        if (semData) return (null, "Informe o dia, ou apague a hora para marcar a data depois.");

        var data = DataDigitada.Interpretar(FormData, _relogio.Agora);
        if (data == null) return (null, "Data inválida. Use dd/mm/aaaa.");

        if (semHora) return (null, "Informe a hora do lembrete.");

        var hora = DataDigitada.InterpretarHora(FormHora);
        if (hora == null) return (null, "Hora inválida. Use hh:mm.");

        return (data.Value.Add(hora.Value), null);
    }

    // ── Tradução entre o texto dos chips e os minutos gravados ───────────────

    private static int? Minutos(string texto) =>
        int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) ? m : null;

    private static string Texto(int? minutos) =>
        minutos?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
}
