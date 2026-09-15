using Dapper;
using System.Data;
using System.Globalization;

namespace MacroHelper.Data;

/// <summary>
/// Fixa o formato de data nas duas pontas: 'yyyy-MM-dd HH:mm:ss', hora local.
///
/// Sem ele, o Microsoft.Data.Sqlite grava DateTime com frações de segundo e sufixo de fuso
/// ("2026-08-18 14:30:00.1234567"), e aí duas coisas quebram de uma vez: a comparação por
/// texto (<c>data_uso &gt;= @de</c>) passa a depender do comprimento da string, e as linhas
/// importadas do dump — que não têm frações — deixam de ordenar junto com as gravadas pelo app.
/// </summary>
public sealed class DataSqliteHandler : SqlMapper.TypeHandler<DateTime>
{
    public const string Formato = "yyyy-MM-dd HH:mm:ss";

    private static bool _registrado;

    /// <summary>
    /// Registra o handler uma única vez no Dapper. Chamar de novo é inofensivo.
    /// Registrar <c>DateTime</c> cobre também <c>DateTime?</c>: o Dapper associa
    /// automaticamente o Nullable de todo tipo-valor registrado.
    /// </summary>
    public static void Registrar()
    {
        if (_registrado) return;
        SqlMapper.AddTypeHandler(new DataSqliteHandler());
        _registrado = true;
    }

    public override void SetValue(IDbDataParameter parameter, DateTime value)
    {
        parameter.DbType = DbType.String;
        parameter.Value  = value.ToString(Formato, CultureInfo.InvariantCulture);
    }

    public override DateTime Parse(object value)
    {
        if (value is DateTime jaConvertido) return jaConvertido;

        var texto = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

        // DateTimeStyles.None devolve Kind = Unspecified, que é o correto para uma hora de
        // parede: nenhuma conversão de fuso acontece ao comparar com DateTime.Now.
        return DateTime.TryParseExact(texto, Formato, CultureInfo.InvariantCulture,
                   DateTimeStyles.None, out var exato)
            ? exato
            : DateTime.Parse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None);
    }
}
