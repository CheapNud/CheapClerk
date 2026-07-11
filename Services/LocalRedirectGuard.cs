namespace CheapClerk.Services;

public static class LocalRedirectGuard
{
    /// <summary>
    /// Returns the redirect path only when it is app-local (mirrors ASP.NET's
    /// IsLocalUrl: leading '/', not '//' or '/\'); anything else falls back to "/".
    /// </summary>
    public static string Sanitize(string? redirectUri)
    {
        if (string.IsNullOrEmpty(redirectUri) || redirectUri[0] != '/')
            return "/";

        if (redirectUri.Length > 1 && (redirectUri[1] == '/' || redirectUri[1] == '\\'))
            return "/";

        return redirectUri;
    }
}
