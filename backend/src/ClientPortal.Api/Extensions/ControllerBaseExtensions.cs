using ClientPortal.Application.DTOs.ProjectFiles;
using Microsoft.AspNetCore.Mvc;

namespace ClientPortal.Api.Extensions;

internal static class ControllerBaseExtensions
{
    /// <summary>Always a download, never rendered: a file somebody uploaded must not run on this origin (the browser may not sniff it either).</summary>
    public static IActionResult DownloadFile(this ControllerBase controller, FileDownload download)
    {
        controller.Response.Headers.XContentTypeOptions = "nosniff";
        controller.Response.ContentLength = download.Length;

        return controller.File(download.Content, download.ContentType, download.FileName);
    }
}
