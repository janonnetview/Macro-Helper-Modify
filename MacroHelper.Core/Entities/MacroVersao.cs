namespace MacroHelper.Core.Entities;

public class MacroVersao
{
    public int      Id              { get; set; }
    public int      MacroId         { get; set; }
    public string   Titulo          { get; set; } = string.Empty;
    public string   Conteudo        { get; set; } = string.Empty;
    public DateTime DataModificacao { get; set; } = DateTime.Now;
}
