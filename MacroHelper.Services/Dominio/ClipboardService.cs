using MacroHelper.Core.Entities;
using MacroHelper.Data.Repositories;

namespace MacroHelper.Services;

/// <summary>
/// O histórico da área de transferência: o que entra, o que fica e o que é descartado.
///
/// A captura em si mora na UI (<c>ClipboardMonitorService</c>), que é quem tem uma janela para
/// receber a mensagem do Windows. Aqui ficam as regras que valem independentemente de como o
/// texto chegou — e que por isso dá para verificar sem clipboard nenhum.
/// </summary>
public class ClipboardService
{
    private readonly ClipboardRepository _repo;

    public ClipboardService(ClipboardRepository repo) => _repo = repo;

    /// <summary>
    /// Quantos itens não fixados ficam guardados.
    ///
    /// O Win+V do Windows guarda 25. Aqui são 50 porque a lista é buscável: o custo de ter
    /// mais é uma linha a mais no LIKE, e o benefício é alcançar a cópia de ontem de manhã.
    /// </summary>
    public const int LimiteHistorico = 50;

    /// <summary>
    /// Acima disto o texto não entra no histórico.
    ///
    /// Não é medo de espaço — são 200 KB por item no pior caso. É que copiar um arquivo
    /// inteiro para colar em outro lugar é uma operação de passagem, e guardá-la só empurraria
    /// para fora do limite as dez cópias curtas que a pessoa realmente quer de volta.
    /// </summary>
    public const int TamanhoMaximo = 100_000;

    public async Task<IEnumerable<ItemClipboard>> ObterTodosAsync() => await _repo.GetAllAsync();

    public async Task<IEnumerable<ItemClipboard>> PesquisarAsync(string termo) =>
        string.IsNullOrWhiteSpace(termo)
            ? await _repo.GetAllAsync()
            : await _repo.SearchAsync(termo.Trim());

    /// <summary>
    /// Registra uma cópia, se ela merecer entrar.
    ///
    /// Espaço em branco puro não entra: selecionar sem querer e apertar Ctrl+C é comum, e uma
    /// linha vazia no histórico não tem para que servir. O corte por tamanho está acima.
    /// </summary>
    /// <returns>O item gravado, ou null quando o texto foi descartado.</returns>
    public async Task<ItemClipboard?> RegistrarAsync(string? conteudo, string? origem = null)
    {
        if (string.IsNullOrWhiteSpace(conteudo)) return null;
        if (conteudo.Length > TamanhoMaximo)    return null;

        var item = await _repo.RegistrarAsync(conteudo, string.IsNullOrWhiteSpace(origem) ? null : origem);

        // Poda depois de gravar, não antes: assim o limite conta o item que acabou de entrar,
        // e a lista nunca passa de LimiteHistorico nem por um instante.
        await _repo.PodarAsync(LimiteHistorico);
        return item;
    }

    public async Task<(bool Ok, string Msg)> FixarAsync(int id, bool fixado)
    {
        await _repo.ToggleFixadoAsync(id, fixado);
        return (true, fixado ? "Fixado no topo." : "Desafixado.");
    }

    public async Task<(bool Ok, string Msg)> ExcluirAsync(int id)
    {
        await _repo.DeleteAsync(id);
        return (true, "Item removido do histórico.");
    }

    /// <summary>Esvazia o histórico, preservando o que está fixado.</summary>
    public async Task<(bool Ok, string Msg)> LimparAsync()
    {
        var removidos = await _repo.LimparNaoFixadosAsync();
        return (true, removidos == 0
            ? "O histórico já estava vazio."
            : $"{removidos} item(ns) removido(s). Os fixados continuam aqui.");
    }
}
