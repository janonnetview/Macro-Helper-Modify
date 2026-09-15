using MacroHelper.Core.Entities;
using MacroHelper.Core.Texto;
using MacroHelper.Data.Repositories;

namespace MacroHelper.Services;

public class NotaService
{
    private readonly NotaRepository _repo;
    public NotaService(NotaRepository repo) => _repo = repo;

    public async Task<IEnumerable<Nota>> ObterTodasAsync() => await _repo.GetAllAsync();

    public async Task<IEnumerable<Nota>> PesquisarAsync(string termo) =>
        string.IsNullOrWhiteSpace(termo)
            ? await _repo.GetAllAsync()
            : await _repo.SearchAsync(termo.Trim());

    public async Task<(bool Ok, string Msg, Nota? Nota)> SalvarAsync(Nota nota)
    {
        // O título pode ser deduzido do conteúdo: numa tela de anotações, obrigar a preencher
        // dois campos para guardar uma linha de texto atrapalha mais do que ajuda.
        if (string.IsNullOrWhiteSpace(nota.Titulo) && string.IsNullOrWhiteSpace(nota.Conteudo))
            return (false, "Escreva um título ou algum conteúdo.", null);

        nota.Titulo = string.IsNullOrWhiteSpace(nota.Titulo)
            ? PrimeiraLinha(nota.Conteudo)
            : nota.Titulo.Trim();

        if (nota.Id == 0)
        {
            nota.Id = await _repo.InsertAsync(nota);
            return (true, "Nota criada!", nota);
        }

        await _repo.UpdateAsync(nota);
        return (true, "Nota salva!", nota);
    }

    public async Task<(bool Ok, string Msg)> FixarAsync(int id, bool fixada)
    {
        await _repo.ToggleFixadaAsync(id, fixada);
        return (true, fixada ? "Nota fixada no topo." : "Nota desafixada.");
    }

    public async Task<(bool Ok, string Msg)> ExcluirAsync(int id)
    {
        await _repo.DeleteAsync(id);
        return (true, "Nota excluída.");
    }

    /// <summary>
    /// A primeira linha com alguma coisa escrita, já sem marcação: o título deduzido de
    /// "# Reunião de quarta" é "Reunião de quarta", e não o cerquilha junto.
    /// </summary>
    private static string PrimeiraLinha(string conteudo)
    {
        var linha = conteudo.Split('\n')
                        .Select(l => Markdown.ParaTextoSimples(l).Trim())
                        .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))
                    ?? "Sem título";
        return linha.Length <= 60 ? linha : linha[..60] + "…";
    }
}
