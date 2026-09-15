using Dapper;
using MacroHelper.Core.Entities;
using MacroHelper.Data.Context;

namespace MacroHelper.Data.Repositories;

public class CompromissoRepository
{
    private readonly SqliteContext _ctx;
    public CompromissoRepository(SqliteContext ctx) => _ctx = ctx;

    private const string Colunas = """
        id, titulo, local, observacoes, quando, situacao,
        duracao_minutos            AS DuracaoMinutos,
        aviso_antecipado_minutos   AS AvisoAntecipadoMinutos,
        aviso_antecipado_disparado AS AvisoAntecipadoDisparado,
        aviso_na_hora_minutos      AS AvisoNaHoraMinutos,
        aviso_na_hora_disparado    AS AvisoNaHoraDisparado,
        data_criacao               AS DataCriacao
        """;

    /// <summary>
    /// Cronológica, e só isso. Uma agenda não tem "mais importante primeiro": tem o que vem
    /// antes e o que vem depois, e qualquer outra ordenação destruiria a única leitura útil
    /// da lista.
    /// </summary>
    public async Task<IEnumerable<Compromisso>> GetAllAsync()
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QueryAsync<Compromisso>(
            $"SELECT {Colunas} FROM compromissos ORDER BY quando");
    }

    public async Task<Compromisso?> GetByIdAsync(int id)
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QuerySingleOrDefaultAsync<Compromisso>(
            $"SELECT {Colunas} FROM compromissos WHERE id = @id", new { id });
    }

    public async Task<int> InsertAsync(Compromisso c)
    {
        const string sql = """
            INSERT INTO compromissos (titulo, local, observacoes, quando, duracao_minutos,
                                      situacao, aviso_antecipado_minutos, aviso_antecipado_disparado,
                                      aviso_na_hora_minutos, aviso_na_hora_disparado, data_criacao)
            VALUES (@Titulo, @Local, @Observacoes, @Quando, @DuracaoMinutos,
                    @Situacao, @AvisoAntecipadoMinutos, @AvisoAntecipadoDisparado,
                    @AvisoNaHoraMinutos, @AvisoNaHoraDisparado, @DataCriacao)
            RETURNING id
            """;

        c.DataCriacao = DateTime.Now;

        using var conexao = await _ctx.AbrirAsync();
        return await conexao.ExecuteScalarAsync<int>(sql, c);
    }

    public async Task UpdateAsync(Compromisso c)
    {
        const string sql = """
            UPDATE compromissos SET
                titulo                     = @Titulo,
                local                      = @Local,
                observacoes                = @Observacoes,
                quando                     = @Quando,
                duracao_minutos            = @DuracaoMinutos,
                situacao                   = @Situacao,
                aviso_antecipado_minutos   = @AvisoAntecipadoMinutos,
                aviso_antecipado_disparado = @AvisoAntecipadoDisparado,
                aviso_na_hora_minutos      = @AvisoNaHoraMinutos,
                aviso_na_hora_disparado    = @AvisoNaHoraDisparado
            WHERE id = @Id
            """;

        using var conexao = await _ctx.AbrirAsync();
        await conexao.ExecuteAsync(sql, c);
    }

    public async Task DefinirSituacaoAsync(int id, SituacaoCompromisso situacao)
    {
        using var conexao = await _ctx.AbrirAsync();
        await conexao.ExecuteAsync(
            "UPDATE compromissos SET situacao = @situacao WHERE id = @id",
            new { id, situacao });
    }

    public async Task DeleteAsync(int id)
    {
        using var conexao = await _ctx.AbrirAsync();
        await conexao.ExecuteAsync("DELETE FROM compromissos WHERE id = @id", new { id });
    }

    /// <summary>
    /// Compromissos com pelo menos um aviso cuja hora já chegou e que ainda não avisou.
    ///
    /// A conta do instante do aviso é feita AQUI, no SQL, e não carregando a agenda inteira
    /// para filtrar em memória a cada 30 segundos: <c>datetime(quando, '-' || minutos ||
    /// ' minutes')</c> devolve exatamente o mesmo formato 'yyyy-MM-dd HH:mm:ss' em que as datas
    /// são gravadas, então a comparação com @ate é textual e cronológica ao mesmo tempo — a
    /// mesma propriedade de que o resto do esquema já depende.
    ///
    /// Só situacao = 0: cancelar ou marcar como realizado precisa CALAR os avisos que ainda
    /// não saíram, e é este WHERE que cumpre essa promessa.
    /// </summary>
    public async Task<IEnumerable<Compromisso>> GetAvisosVencidosAsync(DateTime ate)
    {
        using var conexao = await _ctx.AbrirAsync();
        return await conexao.QueryAsync<Compromisso>(
            $"""
             SELECT {Colunas} FROM compromissos
             WHERE situacao = 0
               AND ((aviso_antecipado_minutos IS NOT NULL
                     AND aviso_antecipado_disparado = 0
                     AND datetime(quando, '-' || aviso_antecipado_minutos || ' minutes') <= @ate)
                 OR (aviso_na_hora_minutos IS NOT NULL
                     AND aviso_na_hora_disparado = 0
                     AND datetime(quando, '-' || aviso_na_hora_minutos || ' minutes') <= @ate))
             ORDER BY quando
             """, new { ate });
    }

    /// <summary>
    /// Grava a marca de "já avisei" ANTES de a notificação sair, como em tarefas: se o app
    /// fechar entre uma coisa e outra, o pior caso é um balão perdido — o inverso seria o
    /// mesmo aviso reaparecendo a cada 30 segundos, para sempre.
    /// </summary>
    /// <param name="antecipado">Qual dos dois avisos marcar.</param>
    public async Task MarcarAvisoDisparadoAsync(int id, bool antecipado)
    {
        // A coluna sai de um bool, não de texto vindo de fora: não há entrada externa nesta
        // string, e um parâmetro não pode nomear coluna em SQL.
        var coluna = antecipado ? "aviso_antecipado_disparado" : "aviso_na_hora_disparado";

        using var conexao = await _ctx.AbrirAsync();
        await conexao.ExecuteAsync(
            $"UPDATE compromissos SET {coluna} = 1 WHERE id = @id", new { id });
    }
}
