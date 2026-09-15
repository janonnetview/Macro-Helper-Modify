using System.Diagnostics;

namespace MacroHelper.UI.Helpers;

/// <summary>Abrir um endereço no navegador padrão, do mesmo jeito em todo lugar.</summary>
public static class Navegador
{
    /// <summary>
    /// <c>UseShellExecute</c> é obrigatório: sem ele o .NET tenta EXECUTAR a URL como programa
    /// e o clique morre numa exceção. O esquema é conferido antes — um link de nota é texto que
    /// veio de fora, e "file:///" ou "javascript:" não abrem nada por aqui.
    /// </summary>
    public static void Abrir(string? endereco)
    {
        try
        {
            if (!Uri.TryCreate(endereco, UriKind.Absolute, out var uri)) return;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps &&
                uri.Scheme != Uri.UriSchemeMailto) return;

            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) { App.LogErro(ex); }
    }
}
