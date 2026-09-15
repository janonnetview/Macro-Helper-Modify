namespace MacroHelper.Core.Entities;

public class Categoria
{
    public int     Id          { get; set; }
    public string  Nome        { get; set; } = string.Empty;
    public string? Icone       { get; set; }
    public string? Cor         { get; set; }
    public int?    PaiId       { get; set; }
    public string? NomePai     { get; set; } // join helper
    public int     Ordem       { get; set; }
    public DateTime DataCriacao { get; set; } = DateTime.Now;

    public List<Categoria> Subcategorias { get; set; } = new();
}
