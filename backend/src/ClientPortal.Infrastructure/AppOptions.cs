using System.ComponentModel.DataAnnotations;
using ClientPortal.Application.Interfaces.Services;

namespace ClientPortal.Infrastructure;

/// <summary>The "App" configuration section. Validated at startup, like the email settings.</summary>
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
        $"{options.Value.FrontendUrl.TrimEnd('/')}/invite/{Uri.EscapeDataString(token)}";
}
