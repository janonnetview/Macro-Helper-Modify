using MacroHelper.Core.Agenda;
using MacroHelper.Core.Entities;
using System.Globalization;

namespace MacroHelper.UI.ViewModels;

/// <summary>Uma faixa de hora do fundo da grade e da régua da esquerda: "08h".</summary>
public sealed class HoraDaGrade
{
    public required int    Hora   { get; init; }
    public required double Altura { get; init; }

    public string Texto => $"{Hora:00}h";
}

/// <summary>
/// Uma meia hora vazia de um dia, na grade da semana: o lugar onde se clica para marcar
/// alguma coisa naquele horário.
///
/// Ela existe como ITEM, e não como conta de pixels no clique, porque assim quem sabe que
/// "aquele pedaço da tela são 14h30 da quinta" é o ViewModel, e não um cálculo de coordenada
/// no code-behind. O horário exato viaja no comando, e dá para afirmar sobre ele num teste.
/// </summary>
public sealed class FaixaDeHora
{
    /// <summary>O dia e a hora que um clique aqui marca.</summary>
    public required DateTime Inicio { get; init; }

    public required double Altura { get; init; }

    /// <summary>Se é a faixa cheia (:00). É ela que leva a linha da hora.</summary>
    public required bool InicioDeHora { get; init; }

    public string Texto => $"Novo lembrete às {Inicio:HH:mm}";
}

/// <summary>
/// Um dia desenhado no calendário. Serve às duas grades, e por isso tem duas listas.
///
/// Na SEMANA o que importa são os <see cref="Blocos"/>: cada compromisso com a altura da
/// duração dele e a faixa que a <see cref="GradeDeAgenda"/> reservou. No MÊS não cabe hora
/// nenhuma na célula, e o dia vira uma pilha de etiquetas (<see cref="Itens"/>) com o resto
/// contado em <see cref="Extras"/>.
///
/// É um tipo só porque as duas grades respondem à mesma pergunta ("o que tem neste dia") e
/// compartilham quase tudo: o número do dia, se é hoje, e se ali existe choque de horário.
/// </summary>
public sealed class DiaDaAgenda
{
    /// <summary>Quantas etiquetas cabem numa célula do mês antes de virar "+N".</summary>
    public const int EtiquetasNoMes = 3;

    public required DateTime Data { get; init; }

    /// <summary>Se é o dia de hoje. É o único destaque permanente da grade.</summary>
    public required bool EhHoje { get; init; }

    /// <summary>
    /// No mês, se o dia é da "sobra" — os dias do mês vizinho que completam a primeira e a
    /// última semana. Eles aparecem apagados: estão ali para a semana ficar inteira.
    /// </summary>
    public bool ForaDoMes { get; init; }

    public IReadOnlyList<BlocoDaGrade> Blocos { get; init; } = [];
    public IReadOnlyList<Compromisso>  Itens  { get; init; } = [];

    /// <summary>
    /// As meias horas vazias do dia, de cima a baixo. São o fundo da coluna na grade da
    /// semana, e cada uma é clicável.
    /// </summary>
    public IReadOnlyList<FaixaDeHora> Faixas { get; init; } = [];

    /// <summary>Quantos compromissos não couberam na célula do mês.</summary>
    public int Extras { get; init; }

    public bool   TemExtras   { get; init; }
    public string ExtrasTexto => $"+{Extras}";

    /// <summary>Se dois compromissos marcados dividem horário neste dia.</summary>
    public required bool TemChoque { get; init; }

    /// <summary>A grade da semana precisa dos três em cada dia: eles vão para o painel.</summary>
    public required int    PrimeiraHora { get; init; }
    public required int    UltimaHora   { get; init; }
    public required double AlturaDaHora { get; init; }

    public string NumeroTexto => Data.Day.ToString(CultureInfo.InvariantCulture);

    /// <summary>"Seg", "Ter"... com a inicial maiúscula, que é como o português escreve.</summary>
    public string DiaDaSemanaTexto
    {
        get
        {
            var ptBr = CultureInfo.GetCultureInfo("pt-BR");
            var dia  = ptBr.DateTimeFormat.GetAbbreviatedDayName(Data.DayOfWeek).TrimEnd('.');
            return char.ToUpper(dia[0], ptBr) + dia[1..];
        }
    }
}
