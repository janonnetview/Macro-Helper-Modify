using MacroHelper.Core;
using Microsoft.Data.Sqlite;

namespace MacroHelper.Data;

/// <summary>
/// Traduz a violação de índice único do SQLite para <see cref="RegistroDuplicadoException"/>.
///
/// Atalho repetido é engano corriqueiro do usuário. Sem esta tradução ele chegaria à UI como
/// SqliteException e cairia no handler global de "erro inesperado" — susto desproporcional
/// para algo que merece apenas um aviso no formulário.
/// </summary>
internal static class ErrosSqlite
{
    // Códigos estendidos do SQLite. Os genéricos (SQLITE_CONSTRAINT = 19) englobam também
    // NOT NULL e chave estrangeira, que NÃO são duplicidade e devem continuar subindo como erro.
    private const int ConstraintUnique     = 2067; // SQLITE_CONSTRAINT_UNIQUE
    private const int ConstraintPrimaryKey = 1555; // SQLITE_CONSTRAINT_PRIMARYKEY

    /// <summary>
    /// A mensagem do SQLite nomeia as colunas do índice ("UNIQUE constraint failed:
    /// macros.atalho"), não o nome do índice — por isso o casamento é por tabela.coluna.
    /// A ordem importa: "macros.atalho" é prefixo de "macros.atalho_tecla", então o mais
    /// específico precisa vir primeiro.
    /// </summary>
    private static readonly (string Alvo, string Campo, string Mensagem)[] _conhecidos =
    [
        ("macros.atalho_tecla",    "atalho_tecla", "Esse atalho de teclado já está em uso por outra macro."),
        ("macros.atalho",          "atalho",       "Já existe uma macro com esse atalho."),
        ("variaveis_globais.nome", "nome",         "Já existe uma variável global com esse nome."),
        ("categorias.nome",        "nome",         "Já existe uma categoria com esse nome nesse nível."),
    ];

    public static bool EhDuplicidade(SqliteException ex) =>
        ex.SqliteExtendedErrorCode is ConstraintUnique or ConstraintPrimaryKey;

    public static RegistroDuplicadoException Traduzir(SqliteException ex)
    {
        foreach (var (alvo, campo, mensagem) in _conhecidos)
            if (ex.Message.Contains(alvo, StringComparison.Ordinal))
                return new RegistroDuplicadoException(campo, mensagem, ex);

        return new RegistroDuplicadoException("registro", "Esse registro já existe.", ex);
    }
}
