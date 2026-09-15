using MacroHelper.Core;
using MacroHelper.Core.Entities;
using MacroHelper.Data.Repositories;

namespace MacroHelper.Services;

/// <summary>
/// As regras da agenda: o que é um compromisso válido, quando cada aviso está armado e o que
/// acontece com os avisos quando o compromisso muda de hora.
/// </summary>
public class CompromissoService
{
    private readonly CompromissoRepository _repo;
    private readonly IRelogio              _relogio;

    public CompromissoService(CompromissoRepository repo, IRelogio relogio)
    {
        _repo    = repo;
        _relogio = relogio;
    }

    public async Task<IEnumerable<Compromisso>> ObterTodosAsync() => await _repo.GetAllAsync();

    public async Task<Compromisso?> ObterPorIdAsync(int id) => await _repo.GetByIdAsync(id);

    /// <summary>
    /// Procura por título, local e observações, indiferente a acento e caixa: "reuniao" acha
    /// "Reunião". Sem termo, devolve tudo.
    ///
    /// A ordem é a do Ctrl+Espaço, e é diferente da ordem da tela: primeiro o que AINDA VEM,
    /// do mais próximo para o mais distante, e só depois o que já passou, do mais recente para
    /// o mais antigo. Quem abre a busca quer o próximo compromisso no topo — a agenda completa,
    /// em ordem de calendário, é o que a tela já mostra.
    ///
    /// Filtra em memória pela mesma razão de TarefaService.PesquisarAsync: não existe coluna
    /// normalizada aqui, e o LIKE do SQLite só ignora maiúsculas para ASCII — procurar "tecnico"
    /// jamais acharia "técnico".
    /// </summary>
    public async Task<IEnumerable<Compromisso>> PesquisarAsync(string termo)
    {
        var agora = _relogio.Agora;
        var todos = (await _repo.GetAllAsync()).ToList();

        var alvo = TextoNormalizado.Para(termo);
        if (!string.IsNullOrWhiteSpace(alvo))
            todos = todos
                .Where(c => TextoNormalizado.Para($"{c.Titulo} {c.Local} {c.Observacoes}")
                    .Contains(alvo, StringComparison.Ordinal))
                .ToList();

        return todos.Where(c => c.Agendado && !c.Passou(agora)).OrderBy(c => c.Quando)
            .Concat(todos.Where(c => !c.Agendado || c.Passou(agora)).OrderByDescending(c => c.Quando));
    }

    /// <summary>Os de hoje que ainda estão de pé — alimenta o Início.</summary>
    public async Task<IEnumerable<Compromisso>> ObterDeHojeAsync()
    {
        var agora = _relogio.Agora;
        return (await _repo.GetAllAsync()).Where(c => c.Agendado && c.ParaHoje(agora));
    }

    public async Task<(bool Ok, string Msg, Compromisso? Compromisso)> SalvarAsync(Compromisso c)
    {
        if (string.IsNullOrWhiteSpace(c.Titulo))
            return (false, "O título é obrigatório.", null);

        // Sem data é permitido: é o lembrete remarcado, esperando data nova. O que ele não
        // pode ter é aviso, porque não existe instante de onde descontar a antecedência.
        // Zerar aqui, e não na tela, é o que garante a regra pelos dois caminhos de gravação
        // (a tela de Lembretes e a janelinha flutuante).
        if (c.Quando is null)
        {
            c.AvisoAntecipadoMinutos   = null;
            c.AvisoNaHoraMinutos       = null;
            c.AvisoAntecipadoDisparado = false;
            c.AvisoNaHoraDisparado     = false;
        }

        c.Titulo      = c.Titulo.Trim();
        c.Local       = string.IsNullOrWhiteSpace(c.Local) ? null : c.Local.Trim();
        c.Observacoes = string.IsNullOrWhiteSpace(c.Observacoes) ? null : c.Observacoes.Trim();

        // Duração zero ou negativa é ausência de duração, não duração de nada: assim
        // HorarioTexto mostra "14:00" em vez de "14:00 – 14:00".
        if (c.DuracaoMinutos is <= 0) c.DuracaoMinutos = null;

        // Mudar a HORA rearma os dois avisos, e mudar um aviso rearma aquele aviso. Sem isto,
        // adiar a visita do técnico de quinta para sexta manteria os avisos marcados como já
        // dados — e o compromisso remarcado nunca avisaria, em silêncio.
        if (c.Id != 0)
        {
            var anterior = await _repo.GetByIdAsync(c.Id);
            if (anterior != null)
            {
                var mudouAHora = anterior.Quando != c.Quando;

                if (mudouAHora || anterior.AvisoAntecipadoMinutos != c.AvisoAntecipadoMinutos)
                    c.AvisoAntecipadoDisparado = false;

                if (mudouAHora || anterior.AvisoNaHoraMinutos != c.AvisoNaHoraMinutos)
                    c.AvisoNaHoraDisparado = false;
            }
        }

        SilenciarAvisosQueJaPassaram(c);

        var choque = await AcharChoqueAsync(c);

        if (c.Id == 0)
        {
            c.Id = await _repo.InsertAsync(c);
            return (true, MensagemDeSalvo("Lembrete criado!", choque), c);
        }

        await _repo.UpdateAsync(c);
        return (true, MensagemDeSalvo("Lembrete atualizado!", choque), c);
    }

    /// <summary>
    /// Um aviso cuja hora já passou no momento de salvar nasce marcado como dado.
    ///
    /// Quem acabou de digitar "hoje às 14h, avisar 1 dia antes" às 9h da manhã não precisa de
    /// um balão trinta segundos depois de clicar em Salvar dizendo o que acabou de escrever —
    /// isso se lê como defeito, não como lembrete. O aviso que ainda está no futuro (o de meia
    /// hora antes, nesse exemplo) continua armado e é ele que vai avisar.
    /// </summary>
    private void SilenciarAvisosQueJaPassaram(Compromisso c)
    {
        var agora = _relogio.Agora;

        if (c.AvisoAntecipadoEm is { } antecipado && antecipado <= agora) c.AvisoAntecipadoDisparado = true;
        if (c.AvisoNaHoraEm     is { } naHora     && naHora     <= agora) c.AvisoNaHoraDisparado     = true;
    }

    /// <summary>
    /// Outro compromisso agendado ocupando o mesmo pedaço da agenda.
    ///
    /// Avisa, não impede: duas coisas ao mesmo tempo às vezes são de propósito (uma delas é
    /// remota, ou vai ser resolvida em cinco minutos). Bloquear obrigaria a mentir no horário
    /// para conseguir gravar, e um horário mentido faz o aviso sair na hora errada.
    /// </summary>
    private async Task<Compromisso?> AcharChoqueAsync(Compromisso c)
    {
        if (!c.Agendado) return null;

        return (await _repo.GetAllAsync())
            .FirstOrDefault(outro => outro.Id != c.Id && outro.Agendado && c.ChocaCom(outro));
    }

    private static string MensagemDeSalvo(string sucesso, Compromisso? choque) =>
        choque == null
            ? sucesso
            : $"{sucesso} Atenção: bate com \"{choque.Titulo}\", {choque.HorarioTexto}.";

    /// <summary>
    /// Marca como realizado, cancelado ou de volta para agendado.
    ///
    /// Cancelar é a forma de calar os avisos de um compromisso que não vai mais acontecer sem
    /// apagar o registro dele — a consulta da varredura só enxerga situacao = 0.
    /// </summary>
    public async Task<(bool Ok, string Msg)> DefinirSituacaoAsync(int id, SituacaoCompromisso situacao)
    {
        var compromisso = await _repo.GetByIdAsync(id);
        if (compromisso == null) return (false, "Lembrete não encontrado.");

        await _repo.DefinirSituacaoAsync(id, situacao);

        return (true, situacao switch
        {
            SituacaoCompromisso.Realizado => "Marcado como feito.",
            SituacaoCompromisso.Cancelado => "Lembrete cancelado. Os avisos dele não vão mais sair.",
            _                             => "Lembrete de volta na lista.",
        });
    }

    public async Task<(bool Ok, string Msg)> ExcluirAsync(int id)
    {
        var compromisso = await _repo.GetByIdAsync(id);
        if (compromisso == null) return (false, "Lembrete não encontrado.");

        await _repo.DeleteAsync(id);
        return (true, $"Lembrete \"{compromisso.Titulo}\" excluído.");
    }

    /// <summary>
    /// Cada aviso cuja hora chegou e que ainda não avisou, um por linha — o mesmo compromisso
    /// pode aparecer duas vezes se o app passou tempo fechado e os dois avisos venceram juntos.
    /// Quem decide o que vira balão é o <c>LembreteService</c>.
    /// </summary>
    public async Task<IReadOnlyList<AvisoVencido>> ObterAvisosVencidosAsync(DateTime ate)
    {
        var vencidos = new List<AvisoVencido>();

        foreach (var c in await _repo.GetAvisosVencidosAsync(ate))
        {
            if (!c.AvisoAntecipadoDisparado && c.AvisoAntecipadoEm <= ate)
                vencidos.Add(new AvisoVencido(c, Antecipado: true));

            if (!c.AvisoNaHoraDisparado && c.AvisoNaHoraEm <= ate)
                vencidos.Add(new AvisoVencido(c, Antecipado: false));
        }

        return vencidos;
    }

    public async Task MarcarAvisoDisparadoAsync(int id, bool antecipado)
        => await _repo.MarcarAvisoDisparadoAsync(id, antecipado);
}
