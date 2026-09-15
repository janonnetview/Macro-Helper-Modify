namespace MacroHelper.Services;

/// <summary>
/// Fonte da hora atual. Existe por causa do LembreteService: sem poder controlar "agora", a
/// única forma de testar "um lembrete vencido enquanto o app estava fechado dispara na
/// primeira varredura" seria mexer no relógio da máquina ou esperar de verdade.
/// </summary>
public interface IRelogio
{
    DateTime Agora { get; }
}

public sealed class RelogioDoSistema : IRelogio
{
    public DateTime Agora => DateTime.Now;
}
