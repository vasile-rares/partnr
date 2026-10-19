namespace ClientPortal.Application.DTOs.ProjectFiles;

/// <summary>An uploaded file as the service sees it, so Application does not depend on ASP.NET's <c>IFormFile</c>.</summary>
public sealed record FileUpload(string FileName, long Length, Stream Content);

/// <summary>The caller owns <see cref="Content"/> and disposes it.</summary>
public sealed record FileDownload(string FileName, string ContentType, long Length, Stream Content);
