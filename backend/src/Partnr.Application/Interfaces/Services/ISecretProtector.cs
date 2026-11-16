namespace Partnr.Application.Interfaces.Services;

/// <summary>Encrypts and signs small secrets (a Google token, the OAuth state). A value made for one purpose is useless for another.</summary>
public interface ISecretProtector
{
    /// <summary>URL-safe text.</summary>
    string Protect(string purpose, string plaintext);

    /// <summary>Null when the value was not made by <see cref="Protect"/> for this purpose, or was changed.</summary>
    string? Unprotect(string purpose, string protectedValue);
}
