using MacroHelper.Core;
using MacroHelper.Core.Entities;
using MacroHelper.Data.Repositories;

namespace MacroHelper.Services;

public class CategoriaService
{
    private readonly CategoriaRepository _repo;
    private readonly MacroRepository? _macroRepo;

    public CategoriaService(CategoriaRepository repo, MacroRepository? macroRepo = null)
    {
        _repo = repo;
        _macroRepo = macroRepo;
    }

    public async Task<IEnumerable<Categoria>> ObterTodosAsync() => await _repo.GetAllAsync();
    public async Task<IEnumerable<Categoria>> ObterRaizAsync()   => await _repo.GetRaizAsync();

    public async Task<IEnumerable<Categoria>> ObterArvoreAsync()
    {
        var todos = (await _repo.GetAllAsync()).ToList();
        var raiz  = todos.Where(c => c.PaiId == null).ToList();
        foreach (var cat in raiz)
            cat.Subcategorias = todos.Where(c => c.PaiId == cat.Id).ToList();
        return raiz;
    }

    /// <summary>Quantidade de macros que usam a categoria — para avisar antes de excluir.</summary>
    public async Task<int> ContarUsoAsync(int categoriaId)
    {
        if (_macroRepo == null) return 0;
        var todos = await _macroRepo.GetAllAsync();
        return todos.Count(m => m.CategoriaId == categoriaId);
    }

    public async Task<(bool Ok, string Msg)> SalvarAsync(Categoria cat)
    {
        if (string.IsNullOrWhiteSpace(cat.Nome)) return (false, "Nome é obrigatório.");

        try
        {
            if (cat.Id == 0) cat.Id = await _repo.InsertAsync(cat);
            else             await _repo.UpdateAsync(cat);
        }
        catch (RegistroDuplicadoException ex) { return (false, ex.Message); }

        return (true, "Categoria salva!");
    }

    public async Task<(bool Ok, string Msg)> ExcluirAsync(int id)
    {
        await _repo.DeleteAsync(id);
        return (true, "Categoria excluída.");
    }
}
