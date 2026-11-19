using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Partnr.Integrations.IntegrationTests.Google;

internal sealed class TestHostEnvironment(string name = "Development") : IHostEnvironment
{
    public string EnvironmentName { get; set; } = name;

    public string ApplicationName { get; set; } = "Partnr";

    public string ContentRootPath { get; set; } = string.Empty;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
