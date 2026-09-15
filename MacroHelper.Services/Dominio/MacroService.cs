using MacroHelper.Core;
using MacroHelper.Core.Entities;
using MacroHelper.Data.Repositories;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MacroHelper.Services;

public class MacroService
{
    private readonly MacroRepository        _repository;
    private readonly MacroVersaoRepository? _versaoRepo;
    private readonly VariavelGlobalService? _variavelGlobalService;
    private static readonly Regex _macroRefRegex = new(@"\{macro:([\w-]+)\}", RegexOptions.Compiled);

    public MacroService(MacroRepository repository,
        MacroVersaoRepository? versaoRepo = null,
        VariavelGlobalService? variavelGlobalService = null)
    {
        _repository            = repository;
        _versaoRepo            = versaoRepo;
        _variavelGlobalService = variavelGlobalService;
    }

    public async Task<IEnumerable<Macro>> ObterTodosAsync() =>
        await _repository.GetAllAsync();

    public async Task<IEnumerable<Macro>> PesquisarAsync(string termo) =>
        string.IsNullOrWhiteSpace(termo)
            ? await _repository.GetAllAsync()
            : await _repository.SearchAsync(termo.Trim());

    public async Task<IEnumerable<Macro>> ObterPorCategoriaAsync(string categoria) =>
        await _repository.GetByCategoriaAsync(categoria);

    public async Task<Macro?> ObterPorIdAsync(int id) =>
        await _repository.GetByIdAsync(id);

    public async Task<IEnumerable<string>> ObterCategoriasAsync() =>
        await _repository.GetCategoriasAsync();

    public async Task<Macro?> ObterPorAtalhoTeclaAsync(string digito) =>
        await _repository.GetByAtalhoTeclaAsync(digito);

    public async Task ToggleFavoritoAsync(Macro macro)
    {
        macro.Favorito = !macro.Favorito;
        await _repository.ToggleFavoritoAsync(macro.Id, macro.Favorito);
    }

    /// <summary>Procura uma macro existente com conteúdo muito parecido (Jaccard sobre palavras), para alertar antes de salvar.</summary>
    public async Task<Macro?> DetectarPossivelDuplicataAsync(Macro macro)
    {
        const double LIMIAR = 0.75;
        var todas = await _repository.GetAllAsync();
        return todas
            .Where(m => m.Id != macro.Id)
            .Select(m => (Macro: m, Score: SimilaridadeJaccard(macro.Conteudo, m.Conteudo)))
            .Where(t => t.Score >= LIMIAR)
            .OrderByDescending(t => t.Score)
            .Select(t => t.Macro)
            .FirstOrDefault();
    }

    private static double SimilaridadeJaccard(string a, string b)
    {
        var separadores = new[] { ' ', '\n', '\r', '\t', '.', ',', ';' };
        var wa = a.ToLowerInvariant().Split(separadores, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var wb = b.ToLowerInvariant().Split(separadores, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        if (wa.Count == 0 || wb.Count == 0) return 0;
        var intersecao = wa.Intersect(wb).Count();
        var uniao      = wa.Union(wb).Count();
        return uniao == 0 ? 0 : (double)intersecao / uniao;
    }

    public async Task<(bool Sucesso, string Mensagem, Macro? Macro)> SalvarAsync(Macro macro)
    {
        if (string.IsNullOrWhiteSpace(macro.Atalho))
            return (false, "O atalho é obrigatório.", null);

        if (string.IsNullOrWhiteSpace(macro.Titulo))
            return (false, "O título é obrigatório.", null);

        if (string.IsNullOrWhiteSpace(macro.Conteudo))
            return (false, "O conteúdo é obrigatório.", null);

        macro.Atalho = macro.Atalho.Trim().ToLower().Replace(" ", "-");

        // Atalho repetido é engano comum, não falha do app: o índice único do banco recusa a
        // gravação e aqui isso vira uma mensagem no formulário, no mesmo formato das outras
        // validações acima.
        try
        {
            if (macro.Id == 0)
            {
                macro.Id = await _repository.InsertAsync(macro);
                return (true, "Macro criada com sucesso!", macro);
            }

            var anterior = await _repository.GetByIdAsync(macro.Id);
            if (anterior != null && _versaoRepo != null)
                await _versaoRepo.RegistrarAsync(macro.Id, anterior.Titulo, anterior.Conteudo);

            await _repository.UpdateAsync(macro);
            return (true, "Macro atualizada com sucesso!", macro);
        }
        catch (RegistroDuplicadoException ex)
        {
            return (false, ex.Message, null);
        }
    }

    /// <summary>
    /// Cria uma cópia editável de uma macro existente.
    ///
    /// Sem isto, partir de um modelo de 800 caracteres para fazer a variação dele é copiar e
    /// colar na mão — e o passo que se esquece é sempre o mesmo: trocar o atalho, que é único
    /// e recusaria a gravação lá no fim.
    ///
    /// Três campos NÃO vêm junto, e cada um por um motivo:
    ///
    ///   • o atalho ganha sufixo, porque é único no banco;
    ///   • o atalho de tecla fica vazio, porque Ctrl+Alt+3 precisa resolver para exatamente
    ///     uma macro — copiá-lo faria a gravação estourar no índice parcial;
    ///   • favorito volta a false: favoritar é uma decisão sobre a macro que se usa, e a cópia
    ///     ainda vai ser editada.
    ///
    /// O histórico de versões também não é copiado: ele conta o que aconteceu com a OUTRA
    /// macro, e a nova começa do zero.
    /// </summary>
    public async Task<(bool Sucesso, string Mensagem, Macro? Macro)> DuplicarAsync(int id)
    {
        // Pelo id, e não pelo objeto da lista: as listagens não trazem imagem_base64, e
        // duplicar a partir de uma delas produziria uma cópia sem a imagem, em silêncio.
        var original = await _repository.GetByIdAsync(id);
        if (original == null) return (false, "Macro não encontrada.", null);

        var atalhosEmUso = (await _repository.GetAllAsync())
            .Select(m => m.Atalho)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var copia = new Macro
        {
            Atalho       = AtalhoLivre(original.Atalho, atalhosEmUso),
            Titulo       = $"{original.Titulo} (cópia)",
            Conteudo     = original.Conteudo,
            Categoria    = original.Categoria,
            CategoriaId  = original.CategoriaId,
            ImagemBase64 = original.ImagemBase64,
            Ativo        = true,
            Favorito     = false,
            AtalhoTecla  = null,
        };

        try
        {
            copia.Id = await _repository.InsertAsync(copia);
            return (true, $"Cópia criada como \"{copia.Atalho}\".", copia);
        }
        catch (RegistroDuplicadoException ex)
        {
            return (false, ex.Message, null);
        }
    }

    /// <summary>
    /// "/ola" vira "/ola-copia"; se já existir, "/ola-copia-2", e assim por diante.
    ///
    /// O limite não é decoração: sem ele, um estado inesperado do banco viraria laço infinito
    /// dentro de um clique de botão. Passando de 99 o índice único do banco recusa a gravação,
    /// que é uma mensagem de erro — e não uma janela travada.
    /// </summary>
    private static string AtalhoLivre(string original, ISet<string> emUso)
    {
        var candidato = $"{original}-copia";
        if (!emUso.Contains(candidato)) return candidato;

        for (var n = 2; n < 100; n++)
        {
            candidato = $"{original}-copia-{n}";
            if (!emUso.Contains(candidato)) return candidato;
        }

        return $"{original}-copia-{Guid.NewGuid().ToString("N")[..6]}";
    }

    public async Task<IEnumerable<MacroVersao>> ObterVersoesAsync(int macroId) =>
        _versaoRepo == null ? [] : await _versaoRepo.GetByMacroAsync(macroId);

    /// <summary>Arquivamento reversível (soft-archive): a macro deixa de aparecer em buscas, atalhos
    /// e favoritos, mas continua existindo e pode ser restaurada — diferente de ExcluirAsync.</summary>
    public async Task<(bool Sucesso, string Mensagem)> ArquivarAsync(int id, bool arquivar)
    {
        await _repository.ToggleAtivoAsync(id, !arquivar);
        return (true, arquivar ? "Macro arquivada." : "Macro restaurada.");
    }

    public async Task<(bool Sucesso, string Mensagem)> ExcluirAsync(int id)
    {
        var macro = await _repository.GetByIdAsync(id);
        if (macro == null)
            return (false, "Macro não encontrada.");

        await _repository.DeleteAsync(id);
        return (true, $"Macro \"{macro.Titulo}\" excluída com sucesso.");
    }

    public async Task<IEnumerable<Macro>> BuscarPorGatilhoAsync(string texto, char prefixo = '/')
    {
        if (texto.Length < 2 || texto[0] != prefixo)
            return [];

        return await _repository.SearchAsync(texto[1..]);
    }

    /// <summary>Resolve referências {macro:atalho-x} dentro do conteúdo, expandindo macros aninhadas (até 3 níveis),
    /// e em seguida substitui placeholders de Variáveis Globais cadastradas (ex: {assinatura}).</summary>
    public async Task<string> ResolverMacrosAninhadasAsync(string conteudo, int profundidade = 0)
    {
        if (profundidade < 3 && _macroRefRegex.IsMatch(conteudo))
        {
            var matches = _macroRefRegex.Matches(conteudo);
            foreach (Match m in matches)
            {
                var atalho = m.Groups[1].Value;
                var referenciada = await _repository.GetByAtalhoAsync(atalho);
                var valor = referenciada != null
                    ? await ResolverMacrosAninhadasAsync(referenciada.Conteudo, profundidade + 1)
                    : string.Empty;
                conteudo = conteudo.Replace(m.Value, valor);
            }
        }

        if (profundidade == 0 && _variavelGlobalService != null)
            conteudo = await _variavelGlobalService.ResolverAsync(conteudo);

        return conteudo;
    }

    // ── Import / Export JSON ──────────────────────────────────────
    private class MacroExportDto
    {
        public string Atalho { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Conteudo { get; set; } = string.Empty;
        public string? Categoria { get; set; }
        public bool Ativo { get; set; } = true;
    }

    public async Task<string> ExportarJsonAsync()
    {
        var macros = await _repository.GetAllAsync();
        var dtos = macros.Select(m => new MacroExportDto
        {
            Atalho = m.Atalho, Titulo = m.Titulo, Conteudo = m.Conteudo,
            Categoria = m.Categoria, Ativo = m.Ativo
        });
        return JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<(bool Sucesso, string Mensagem, int Importadas, int Ignoradas)> ImportarJsonAsync(string json)
    {
        List<MacroExportDto>? dtos;
        try { dtos = JsonSerializer.Deserialize<List<MacroExportDto>>(json); }
        catch (Exception ex) { return (false, $"JSON inválido: {ex.Message}", 0, 0); }

        if (dtos == null || dtos.Count == 0)
            return (false, "Nenhuma macro encontrada no arquivo.", 0, 0);

        var existentes = (await _repository.GetAllAsync()).Select(m => m.Atalho).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int importadas = 0, ignoradas = 0;
        var novas = new List<Macro>();

        foreach (var dto in dtos)
        {
            if (string.IsNullOrWhiteSpace(dto.Atalho) || string.IsNullOrWhiteSpace(dto.Conteudo))
            {
                ignoradas++; continue;
            }
            var atalho = dto.Atalho.Trim().ToLower().Replace(" ", "-");
            if (existentes.Contains(atalho))
            {
                ignoradas++; continue;
            }
            existentes.Add(atalho);
            novas.Add(new Macro
            {
                Atalho = atalho,
                Titulo = string.IsNullOrWhiteSpace(dto.Titulo) ? dto.Atalho : dto.Titulo,
                Conteudo = dto.Conteudo,
                Categoria = dto.Categoria,
                Ativo = dto.Ativo
            });
            importadas++;
        }

        // O lote inteiro está numa transação: se o banco recusar uma linha, nada é gravado.
        try { await _repository.BatchInsertAsync(novas); }
        catch (RegistroDuplicadoException ex) { return (false, $"Importação cancelada. {ex.Message}", 0, 0); }

        return (true, $"{importadas} macro(s) importada(s), {ignoradas} ignorada(s) (duplicadas ou inválidas).", importadas, ignoradas);
    }

    // ── Import CSV (cabeçalho: Atalho,Titulo,Conteudo,Categoria) ──────
    public async Task<(bool Sucesso, string Mensagem, int Importadas, int Ignoradas)> ImportarCsvAsync(string csv)
    {
        List<string[]> linhas;
        try { linhas = ParseCsv(csv); }
        catch (Exception ex) { return (false, $"CSV inválido: {ex.Message}", 0, 0); }

        if (linhas.Count < 2)
            return (false, "Nenhuma macro encontrada. A primeira linha deve ser o cabeçalho (Atalho,Titulo,Conteudo,Categoria).", 0, 0);

        var cabecalho = linhas[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        var idxAtalho    = cabecalho.IndexOf("atalho");
        var idxTitulo    = cabecalho.IndexOf("titulo");
        var idxConteudo  = cabecalho.IndexOf("conteudo");
        var idxCategoria = cabecalho.IndexOf("categoria");

        if (idxAtalho < 0 || idxTitulo < 0 || idxConteudo < 0)
            return (false, "Cabeçalho deve conter as colunas Atalho, Titulo e Conteudo (Categoria é opcional).", 0, 0);

        var existentes = (await _repository.GetAllAsync()).Select(m => m.Atalho).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int importadas = 0, ignoradas = 0;
        var novas = new List<Macro>();

        for (var i = 1; i < linhas.Count; i++)
        {
            var l = linhas[i];
            if (l.Length == 1 && string.IsNullOrWhiteSpace(l[0])) continue;
            string Col(int idx) => idx >= 0 && idx < l.Length ? l[idx] : string.Empty;

            var atalho   = Col(idxAtalho).Trim();
            var conteudo = Col(idxConteudo);
            if (string.IsNullOrWhiteSpace(atalho) || string.IsNullOrWhiteSpace(conteudo))
            {
                ignoradas++; continue;
            }

            var atalhoNormalizado = atalho.ToLower().Replace(" ", "-");
            if (existentes.Contains(atalhoNormalizado))
            {
                ignoradas++; continue;
            }

            existentes.Add(atalhoNormalizado);
            novas.Add(new Macro
            {
                Atalho    = atalhoNormalizado,
                Titulo    = string.IsNullOrWhiteSpace(Col(idxTitulo)) ? atalho : Col(idxTitulo),
                Conteudo  = conteudo,
                Categoria = idxCategoria >= 0 && !string.IsNullOrWhiteSpace(Col(idxCategoria)) ? Col(idxCategoria) : null,
                Ativo     = true
            });
            importadas++;
        }

        // O lote inteiro está numa transação: se o banco recusar uma linha, nada é gravado.
        try { await _repository.BatchInsertAsync(novas); }
        catch (RegistroDuplicadoException ex) { return (false, $"Importação cancelada. {ex.Message}", 0, 0); }

        return (true, $"{importadas} macro(s) importada(s), {ignoradas} ignorada(s) (duplicadas ou inválidas).", importadas, ignoradas);
    }

    /// <summary>Parser CSV simples com suporte a campos entre aspas (vírgulas/quebras de linha/aspas escapadas).</summary>
    private static List<string[]> ParseCsv(string conteudo)
    {
        var linhas = new List<string[]>();
        var campos = new List<string>();
        var campoAtual = new System.Text.StringBuilder();
        var dentroDeAspas = false;
        var i = 0;

        while (i < conteudo.Length)
        {
            var c = conteudo[i];
            if (dentroDeAspas)
            {
                if (c == '"')
                {
                    if (i + 1 < conteudo.Length && conteudo[i + 1] == '"') { campoAtual.Append('"'); i += 2; continue; }
                    dentroDeAspas = false; i++; continue;
                }
                campoAtual.Append(c); i++; continue;
            }

            if (c == '"') { dentroDeAspas = true; i++; continue; }
            if (c == ',') { campos.Add(campoAtual.ToString()); campoAtual.Clear(); i++; continue; }
            if (c == '\r') { i++; continue; }
            if (c == '\n')
            {
                campos.Add(campoAtual.ToString()); campoAtual.Clear();
                linhas.Add(campos.ToArray()); campos = new List<string>();
                i++; continue;
            }
            campoAtual.Append(c); i++;
        }

        if (campoAtual.Length > 0 || campos.Count > 0)
        {
            campos.Add(campoAtual.ToString());
            linhas.Add(campos.ToArray());
        }
        return linhas;
    }
}
