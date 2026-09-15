using MacroHelper.Core;
using MacroHelper.Core.Entities;

namespace MacroHelper.Tests;

public class CategoriaRepositoryTests
{
    /// <summary>
    /// O motivo de existirem dois índices parciais em vez de um UNIQUE(nome, pai_id): num índice
    /// único do SQLite dois NULL são DISTINTOS entre si, então "Jurídico" raiz poderia ser
    /// cadastrado duas vezes sem violar nada.
    /// </summary>
    [Fact]
    public async Task DuasCategoriasRaizComOMesmoNome_SaoRecusadas()
    {
        using var banco = new BancoDeTeste();
        await banco.Categorias.InsertAsync(new Categoria { Nome = "Jurídico" });

        var erro = await Assert.ThrowsAsync<RegistroDuplicadoException>(
            () => banco.Categorias.InsertAsync(new Categoria { Nome = "Jurídico" }));

        Assert.Contains("categoria", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MesmoNomeEmPaisDiferentes_EhPermitido()
    {
        using var banco = new BancoDeTeste();
        var juridico  = await banco.Categorias.InsertAsync(new Categoria { Nome = "Jurídico" });
        var financeiro = await banco.Categorias.InsertAsync(new Categoria { Nome = "Financeiro" });

        await banco.Categorias.InsertAsync(new Categoria { Nome = "Modelos", PaiId = juridico });
        await banco.Categorias.InsertAsync(new Categoria { Nome = "Modelos", PaiId = financeiro });

        Assert.Equal(2, banco.Escalar<int>("SELECT COUNT(*) FROM categorias WHERE nome = 'Modelos'"));
    }

    [Fact]
    public async Task MesmoNomeSobOMesmoPai_EhRecusado()
    {
        using var banco = new BancoDeTeste();
        var pai = await banco.Categorias.InsertAsync(new Categoria { Nome = "Jurídico" });
        await banco.Categorias.InsertAsync(new Categoria { Nome = "Modelos", PaiId = pai });

        await Assert.ThrowsAsync<RegistroDuplicadoException>(
            () => banco.Categorias.InsertAsync(new Categoria { Nome = "Modelos", PaiId = pai }));
    }

    /// <summary>
    /// ON DELETE SET NULL substitui o laço manual que o repositório fazia: buscar as filhas,
    /// zerar o pai de cada uma, gravar N vezes. Se a connection string perder ForeignKeys=True,
    /// este teste é o que denuncia.
    /// </summary>
    [Fact]
    public async Task ExcluirCategoriaPai_PromoveAsFilhasAeRaiz()
    {
        using var banco = new BancoDeTeste();
        var pai = await banco.Categorias.InsertAsync(new Categoria { Nome = "Jurídico" });
        await banco.Categorias.InsertAsync(new Categoria { Nome = "Modelos", PaiId = pai });

        await banco.Categorias.DeleteAsync(pai);

        var filha = (await banco.Categorias.GetAllAsync()).Single();
        Assert.Equal("Modelos", filha.Nome);
        Assert.Null(filha.PaiId);
    }

    /// <summary>
    /// A macro perde o vínculo mas continua ROTULADA: excluir a categoria não deve apagar a
    /// informação de qual era, e o filtro da listagem continua encontrando a macro.
    /// </summary>
    [Fact]
    public async Task ExcluirCategoria_DesligaAMacroMasPreservaOTexto()
    {
        using var banco = new BancoDeTeste();
        var cat = await banco.Categorias.InsertAsync(new Categoria { Nome = "Jurídico" });

        var macro = MacroRepositoryTests.Nova("peticao", categoria: "Jurídico");
        macro.CategoriaId = cat;
        var macroId = await banco.Macros.InsertAsync(macro);

        await banco.Categorias.DeleteAsync(cat);

        var depois = await banco.Macros.GetByIdAsync(macroId);
        Assert.Null(depois!.CategoriaId);
        Assert.Equal("Jurídico", depois.Categoria);
    }

    /// <summary>
    /// Renomear reescreve a cópia desnormalizada em macros.categoria na MESMA transação. Sem
    /// isso o filtro da listagem continuaria oferecendo o nome antigo indefinidamente, e a
    /// divergência não teria conserto automático nenhum.
    /// </summary>
    [Fact]
    public async Task RenomearCategoria_AtualizaOTextoNasMacrosLigadas()
    {
        using var banco = new BancoDeTeste();
        var id = await banco.Categorias.InsertAsync(new Categoria { Nome = "Juridico" });

        var ligada = MacroRepositoryTests.Nova("com-vinculo", categoria: "Juridico");
        ligada.CategoriaId = id;
        await banco.Macros.InsertAsync(ligada);

        // Macro rotulada só pelo texto (veio de importação, nunca teve categoria_id).
        await banco.Macros.InsertAsync(MacroRepositoryTests.Nova("sem-vinculo", categoria: "Juridico"));

        await banco.Categorias.UpdateAsync(new Categoria { Id = id, Nome = "Jurídico" });

        var categorias = (await banco.Macros.GetCategoriasAsync()).ToList();
        Assert.Contains("Jurídico", categorias);   // a ligada acompanhou
        Assert.Contains("Juridico", categorias);   // a solta ficou como estava, por não ter vínculo
    }

    [Fact]
    public async Task Listagem_TrazONomeDoPaiEColocaRaizesPrimeiro()
    {
        using var banco = new BancoDeTeste();
        var pai = await banco.Categorias.InsertAsync(new Categoria { Nome = "Jurídico" });
        await banco.Categorias.InsertAsync(new Categoria { Nome = "Modelos", PaiId = pai });

        var todas = (await banco.Categorias.GetAllAsync()).ToList();

        Assert.Equal("Jurídico", todas[0].Nome);
        Assert.Null(todas[0].NomePai);
        Assert.Equal("Modelos", todas[1].Nome);
        Assert.Equal("Jurídico", todas[1].NomePai);
    }

    [Fact]
    public async Task GetRaiz_NaoTrazSubcategorias()
    {
        using var banco = new BancoDeTeste();
        var pai = await banco.Categorias.InsertAsync(new Categoria { Nome = "Jurídico" });
        await banco.Categorias.InsertAsync(new Categoria { Nome = "Modelos", PaiId = pai });

        Assert.Equal("Jurídico", (await banco.Categorias.GetRaizAsync()).Single().Nome);
    }
}
