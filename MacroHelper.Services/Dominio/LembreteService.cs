using MacroHelper.Core.Entities;
using System.Windows.Threading;

namespace MacroHelper.Services;

/// <summary>
/// Varre o que venceu e avisa quem souber notificar. São duas fontes: o lembrete de uma tarefa
/// e os avisos de um compromisso.
///
/// As duas moram no mesmo timer de propósito. Um segundo <see cref="DispatcherTimer"/> só para
/// compromissos duplicaria a única coisa desta classe que é difícil de acertar — o parágrafo
/// abaixo — e obrigaria a manter a mesma lição em dois lugares.
///
/// O relógio é um <see cref="DispatcherTimer"/>, e essa escolha é o ponto inteiro da classe.
/// A auditoria provou que <c>System.Threading.Timer</c> chama de volta numa thread do
/// ThreadPool, que é MTA — e tanto o clipboard (OLE, exige STA) quanto o NotifyIcon da bandeja
/// (controle WinForms criado na thread de UI) falham ali. O <c>Tick</c> do DispatcherTimer é
/// levantado PELO Dispatcher, na thread dele: não existe marshalling a esquecer, porque o
/// callback nunca sai da thread certa. A garantia fica no tipo, não na disciplina de quem chama.
/// </summary>
public class LembreteService
{
    private readonly TarefaService      _tarefas;
    private readonly CompromissoService _compromissos;
    private readonly IRelogio           _relogio;
    private readonly DispatcherTimer    _timer;
    private bool _varrendoTarefas;
    private bool _varrendoCompromissos;

    /// <summary>
    /// Lembretes mais antigos que isto são marcados como avisados, mas não geram notificação.
    /// Voltar de duas semanas de férias não deve encher a tela de balões sobre coisas que já
    /// passaram — eles continuam visíveis na tela de Tarefas, que é onde importam.
    /// </summary>
    private static readonly TimeSpan JanelaMaxima = TimeSpan.FromDays(7);

    /// <summary>
    /// A mesma ideia para compromissos, com prazo bem mais curto — e medida no COMPROMISSO, não
    /// no aviso. Um aviso de véspera que só foi visto no dia seguinte ainda vale se a reunião
    /// for daqui a uma hora; já a reunião de ontem não vira balão hoje, porque não há mais nada
    /// a fazer com essa informação. Doze horas cobrem "fechei o notebook ontem à noite".
    /// </summary>
    private static readonly TimeSpan JanelaDoCompromisso = TimeSpan.FromHours(12);

    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(30);

    /// <summary>Recebe os lembretes que acabaram de vencer. A UI decide como mostrar.</summary>
    public event Action<IReadOnlyList<Tarefa>>? LembretesVencidos;

    /// <summary>Recebe os avisos de compromisso que acabaram de vencer.</summary>
    public event Action<IReadOnlyList<AvisoVencido>>? AvisosDeCompromisso;

    /// <summary>Para onde mandar uma falha da varredura — a UI liga no log do app.</summary>
    public Action<Exception>? AoFalhar { get; set; }

    /// <param name="dispatcher">
    /// Passado explicitamente, e não pelo construtor sem parâmetros do DispatcherTimer: aquele
    /// amarra em <c>Dispatcher.CurrentDispatcher</c>, e se o contêiner de DI resolvesse este
    /// serviço a partir de uma thread de background, o Tick nunca dispararia — sem exceção,
    /// sem log, sem sintoma além de "os lembretes não avisam".
    /// </param>
    public LembreteService(TarefaService tarefas, CompromissoService compromissos,
                           IRelogio relogio, Dispatcher dispatcher)
    {
        _tarefas      = tarefas;
        _compromissos = compromissos;
        _relogio      = relogio;

        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = Intervalo,
        };
        _timer.Tick += AoDisparar;
    }

    /// <summary>
    /// Varre uma vez na hora e só então começa o intervalo. É o que faz um lembrete marcado
    /// para as 14h com o app fechado aparecer assim que o app abre, em vez de ficar esperando
    /// o próximo tique de 30 segundos ou, pior, nunca ser notado.
    /// </summary>
    public async Task IniciarAsync()
    {
        await VarrerAsync();
        _timer.Start();
    }

    public void Parar() => _timer.Stop();

    private async void AoDisparar(object? sender, EventArgs e)
    {
        // Tick é async void: uma exceção que escape daqui não tem quem a pegue e derruba o app.
        try { await VarrerAsync(); }
        catch (Exception ex) { AoFalhar?.Invoke(ex); }
    }

    /// <summary>
    /// As duas varreduras, em sequência. Uma falha na primeira não pode impedir a segunda de
    /// rodar: são fontes independentes, e o compromisso das 14h não tem nada a ver com o
    /// lembrete que deu erro.
    /// </summary>
    public async Task VarrerAsync()
    {
        try { await VarrerTarefasAsync(); }
        catch (Exception ex) { AoFalhar?.Invoke(ex); }

        try { await VarrerCompromissosAsync(); }
        catch (Exception ex) { AoFalhar?.Invoke(ex); }
    }

    /// <summary>
    /// Marca e notifica os lembretes de tarefa que venceram. Pública e sem depender do timer
    /// para que dê para verificar o comportamento passando um relógio controlado.
    /// </summary>
    /// <returns>Os lembretes efetivamente notificados.</returns>
    public async Task<IReadOnlyList<Tarefa>> VarrerTarefasAsync()
    {
        if (_varrendoTarefas) return [];
        _varrendoTarefas = true;
        try
        {
            var agora    = _relogio.Agora;
            var vencidos = (await _tarefas.ObterLembretesVencidosAsync(agora)).ToList();
            if (vencidos.Count == 0) return [];

            // Marca TODOS, inclusive os fora da janela: um lembrete de 3 semanas atrás que
            // continuasse "pendente" seria reavaliado a cada 30 segundos para sempre.
            foreach (var tarefa in vencidos)
            {
                await _tarefas.MarcarLembreteDisparadoAsync(tarefa.Id);
                tarefa.LembreteDisparado = true;
            }

            var paraAvisar = vencidos
                .Where(t => t.Lembrete >= agora - JanelaMaxima)
                .ToList();

            if (paraAvisar.Count > 0) LembretesVencidos?.Invoke(paraAvisar);
            return paraAvisar;
        }
        finally { _varrendoTarefas = false; }
    }

    /// <summary>
    /// Marca e notifica os avisos de compromisso que venceram.
    /// </summary>
    /// <returns>Os avisos que viraram notificação.</returns>
    public async Task<IReadOnlyList<AvisoVencido>> VarrerCompromissosAsync()
    {
        if (_varrendoCompromissos) return [];
        _varrendoCompromissos = true;
        try
        {
            var agora    = _relogio.Agora;
            var vencidos = await _compromissos.ObterAvisosVencidosAsync(agora);
            if (vencidos.Count == 0) return [];

            // Marca TODOS antes de qualquer notificação sair, pela mesma razão das tarefas: o
            // que não fosse marcado voltaria a cada 30 segundos, para sempre.
            foreach (var aviso in vencidos)
            {
                await _compromissos.MarcarAvisoDisparadoAsync(aviso.Compromisso.Id, aviso.Antecipado);

                if (aviso.Antecipado) aviso.Compromisso.AvisoAntecipadoDisparado = true;
                else                  aviso.Compromisso.AvisoNaHoraDisparado     = true;
            }

            var paraAvisar = vencidos
                // Os dois avisos do mesmo compromisso podem vencer na mesma varredura — é o que
                // acontece quando o app passa dias fechado. Dois balões seguidos sobre a mesma
                // reunião não informam mais que um, então sobra o de cima da hora: ele é o que
                // diz "em 20 min" em vez de "amanhã", que a essa altura seria falso.
                .GroupBy(a => a.Compromisso.Id)
                .Select(g => g.OrderBy(a => a.Antecipado).First())
                .Where(a => a.Compromisso.Quando >= agora - JanelaDoCompromisso)
                .OrderBy(a => a.Compromisso.Quando)
                .ToList();

            if (paraAvisar.Count > 0) AvisosDeCompromisso?.Invoke(paraAvisar);
            return paraAvisar;
        }
        finally { _varrendoCompromissos = false; }
    }
}
