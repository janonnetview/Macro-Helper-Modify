using System.Globalization;

namespace MacroHelper.Core.Entities;

/// <summary>Gravada como INTEGER 0/1/2 — ver o comentário em 004_compromissos.sql.</summary>
public enum SituacaoCompromisso
{
    Agendado  = 0,
    Realizado = 1,
    Cancelado = 2,
}

/// <summary>
/// Quanto tempo ANTES o aviso sai. Tudo em minutos, que é como a coluna guarda.
///
/// Aqui moram as opções que a tela oferece e o texto de cada uma. Fica em Core, e não no
/// ViewModel, porque o mesmo número precisa virar texto em três lugares diferentes — o cartão
/// da agenda, o formulário e o balão da bandeja — e três tabelas de tradução acabariam
/// discordando entre si.
/// </summary>
public static class Antecedencia
{
    public const int MeiaHora = 30;
    public const int UmDia    = 1440;

    /// <summary>
    /// O que já vem marcado num compromisso novo: véspera e meia hora antes.
    ///
    /// Não é enfeite. O compromisso que motivou esta tela — a visita do técnico na quinta —
    /// precisa dos dois: a véspera é quando ainda dá para reorganizar o dia, e os 30 minutos
    /// são quando ainda dá para estar pronto. Um padrão em "nenhum aviso" transformaria a
    /// agenda numa lista bonita que não avisa nada.
    /// </summary>
    public const int PadraoAntecipado = UmDia;
    public const int PadraoNaHora     = MeiaHora;

    /// <summary>"na hora", "30 min antes", "1 dia antes".</summary>
    public static string Texto(int minutos) => minutos switch
    {
        <= 0                          => "na hora",
        < 60                          => $"{minutos} min antes",
        < 1440 when minutos % 60 == 0 => $"{Quantidade(minutos / 60, "hora", "horas")} antes",
        < 1440                        => $"{minutos / 60}h{minutos % 60:00} antes",
        10080                         => "1 semana antes",
        _ when minutos % 1440 == 0    => $"{Quantidade(minutos / 1440, "dia", "dias")} antes",
        _                             => $"{Quantidade(minutos / 60, "hora", "horas")} antes",
    };

    private static string Quantidade(int n, string singular, string plural) =>
        $"{n} {(n == 1 ? singular : plural)}";
}

/// <summary>
/// Um aviso que acabou de vencer: o compromisso e QUAL dos dois disparou.
///
/// A UI precisa dos dois dados para escrever o balão — "amanhã às 14:00" e "em 30 min" são o
/// mesmo compromisso em avisos diferentes, e só o segundo pede que se levante da mesa.
/// </summary>
/// <param name="Antecipado">true para o aviso de folga; false para o de cima da hora.</param>
public record AvisoVencido(Compromisso Compromisso, bool Antecipado);

/// <summary>
/// Um compromisso: dia e hora marcados, com aviso antes.
///
/// NA TELA ISTO SE CHAMA "LEMBRETE". No código continua Compromisso, e a divergência é
/// deliberada: `Lembrete` já é outra coisa aqui dentro — é a coluna <c>lembrete</c> de
/// <see cref="Tarefa"/>, o <c>LembreteService</c> e o <c>lembrete_disparado</c>. Dois tipos
/// chamados Lembrete significando coisas diferentes seria a ambiguidade perigosa, a que troca
/// um pelo outro numa assinatura de método sem o compilador reclamar; nome de tela diferente do
/// nome do tipo é a ambiguidade inofensiva, que se resolve lendo este parágrafo.
///
/// A diferença para <see cref="Tarefa"/> não é de campo, é de natureza: está explicada por
/// inteiro em 004_compromissos.sql. Aqui nada se conclui, e a pergunta que toda propriedade
/// derivada responde é "quanto falta".
///
/// A data pode faltar (ver <see cref="Quando"/>), e aí todas as respostas derivadas mudam
/// de tom: nada venceu, nada choca, nada avisa. Ver 005_compromisso_sem_data.sql.
/// </summary>
public class Compromisso
{
    public int     Id          { get; set; }
    public string  Titulo      { get; set; } = string.Empty;
    public string? Local       { get; set; }
    public string? Observacoes { get; set; }

    /// <summary>
    /// O instante marcado, ou nulo enquanto ele ainda não foi definido.
    ///
    /// Nulo é o compromisso REMARCADO: "a visita do técnico saiu de quinta e ainda não tem
    /// data nova". A alternativa seria inventar uma data só para conseguir gravar, e data
    /// inventada dispara aviso na hora errada.
    ///
    /// Sem data nenhum aviso existe: não há de onde descontar a antecedência. Quem garante
    /// isso ao gravar é o CompromissoService.
    /// </summary>
    public DateTime? Quando { get; set; }

    /// <summary>Quanto dura, quando faz sentido dizer. Nulo é o normal em "entregar o documento".</summary>
    public int? DuracaoMinutos { get; set; }

    public SituacaoCompromisso Situacao { get; set; } = SituacaoCompromisso.Agendado;

    /// <summary>Minutos antes de <see cref="Quando"/> para o aviso de folga. Nulo = sem este aviso.</summary>
    public int? AvisoAntecipadoMinutos   { get; set; }
    public bool AvisoAntecipadoDisparado { get; set; }

    /// <summary>Minutos antes de <see cref="Quando"/> para o aviso de cima da hora. Nulo = sem este aviso.</summary>
    public int? AvisoNaHoraMinutos   { get; set; }
    public bool AvisoNaHoraDisparado { get; set; }

    public DateTime DataCriacao { get; set; } = DateTime.Now;

    // ── Estado derivado, usado pela tela e pela varredura ────────────────────

    /// <summary>Tem dia e hora. É o que separa a agenda do que ainda está por marcar.</summary>
    public bool TemData => Quando.HasValue;

    public bool Agendado  => Situacao == SituacaoCompromisso.Agendado;
    public bool Realizado => Situacao == SituacaoCompromisso.Realizado;
    public bool Cancelado => Situacao == SituacaoCompromisso.Cancelado;

    /// <summary>Quando termina. Nulo quando não há duração — que não é o mesmo que durar zero.</summary>
    public DateTime? Fim =>
        Quando is { } inicio && DuracaoMinutos is > 0 ? inicio.AddMinutes(DuracaoMinutos.Value) : null;

    /// <summary>O instante em que cada aviso deve sair. Nulo quando aquele aviso não existe.</summary>
    public DateTime? AvisoAntecipadoEm =>
        Quando is { } q && AvisoAntecipadoMinutos.HasValue ? q.AddMinutes(-AvisoAntecipadoMinutos.Value) : null;

    public DateTime? AvisoNaHoraEm =>
        Quando is { } q && AvisoNaHoraMinutos.HasValue ? q.AddMinutes(-AvisoNaHoraMinutos.Value) : null;

    /// <summary>Só a hora de início. É o que cabe na etiqueta de data do cartão.</summary>
    public string HoraTexto => Quando is { } q ? q.ToString("HH:mm") : "Sem data";

    /// <summary>"14:00", ou "14:00 às 15:00" quando há duração.</summary>
    public string HorarioTexto => Quando switch
    {
        null                      => "sem data marcada",
        { } q when Fim is { } fim => $"{q:HH:mm} às {fim:HH:mm}",
        { } q                     => q.ToString("HH:mm"),
    };

    /// <summary>"Qui": o dia da semana abreviado, que é como se localiza um compromisso na
    /// lista. Vazio enquanto não há data.</summary>
    public string DiaDaSemanaTexto
    {
        get
        {
            if (Quando is not { } q) return string.Empty;

            var ptBr = CultureInfo.GetCultureInfo("pt-BR");
            var dia  = ptBr.DateTimeFormat.GetAbbreviatedDayName(q.DayOfWeek).TrimEnd('.');
            return char.ToUpper(dia[0], ptBr) + dia[1..];
        }
    }

    /// <summary>
    /// A promessa do cartão: "Avisa 1 dia antes e 30 min antes".
    ///
    /// Fica visível na lista porque promessa de notificação precisa ser conferível sem abrir o
    /// formulário. Um compromisso que não avisa nada diz "Sem aviso" em voz alta — é a única
    /// forma de a pessoa descobrir isso antes da hora, e não depois.
    /// </summary>
    public string AvisosTexto => !TemData ? "Data ainda por marcar" : (AvisoAntecipadoMinutos, AvisoNaHoraMinutos) switch
    {
        (null,  null)  => "Sem aviso",
        ({ } a, null)  => $"Avisa {Antecedencia.Texto(a)}",
        (null,  { } h) => $"Avisa {Antecedencia.Texto(h)}",
        ({ } a, { } h) => $"Avisa {Antecedencia.Texto(a)} e {Antecedencia.Texto(h)}",
    };

    public bool ParaHoje(DateTime agora) => Quando?.Date == agora.Date;

    /// <summary>
    /// Já passou da hora, e do fim quando há duração.
    ///
    /// O que não tem data nunca passou: ele está esperando ser marcado, e mandá-lo para o
    /// histórico enterraria justamente o que precisa de decisão.
    /// </summary>
    public bool Passou(DateTime agora) => Quando is { } q && (Fim ?? q) < agora;

    /// <summary>
    /// Se os dois ocupam o mesmo pedaço da agenda.
    ///
    /// Um compromisso sem duração ocupa <paramref name="minimo"/> minutos aqui, e só aqui:
    /// dois "entregar o documento" marcados para as 10h são um choque de verdade, e com
    /// duração zero nenhum intervalo se cruzaria com nada.
    /// </summary>
    public bool ChocaCom(Compromisso outro, int minimo = 15)
    {
        // Sem data não se ocupa pedaço nenhum da agenda: dois lembretes por marcar não batem
        // entre si, e nenhum deles bate com o que tem hora.
        if (Quando is not { } meuInicio || outro.Quando is not { } outroInicio) return false;

        var meuFim   = Fim       ?? meuInicio.AddMinutes(minimo);
        var outroFim = outro.Fim ?? outroInicio.AddMinutes(minimo);
        return meuInicio < outroFim && outroInicio < meuFim;
    }

    /// <summary>
    /// Quanto falta, em português de balão de notificação: "agora", "em 25 min", "hoje às
    /// 14:00", "amanhã às 14:00", "Qui 20/08 às 14:00".
    ///
    /// O "agora" entra por parâmetro pela mesma razão de <c>IRelogio</c> existir: assim dá para
    /// afirmar sobre o texto do aviso de véspera sem esperar até a véspera.
    /// </summary>
    public string ContagemTexto(DateTime agora)
    {
        if (Quando is not { } quando) return "sem data marcada";

        var falta = quando - agora;

        if (falta <= TimeSpan.Zero)           return "agora";
        if (falta < TimeSpan.FromMinutes(1))  return "em menos de 1 min";
        if (falta < TimeSpan.FromHours(1))    return $"em {(int)falta.TotalMinutes} min";

        var dias = (quando.Date - agora.Date).Days;

        return dias switch
        {
            0 => $"hoje às {quando:HH:mm}",
            1 => $"amanhã às {quando:HH:mm}",
            _ => $"{DiaDaSemanaTexto} {quando:dd/MM} às {quando:HH:mm}",
        };
    }
}
