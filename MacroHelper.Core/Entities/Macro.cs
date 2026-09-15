namespace MacroHelper.Core.Entities;

public class Macro
{
    public int      Id              { get; set; }
    public string   Atalho          { get; set; } = string.Empty;
    public string   Titulo          { get; set; } = string.Empty;
    public string   Conteudo        { get; set; } = string.Empty;
    public string?  Categoria       { get; set; }
    public int?     CategoriaId     { get; set; }
    public bool     Ativo           { get; set; } = true;
    public DateTime DataCriacao     { get; set; } = DateTime.Now;
    public DateTime? DataAtualizacao { get; set; }

    public bool     Favorito        { get; set; } = false;
    public string?  AtalhoTecla     { get; set; }              // dígito 1-9 para Ctrl+Alt+N
    public string?  ImagemBase64    { get; set; }
}
