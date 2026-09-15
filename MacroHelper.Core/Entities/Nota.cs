using MacroHelper.Core.Texto;

namespace MacroHelper.Core.Entities;

public class Nota
{
    public int       Id       { get; set; }
    public string    Titulo   { get; set; } = string.Empty;
    public string    Conteudo { get; set; } = string.Empty;

    /// <summary>Fixada no topo da lista, independente da data.</summary>
    public bool      Fixada   { get; set; }

    public DateTime  DataCriacao     { get; set; } = DateTime.Now;
    public DateTime? DataAtualizacao { get; set; }

    /// <summary>
    /// A data que a lista mostra: a da última edição enquanto houver uma, senão a de criação.
    /// É o mesmo COALESCE que ordena as notas no repositório — sem isso, uma nota nunca
    /// editada apareceria sem data nenhuma ao lado das outras.
    /// </summary>
    public DateTime DataDeExibicao => DataAtualizacao ?? DataCriacao;

    /// <summary>
    /// Primeiras linhas do conteúdo, para o cartão da lista.
    ///
    /// Sem a marcação: o cartão é uma prévia de uma linha só, e ali um "## Reunião de quarta"
    /// com o cerquilha à mostra rouba a atenção do que a nota diz.
    /// </summary>
    public string Resumo
    {
        get
        {
            var texto = Markdown.ParaTextoSimples(Conteudo);
            return texto.Length <= 160 ? texto : texto[..160] + "…";
        }
    }
}
