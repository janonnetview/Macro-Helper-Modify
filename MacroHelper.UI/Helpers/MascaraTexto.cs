using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
// O projeto também referencia WinForms (por causa do ícone de bandeja) e os dois mundos têm
// TextBox e KeyEventArgs. Os apelidos deixam explícito que aqui é sempre o WPF.
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace MacroHelper.UI.Helpers;

/// <summary>
/// Máscara de digitação para os campos de data e hora escritos à mão.
///
/// Antes eram TextBox comuns: dava para digitar "2424" numa data e só descobrir que estava
/// errado ao salvar. Aqui a barra e os dois-pontos entram sozinhos enquanto se digita, letra
/// nenhuma passa, e o campo para de crescer quando a data completa — o erro deixa de ser
/// possível em vez de ser avisado depois.
///
/// Backspace e Delete apagam o dígito, nunca o separador: apagar a barra de "24/08" seria um
/// gesto sem efeito, porque a máscara a devolveria no instante seguinte.
/// </summary>
public static class MascaraTexto
{
    public enum Formato { Nenhum, Data, Hora }

    public static readonly DependencyProperty FormatoProperty =
        DependencyProperty.RegisterAttached(
            "Formato", typeof(Formato), typeof(MascaraTexto),
            new PropertyMetadata(Formato.Nenhum, AoTrocarFormato));

    public static Formato GetFormato(DependencyObject alvo) => (Formato)alvo.GetValue(FormatoProperty);

    public static void SetFormato(DependencyObject alvo, Formato valor) => alvo.SetValue(FormatoProperty, valor);

    /// <summary>Marca o momento em que a própria máscara reescreve o Text, para não se reprocessar.</summary>
    private static readonly DependencyProperty ReescrevendoProperty =
        DependencyProperty.RegisterAttached(
            "Reescrevendo", typeof(bool), typeof(MascaraTexto), new PropertyMetadata(false));

    private static void AoTrocarFormato(DependencyObject alvo, DependencyPropertyChangedEventArgs e)
    {
        if (alvo is not TextBox caixa) return;

        caixa.PreviewTextInput -= AoDigitar;
        caixa.PreviewKeyDown   -= AoTeclar;
        caixa.TextChanged      -= AoMudarTexto;

        if ((Formato)e.NewValue == Formato.Nenhum) return;

        caixa.PreviewTextInput += AoDigitar;
        caixa.PreviewKeyDown   += AoTeclar;
        caixa.TextChanged      += AoMudarTexto;
    }

    // ── Eventos ──────────────────────────────────────────────────────────────

    /// <summary>Só dígito entra; separador é coisa da máscara, e espaço também não vale.</summary>
    private static void AoDigitar(object sender, TextCompositionEventArgs e)
    {
        if (!e.Text.All(char.IsDigit)) e.Handled = true;
    }

    private static void AoTeclar(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox caixa) return;
        if (e.Key is not (Key.Back or Key.Delete)) return;

        // Apagar uma seleção já se resolve sozinho: o TextChanged remonta o que sobrou.
        if (caixa.SelectionLength > 0) return;

        var texto = caixa.Text;
        var i = e.Key == Key.Back
            ? DigitoAntesDe(texto, caixa.SelectionStart)
            : DigitoAPartirDe(texto, caixa.SelectionStart);

        // Sem dígito para apagar naquela direção, a tecla não faz nada — e não pode fazer:
        // o comportamento padrão comeria o separador.
        if (i < 0) { e.Handled = true; return; }

        Reescrever(caixa, Formatar(texto.Remove(i, 1), GetFormato(caixa)), ContarDigitos(texto, i));
        e.Handled = true;
    }

    private static void AoMudarTexto(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox caixa) return;
        if ((bool)caixa.GetValue(ReescrevendoProperty)) return;

        var formatado = Formatar(caixa.Text, GetFormato(caixa));
        if (formatado == caixa.Text) return;

        Reescrever(caixa, formatado, ContarDigitos(caixa.Text, caixa.SelectionStart));
    }

    /// <summary>
    /// Troca o texto e recoloca o cursor. A conta é feita em dígitos, não em posição: só assim
    /// o cursor volta para o mesmo ponto lógico depois de a máscara mexer nos separadores.
    /// </summary>
    private static void Reescrever(TextBox caixa, string texto, int digitosAntesDoCursor)
    {
        caixa.SetValue(ReescrevendoProperty, true);
        try
        {
            caixa.Text            = texto;
            caixa.SelectionStart  = PosicaoDepoisDe(texto, digitosAntesDoCursor);
            caixa.SelectionLength = 0;
        }
        finally { caixa.SetValue(ReescrevendoProperty, false); }
    }

    // ── Contas de texto (sem WPF, para poderem ser testadas) ─────────────────

    /// <summary>
    /// Reduz o texto aos dígitos e os devolve com os separadores no lugar, cortando o que
    /// passar do tamanho do formato: "24082026" e "24/08/2026" dão no mesmo, e "240820269"
    /// perde o dígito sobrando em vez de virar uma data que ninguém consegue ler.
    /// </summary>
    public static string Formatar(string texto, Formato formato)
    {
        if (formato == Formato.Nenhum) return texto;

        var (separador, posicoes, maximo) = formato == Formato.Hora
            ? (':', new[] { 2 },    4)
            : ('/', new[] { 2, 4 }, 8);

        var montado = new StringBuilder();
        var digitos = 0;

        foreach (var c in texto)
        {
            if (!char.IsDigit(c)) continue;
            if (digitos == maximo) break;
            if (posicoes.Contains(digitos)) montado.Append(separador);

            montado.Append(c);
            digitos++;
        }

        return montado.ToString();
    }

    /// <summary>Posição do cursor logo depois do n-ésimo dígito do texto formatado.</summary>
    public static int PosicaoDepoisDe(string texto, int digitos)
    {
        if (digitos <= 0) return 0;

        var vistos = 0;
        for (var i = 0; i < texto.Length; i++)
            if (char.IsDigit(texto[i]) && ++vistos == digitos)
                return i + 1;

        return texto.Length;
    }

    /// <summary>Quantos dígitos existem antes de uma posição do texto.</summary>
    public static int ContarDigitos(string texto, int ate) =>
        texto[..Math.Clamp(ate, 0, texto.Length)].Count(char.IsDigit);

    private static int DigitoAntesDe(string texto, int posicao)
    {
        var i = Math.Min(posicao, texto.Length) - 1;
        while (i >= 0 && !char.IsDigit(texto[i])) i--;
        return i;
    }

    private static int DigitoAPartirDe(string texto, int posicao)
    {
        var i = Math.Max(posicao, 0);
        while (i < texto.Length && !char.IsDigit(texto[i])) i++;
        return i < texto.Length ? i : -1;
    }
}
