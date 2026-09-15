using MacroHelper.Core.Entities;

namespace MacroHelper.Core.Agenda;

/// <summary>
/// Um compromisso já colocado na grade de um dia: onde ele começa, quanto ocupa, e em qual das
/// faixas paralelas ele coube.
/// </summary>
public sealed class BlocoDaGrade
{
    public required Compromisso Compromisso { get; init; }

    /// <summary>Minutos desde a meia-noite do dia.</summary>
    public required int InicioMinutos { get; init; }

    /// <summary>Quanto ele ocupa na grade — nunca zero, ver <see cref="GradeDeAgenda.DuracaoMinima"/>.</summary>
    public required int DuracaoMinutos { get; init; }

    /// <summary>A faixa vertical, contada de zero, dentro do grupo de compromissos que se cruzam.</summary>
    public required int Coluna { get; init; }

    /// <summary>Quantas faixas o grupo inteiro precisou. É o divisor da largura do dia.</summary>
    public required int TotalColunas { get; init; }

    /// <summary>Se ele divide horário com outro compromisso marcado. É o choque, agora visível.</summary>
    public required bool Choca { get; init; }

    public int FimMinutos => InicioMinutos + DuracaoMinutos;
}

/// <summary>
/// Onde cada compromisso de um dia é desenhado na grade da semana.
///
/// A conta que importa é a das COLUNAS PARALELAS: dois compromissos que se cruzam não podem
/// ocupar o mesmo espaço, ou um esconde o outro e o choque, que é justamente o que a grade
/// existe para mostrar, fica invisível. Eles são postos lado a lado, cada um com metade da
/// largura do dia.
///
/// Isto mora no Core, longe do WPF, porque é aritmética de agenda e não desenho: a tela só
/// multiplica estes números por pixels.
/// </summary>
public static class GradeDeAgenda
{
    /// <summary>
    /// Quanto ocupa na grade um compromisso sem duração.
    ///
    /// É o mesmo mínimo que <see cref="Compromisso.ChocaCom"/> usa, e tem de continuar sendo:
    /// se a grade e o aviso de choque usassem números diferentes, dois compromissos apareceriam
    /// encavalados sem aviso, ou com aviso e sem encavalar.
    /// </summary>
    public const int DuracaoMinima = 15;

    private const int MinutosDoDia = 24 * 60;

    /// <summary>
    /// Monta a grade de UM dia. O que entra é o que tem data; cancelado fica de fora, porque
    /// cancelado não ocupa mais lugar nenhum na agenda.
    /// </summary>
    public static IReadOnlyList<BlocoDaGrade> Montar(IEnumerable<Compromisso> doDia)
    {
        var itens = doDia
            .Where(c => c.TemData && !c.Cancelado)
            .OrderBy(c => c.Quando!.Value)
            .ThenByDescending(c => c.DuracaoMinutos ?? DuracaoMinima)
            .ToList();

        var blocos = new List<BlocoDaGrade>(itens.Count);

        foreach (var grupo in Agrupar(itens))
        {
            // Cada coluna guarda o minuto em que ela ficou livre. O compromisso entra na
            // primeira que já vagou; se nenhuma vagou, abre-se mais uma.
            var colunas = new List<int>();
            var escolha = new List<(Compromisso Item, int Coluna)>();

            foreach (var item in grupo)
            {
                var inicio = Inicio(item);
                var coluna = colunas.FindIndex(livreEm => livreEm <= inicio);

                if (coluna < 0)
                {
                    colunas.Add(Fim(item));
                    coluna = colunas.Count - 1;
                }
                else
                {
                    colunas[coluna] = Fim(item);
                }

                escolha.Add((item, coluna));
            }

            foreach (var (item, coluna) in escolha)
                blocos.Add(new BlocoDaGrade
                {
                    Compromisso    = item,
                    InicioMinutos  = Inicio(item),
                    DuracaoMinutos = Fim(item) - Inicio(item),
                    Coluna         = coluna,
                    TotalColunas   = colunas.Count,
                    Choca          = item.Agendado && grupo.Any(o => !ReferenceEquals(o, item) && o.Agendado && item.ChocaCom(o, DuracaoMinima)),
                });
        }

        return blocos;
    }

    /// <summary>Se o dia tem pelo menos dois compromissos marcados dividindo o mesmo horário.</summary>
    public static bool TemChoque(IEnumerable<Compromisso> doDia)
    {
        var marcados = doDia.Where(c => c.TemData && c.Agendado).ToList();

        return marcados.Any(c => marcados.Any(o => !ReferenceEquals(o, c) && c.ChocaCom(o, DuracaoMinima)));
    }

    /// <summary>
    /// Junta em grupos os compromissos que se encostam, direta ou indiretamente.
    ///
    /// O grupo é transitivo de propósito: se A cruza com B e B cruza com C, os três disputam a
    /// largura do dia mesmo que A e C não se toquem. Sem isso, A e C ficariam largos e B
    /// espremido em cima dos dois.
    /// </summary>
    private static IEnumerable<List<Compromisso>> Agrupar(List<Compromisso> ordenados)
    {
        var grupo = new List<Compromisso>();
        var fimDoGrupo = int.MinValue;

        foreach (var item in ordenados)
        {
            if (grupo.Count > 0 && Inicio(item) >= fimDoGrupo)
            {
                yield return grupo;
                grupo = [];
            }

            grupo.Add(item);
            fimDoGrupo = Math.Max(fimDoGrupo, Fim(item));
        }

        if (grupo.Count > 0) yield return grupo;
    }

    private static int Inicio(Compromisso c) =>
        (int)c.Quando!.Value.TimeOfDay.TotalMinutes;

    /// <summary>
    /// O fim dentro do dia. O que passa da meia-noite é cortado ali: a grade é de um dia, e
    /// deixar o bloco vazar desenharia por cima do cabeçalho do dia seguinte.
    /// </summary>
    private static int Fim(Compromisso c) =>
        Math.Min(MinutosDoDia, Inicio(c) + Math.Max(DuracaoMinima, c.DuracaoMinutos ?? 0));
}
