namespace MacroHelper.Core.Entities;

/// <summary>Gravada como INTEGER 0/1/2 — ver o comentário em 002_tarefas_notas.sql.</summary>
public enum PrioridadeTarefa
{
    Baixa  = 0,
    Normal = 1,
    Alta   = 2,
}

/// <summary>
/// De quanto em quanto tempo a tarefa volta. Gravada como INTEGER — ver o comentário em
/// 003_clipboard_e_recorrencia.sql.
/// </summary>
public enum RecorrenciaTarefa
{
    Nenhuma   = 0,
    Diaria    = 1,
    DiasUteis = 2,
    Semanal   = 3,
    Quinzenal = 4,
    Mensal    = 5,
    Anual     = 6,
}

public static class RecorrenciaTarefaExtensoes
{
    /// <summary>
    /// Como a recorrência aparece na tela. Vazio para <see cref="RecorrenciaTarefa.Nenhuma"/>,
    /// porque o uso principal é uma etiqueta no cartão da tarefa — e "não repete" é o normal,
    /// que não merece etiqueta nenhuma. Quem precisa de rótulo para o caso vazio (a lista de
    /// opções do formulário) escreve o dele.
    ///
    /// Extensão sobre o enum, e não só uma propriedade em <see cref="Tarefa"/>: o ComboBox do
    /// formulário lista VALORES do enum, sem tarefa nenhuma por perto, e duas tabelas de
    /// tradução acabariam discordando.
    /// </summary>
    public static string Texto(this RecorrenciaTarefa recorrencia) => recorrencia switch
    {
        RecorrenciaTarefa.Diaria    => "Todo dia",
        RecorrenciaTarefa.DiasUteis => "Dias úteis",
        RecorrenciaTarefa.Semanal   => "Toda semana",
        RecorrenciaTarefa.Quinzenal => "A cada 15 dias",
        RecorrenciaTarefa.Mensal    => "Todo mês",
        RecorrenciaTarefa.Anual     => "Todo ano",
        _                           => string.Empty,
    };
}

public class Tarefa
{
    public int       Id          { get; set; }
    public string    Titulo      { get; set; } = string.Empty;
    public string?   Observacoes { get; set; }
    public bool      Concluida   { get; set; }
    public PrioridadeTarefa Prioridade { get; set; } = PrioridadeTarefa.Normal;

    /// <summary>Quando a tarefa vence. Informativo — não dispara nada sozinho.</summary>
    public DateTime? Prazo    { get; set; }

    /// <summary>Instante em que a notificação deve aparecer. Independente do prazo.</summary>
    public DateTime? Lembrete { get; set; }

    /// <summary>
    /// De quanto em quanto tempo esta tarefa volta. Concluir uma tarefa recorrente cria a
    /// PRÓXIMA ocorrência e passa a recorrência para ela — ver TarefaService.ConcluirAsync.
    /// </summary>
    public RecorrenciaTarefa Recorrencia { get; set; } = RecorrenciaTarefa.Nenhuma;

    public bool      LembreteDisparado { get; set; }
    public DateTime  DataCriacao       { get; set; } = DateTime.Now;
    public DateTime? DataConclusao     { get; set; }

    // ── Estado derivado, usado pela tela ─────────────────────────────────────

    public bool TemLembretePendente => Lembrete.HasValue && !LembreteDisparado && !Concluida;

    /// <summary>
    /// Notificação marcada para depois do dia em que a tarefa vence. Não faz sentido: o aviso
    /// chegaria para cobrar uma tarefa cujo prazo já passou.
    ///
    /// Compara o DIA, e não o instante, porque o prazo não tem hora — ele fica em 00:00, e um
    /// comparativo direto recusaria "vence hoje, me avise hoje ao meio-dia", que é o uso mais
    /// comum dos dois campos juntos.
    /// </summary>
    public bool LembreteDepoisDoPrazo =>
        Prazo.HasValue && Lembrete.HasValue && Lembrete.Value.Date > Prazo.Value.Date;

    public bool Recorrente => Recorrencia != RecorrenciaTarefa.Nenhuma;

    /// <summary>Como a recorrência aparece no cartão da tarefa. Vazio quando não há nenhuma.</summary>
    public string RecorrenciaTexto => Recorrencia.Texto();

    public bool Atrasada(DateTime agora) => !Concluida && Prazo.HasValue && Prazo.Value.Date < agora.Date;

    public bool ParaHoje(DateTime agora) => !Concluida && Prazo.HasValue && Prazo.Value.Date == agora.Date;
}
