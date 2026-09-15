namespace MacroHelper.Core.Entities;

public class VariavelGlobal
{
    public int      Id          { get; set; }
    public string   Nome        { get; set; } = string.Empty;
    public string   ValorPadrao { get; set; } = string.Empty;
    public string?  Descricao   { get; set; }
    public DateTime DataCriacao { get; set; } = DateTime.Now;
}
