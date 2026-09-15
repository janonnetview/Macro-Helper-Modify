namespace MacroHelper.Core.Entities;

/// <summary>
/// Um texto que passou pela área de transferência.
///
/// Só texto: uma imagem copiada continua se perdendo, como sempre. Guardar imagem custaria
/// megabytes por item num banco que hoje inteiro cabe em poucos, e o app não tem para onde
/// colar uma — a inserção manda teclas, não objetos.
/// </summary>
public class ItemClipboard
{
    public int      Id        { get; set; }
    public string   Conteudo  { get; set; } = string.Empty;

    /// <summary>Fixado no topo e imune à poda por retenção.</summary>
    public bool     Fixado    { get; set; }

    /// <summary>Título da janela de onde o texto foi copiado. Null quando não deu para saber.</summary>
    public string?  Origem    { get; set; }

    public DateTime DataCopia { get; set; } = DateTime.Now;

    /// <summary>
    /// Uma linha só, para o cartão da lista. Quebras viram espaço porque um trecho de três
    /// linhas ocuparia a lista inteira; o texto que vai para o outro app continua intacto.
    /// </summary>
    public string Resumo
    {
        get
        {
            var texto = Conteudo.Replace('\r', ' ').Replace('\n', ' ').Trim();

            // Espaços colapsados: texto copiado de tabela ou de PDF costuma vir com corridas
            // de espaço que, na lista, viram buracos sem significado nenhum.
            var sb = new System.Text.StringBuilder(texto.Length);
            var espacoAnterior = false;
            foreach (var c in texto)
            {
                var espaco = char.IsWhiteSpace(c);
                if (espaco && espacoAnterior) continue;
                sb.Append(espaco ? ' ' : c);
                espacoAnterior = espaco;
            }

            var limpo = sb.ToString();
            return limpo.Length <= 160 ? limpo : limpo[..160] + "…";
        }
    }

    /// <summary>"3 linhas · 412 caracteres" — o que o resumo cortou fora.</summary>
    public string Medida
    {
        get
        {
            var linhas = Conteudo.Count(c => c == '\n') + 1;
            var chars  = Conteudo.Length;
            return linhas > 1 ? $"{linhas} linhas · {chars} caracteres" : $"{chars} caracteres";
        }
    }
}
