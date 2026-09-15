namespace MacroHelper.Core.Entities;

public class LogUso
{
    public int      Id          { get; set; }
    public int?     MacroId     { get; set; }
    public string   MacroTitulo { get; set; } = string.Empty;
    public string   MacroAtalho { get; set; } = string.Empty;
    public string?  Aplicativo  { get; set; }
    public DateTime DataUso     { get; set; } = DateTime.Now;
    public int      Caracteres  { get; set; } = 0;
}
