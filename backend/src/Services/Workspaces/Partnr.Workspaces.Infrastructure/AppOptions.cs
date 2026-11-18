using System.ComponentModel.DataAnnotations;
using Partnr.Workspaces.Application.Interfaces.Services;

namespace Partnr.Workspaces.Infrastructure;

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Where the web app lives (no trailing path), e.g. http://localhost:3000. Links in emails point here.</summary>
    [Required]
    [Url]
    public string FrontendUrl { get; init; } = string.Empty;
}

internal sealed class AppLinks(Microsoft.Extensions.Options.IOptions<AppOptions> options) : IAppLinks
{
    public string InvitationUrl(string token) =>
        Url($"/invite/{Uri.EscapeDataString(token)}");

    public string Url(string path) => $"{options.Value.FrontendUrl.TrimEnd('/')}{path}";
}
