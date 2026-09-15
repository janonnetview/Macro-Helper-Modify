using MacroHelper.Core.Entities;

namespace MacroHelper.Services;

/// <summary>
/// O caminho que toda macro percorre da escolha até o texto aparecer na tela do outro app:
/// resolver aninhadas → resolver globais → resolver variáveis → inserir → registrar no log.
///
/// Existia em três cópias — popup do gatilho, busca rápida e Ctrl+Alt+N — e elas tinham
/// divergido. A do Ctrl+Alt+N nunca abria o diálogo de variáveis: trocava todo placeholder
/// sem preenchimento automático por string vazia. Na prática, Ctrl+Alt+1 apagava o
/// <c>{nome}</c> da macro em silêncio, o mesmo estrago que a variável global em branco fazia.
/// Com um caminho só, corrigir uma vez corrige nos três.
/// </summary>
public class InsercaoDeMacroService
{
    private readonly MacroService         _macroService;
    private readonly TextInsertionService _insercao;
    private readonly LogUsoService        _log;

    public InsercaoDeMacroService(MacroService macroService,
        TextInsertionService insercao, LogUsoService log)
    {
        _macroService = macroService;
        _insercao     = insercao;
        _log          = log;
    }

    /// <summary>
    /// Como pedir ao usuário os valores que o app não sabe preencher sozinho. Recebe o conteúdo
    /// já resolvido e as variáveis encontradas; devolve o texto final, ou <c>null</c> se o
    /// usuário cancelou.
    ///
    /// É um Func e não uma dependência porque abrir janela é responsabilidade da UI — a camada
    /// de serviço não conhece WPF. Quem não fornece (um teste, por exemplo) simplesmente não
    /// pergunta nada.
    /// </summary>
    public Func<string, List<VariavelInfo>, string?>? PerguntarVariaveis { get; set; }

    /// <summary>Nome que preenche o placeholder <c>{usuario}</c>.</summary>
    public Func<string> ObterNomeUsuario { get; set; } = () => string.Empty;

    /// <summary>
    /// Insere a macro no aplicativo em foco.
    /// </summary>
    /// <param name="gatilhoParaRemover">
    /// Texto que o usuário digitou e precisa ser apagado antes (ex.: "/atalho"). Vazio quando a
    /// macro veio da busca rápida ou de um atalho de teclado, onde nada foi digitado.
    /// </param>
    /// <param name="antesDeInserir">
    /// Executado depois de resolver tudo e antes de mandar as teclas — é onde a UI devolve o
    /// foco para a janela de origem. Precisa ser aqui: se rodasse antes, o diálogo de variáveis
    /// roubaria o foco de volta e o texto iria parar na janela errada.
    /// </param>
    /// <returns>false se o usuário cancelou o diálogo de variáveis.</returns>
    public async Task<bool> InserirAsync(Macro macro, string gatilhoParaRemover = "",
        Action? antesDeInserir = null)
    {
        var conteudo = await ResolverConteudoAsync(macro);
        if (conteudo == null) return false;

        antesDeInserir?.Invoke();

        await _insercao.InserirTextoAsync(gatilhoParaRemover, conteudo);

        // Sem o marcador na conta: ele não vira caractere no app de destino, e a estimativa de
        // tempo economizado do Início é feita em cima deste número.
        await _log.RegistrarAsync(macro.Id, macro.Titulo, macro.Atalho,
            MarcadorDeCursor.Remover(conteudo).Length);
        return true;
    }

    /// <summary>
    /// A metade do pipeline que decide QUAL texto entra — separada da metade que manda as
    /// teclas. É aqui que moram as regras (aninhadas, globais, variáveis), e ela roda inteira
    /// sem tocar no teclado, então dá para conferir o resultado sem digitar na janela de
    /// ninguém. Devolve <c>null</c> quando o usuário cancela o diálogo de variáveis.
    /// </summary>
    public async Task<string?> ResolverConteudoAsync(Macro macro)
    {
        var conteudo = await _macroService.ResolverMacrosAninhadasAsync(macro.Conteudo);

        if (!VariavelService.TemVariaveis(conteudo)) return conteudo;

        var variaveis = VariavelService.ExtrairVariaveis(conteudo, ObterNomeUsuario());

        // Data, hora, usuário e clipboard já vêm resolvidos: se são as únicas, não há o que
        // perguntar e a inserção acontece sem interrupção.
        //
        // A chave é o TOKEN inteiro, e não o nome: {data} e {data+7} são a mesma variável com
        // valores obrigatoriamente diferentes, e por nome uma sobrescreveria a outra.
        if (variaveis.All(v => v.AutoPreencher) || PerguntarVariaveis == null)
            return VariavelService.Substituir(conteudo,
                variaveis.ToDictionary(v => v.Token, v => v.ValorPadrao ?? string.Empty));

        return PerguntarVariaveis(conteudo, variaveis);
    }
}
