namespace MacroHelper.Core;

/// <summary>
/// Um índice único do banco recusou a gravação — atalho repetido, atalho de tecla já usado,
/// variável global com nome repetido.
///
/// Existe para que a camada de serviço devolva "Já existe uma macro com esse atalho" em vez
/// de deixar uma SqliteException chegar ao handler global como erro inesperado: duplicar um
/// atalho é engano comum do usuário, não falha do app. Também mantém a camada de serviço sem
/// referência a tipos do provider de banco.
/// </summary>
public class RegistroDuplicadoException : Exception
{
    /// <summary>Nome do campo que colidiu, em linguagem de usuário (ex.: "atalho").</summary>
    public string Campo { get; }

    public RegistroDuplicadoException(string campo, string mensagem, Exception? interna = null)
        : base(mensagem, interna) => Campo = campo;
}
