using MacroHelper.Core;
using MacroHelper.Core.Entities;
using MacroHelper.Data.Repositories;
using System.Text.RegularExpressions;

namespace MacroHelper.Services;

public class VariavelGlobalService
{
    private readonly VariavelGlobalRepository _repo;
    private readonly MacroRepository? _macroRepo;
    private static readonly Regex _regex = new(@"\{(\w+)\}", RegexOptions.Compiled);

    public VariavelGlobalService(VariavelGlobalRepository repo, MacroRepository? macroRepo = null)
    {
        _repo = repo;
        _macroRepo = macroRepo;
    }

    public async Task<IEnumerable<VariavelGlobal>> ObterTodasAsync() => await _repo.GetAllAsync();

    /// <summary>Quantidade de macros cujo conteúdo referencia {nome} — para avisar antes de excluir.</summary>
    public async Task<int> ContarUsoAsync(string nome)
    {
        if (_macroRepo == null) return 0;
        var placeholder = "{" + nome + "}";
        var todos = await _macroRepo.GetAllAsync();
        return todos.Count(m => m.Conteudo.Contains(placeholder, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<(bool Ok, string Msg)> SalvarAsync(VariavelGlobal v)
    {
        if (string.IsNullOrWhiteSpace(v.Nome)) return (false, "Nome é obrigatório.");
        v.Nome = v.Nome.Trim().ToLowerInvariant();

        try
        {
            if (v.Id == 0) await _repo.InsertAsync(v);
            else           await _repo.UpdateAsync(v);
        }
        catch (RegistroDuplicadoException ex) { return (false, ex.Message); }

        return (true, "Variável global salva!");
    }

    public async Task ExcluirAsync(int id) => await _repo.DeleteAsync(id);

    /// <summary>
    /// Substitui placeholders {nome} no conteúdo pelos valores das variáveis globais cadastradas.
    /// Variáveis com valor em branco são IGNORADAS de propósito: uma global vazia mascararia o
    /// placeholder, apagando o texto em vez de deixar VariavelService perguntar o valor ao usuário.
    /// </summary>
    public async Task<string> ResolverAsync(string conteudo)
    {
        if (!_regex.IsMatch(conteudo)) return conteudo;
        var globais = (await _repo.GetAllAsync())
            .Where(g => !string.IsNullOrWhiteSpace(g.ValorPadrao))
            .ToDictionary(g => g.Nome, g => g.ValorPadrao, StringComparer.OrdinalIgnoreCase);
        if (globais.Count == 0) return conteudo;

        return _regex.Replace(conteudo, m =>
            globais.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
    }
}
