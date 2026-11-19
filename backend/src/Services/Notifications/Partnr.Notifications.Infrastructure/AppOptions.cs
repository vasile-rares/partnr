using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Partnr.Notifications.Application.Interfaces.Services;

namespace Partnr.Notifications.Infrastructure;

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Where the web app lives (no trailing path), e.g. http://localhost:3000. Links in emails point here.</summary>
    [Required]
    [Url]
    public string FrontendUrl { get; init; } = string.Empty;
}

internal sealed class AppLinks(IOptions<AppOptions> options) : IAppLinks
{
    public string Url(string path) => $"{options.Value.FrontendUrl.TrimEnd('/')}{path}";
}
