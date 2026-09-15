using MacroHelper.Core;
using MacroHelper.Core.Entities;
using MacroHelper.Data.Repositories;

namespace MacroHelper.Services;

public class TarefaService
{
    private readonly TarefaRepository _repo;
    private readonly IRelogio         _relogio;

    public TarefaService(TarefaRepository repo, IRelogio relogio)
    {
        _repo    = repo;
        _relogio = relogio;
    }

    public async Task<IEnumerable<Tarefa>> ObterTodasAsync() => await _repo.GetAllAsync();

    public async Task<Tarefa?> ObterPorIdAsync(int id) => await _repo.GetByIdAsync(id);

    /// <summary>
    /// Procura por título e observações, indiferente a acento e caixa como a busca de notas:
    /// "reuniao" acha "Reunião". Sem termo, devolve a lista inteira na ordem padrão.
    ///
    /// Filtra em memória, e não no SQL, porque tarefas não têm a coluna <c>busca</c> que as
    /// notas têm — e o LIKE do SQLite só ignora maiúsculas para ASCII, então um LIKE aqui
    /// jamais acharia "Ação" procurando "acao". A lista de tarefas de uma pessoa cabe num
    /// Where sem custo perceptível; se um dia deixar de caber, o caminho é acrescentar a
    /// coluna normalizada no esquema — não espalhar LIKE que erra acento.
    /// </summary>
    public async Task<IEnumerable<Tarefa>> PesquisarAsync(string termo)
    {
        var todas = await _repo.GetAllAsync();

        var alvo = TextoNormalizado.Para(termo);
        if (string.IsNullOrWhiteSpace(alvo)) return todas;

        return todas.Where(t => TextoNormalizado.Para($"{t.Titulo} {t.Observacoes}")
            .Contains(alvo, StringComparison.Ordinal));
    }

    public async Task<(bool Ok, string Msg, Tarefa? Tarefa)> SalvarAsync(Tarefa tarefa)
    {
        if (string.IsNullOrWhiteSpace(tarefa.Titulo))
            return (false, "O título é obrigatório.", null);

        tarefa.Titulo = tarefa.Titulo.Trim();

        var anterior = tarefa.Id == 0 ? null : await _repo.GetByIdAsync(tarefa.Id);

        // Mexer no lembrete rearma a notificação: sem isso, adiar um lembrete já disparado
        // não avisaria de novo, e o usuário concluiria que o adiamento não funcionou.
        if (anterior != null && anterior.Lembrete != tarefa.Lembrete)
            tarefa.LembreteDisparado = false;

        // Notificação depois do prazo avisaria de uma tarefa já vencida — ver
        // Tarefa.LembreteDepoisDoPrazo. Recusa só o que está MUDANDO agora, e não qualquer
        // tarefa que já esteja nesse estado: adiar um lembrete que tocou empurra ele para
        // depois do prazo de propósito (AdiarLembreteAsync), e uma tarefa que chegou assim não
        // pode ficar impossível de gravar por causa de uma vírgula no título.
        var parNovo = anterior == null
                      || anterior.Prazo    != tarefa.Prazo
                      || anterior.Lembrete != tarefa.Lembrete;

        if (tarefa.LembreteDepoisDoPrazo && parNovo)
            return (false, "O lembrete não pode ser depois do prazo: ele avisaria de uma tarefa já vencida.", null);

        if (tarefa.Id == 0)
        {
            tarefa.Id = await _repo.InsertAsync(tarefa);
            return (true, "Tarefa criada!", tarefa);
        }

        await _repo.UpdateAsync(tarefa);
        return (true, "Tarefa atualizada!", tarefa);
    }

    /// <summary>
    /// Marca ou desmarca. Concluir uma tarefa RECORRENTE cria a próxima ocorrência aqui — é o
    /// único lugar onde isso acontece, então os três caminhos que concluem (a tela de Tarefas,
    /// o Início e o Enter da busca rápida) se comportam igual sem nenhum deles saber do assunto.
    ///
    /// A recorrência PASSA para a ocorrência nova, e a concluída fica no histórico como tarefa
    /// comum. Duas coisas saem de graça disso: reabrir uma tarefa já concluída não gera uma
    /// segunda cópia da mesma ocorrência (ela não é mais recorrente), e o histórico mostra o
    /// que foi feito de verdade, uma linha por vez que a tarefa foi cumprida.
    /// </summary>
    public async Task<(bool Ok, string Msg)> ConcluirAsync(int id, bool concluida)
    {
        if (!concluida)
        {
            await _repo.ToggleConcluidaAsync(id, false);
            return (true, "Tarefa reaberta.");
        }

        var tarefa  = await _repo.GetByIdAsync(id);
        var proxima = tarefa == null ? null : Recorrencia.ProximaOcorrencia(tarefa, _relogio.Agora);

        if (proxima == null)
        {
            await _repo.ToggleConcluidaAsync(id, true);
            return (true, "Tarefa concluída!");
        }

        await _repo.InsertAsync(proxima);

        // O bastão passou: a concluída deixa de ser recorrente para não gerar uma segunda
        // ocorrência se for reaberta e concluída de novo.
        //
        // Este UPDATE vem ANTES do toggle de propósito. O objeto foi lido antes de a tarefa
        // ser concluída, então gravá-lo depois devolveria concluida = 0 e apagaria a data de
        // conclusão que o toggle acabou de escrever. Deixando o toggle por último, quem tem a
        // última palavra sobre as duas colunas é a instrução que existe só para elas.
        tarefa!.Recorrencia = RecorrenciaTarefa.Nenhuma;
        await _repo.UpdateAsync(tarefa);
        await _repo.ToggleConcluidaAsync(id, true);

        var quando = proxima.Prazo ?? proxima.Lembrete;
        return (true, quando == null
            ? "Tarefa concluída! A próxima já está na lista."
            : $"Tarefa concluída! A próxima ficou para {quando:dd/MM}.");
    }

    /// <summary>
    /// Empurra o lembrete para daqui a pouco e rearma a notificação.
    ///
    /// Existe porque o balão da bandeja não tem botão: sem um jeito de adiar, um lembrete que
    /// chega em hora ruim está perdido — <c>lembrete_disparado</c> já foi gravado antes de o
    /// balão sair, e ele não volta sozinho.
    /// </summary>
    public async Task<(bool Ok, string Msg)> AdiarLembreteAsync(int id, TimeSpan quanto)
    {
        var tarefa = await _repo.GetByIdAsync(id);
        if (tarefa == null) return (false, "Tarefa não encontrada.");

        // A partir de AGORA, não do lembrete antigo: "adiar 15 minutos" às 14h30 de um
        // lembrete que era para as 9h significa 14h45, e não 9h15 — que já passou.
        tarefa.Lembrete          = _relogio.Agora.Add(quanto);
        tarefa.LembreteDisparado = false;
        await _repo.UpdateAsync(tarefa);

        return (true, $"Lembrete adiado para {tarefa.Lembrete:HH:mm}.");
    }

    /// <summary>Novo lembrete em dia e hora escolhidos, rearmando a notificação.</summary>
    public async Task<(bool Ok, string Msg)> ReagendarLembreteAsync(int id, DateTime quando)
    {
        var tarefa = await _repo.GetByIdAsync(id);
        if (tarefa == null) return (false, "Tarefa não encontrada.");

        tarefa.Lembrete          = quando;
        tarefa.LembreteDisparado = false;
        await _repo.UpdateAsync(tarefa);

        return (true, $"Lembrete adiado para {quando:dd/MM 'às' HH:mm}.");
    }

    public async Task<(bool Ok, string Msg)> ExcluirAsync(int id)
    {
        var tarefa = await _repo.GetByIdAsync(id);
        if (tarefa == null) return (false, "Tarefa não encontrada.");

        await _repo.DeleteAsync(id);
        return (true, $"Tarefa \"{tarefa.Titulo}\" excluída.");
    }

    /// <summary>Tarefas abertas com prazo para hoje ou já vencido — alimenta o Início.</summary>
    public async Task<IEnumerable<Tarefa>> ObterDeHojeEAtrasadasAsync()
    {
        var agora = _relogio.Agora;
        return (await _repo.GetAllAsync())
            .Where(t => t.ParaHoje(agora) || t.Atrasada(agora));
    }

    public async Task<IEnumerable<Tarefa>> ObterLembretesVencidosAsync(DateTime ate)
        => await _repo.GetLembretesVencidosAsync(ate);

    public async Task MarcarLembreteDisparadoAsync(int id)
        => await _repo.MarcarLembreteDisparadoAsync(id);
}
