using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClientPortal.Application.DTOs.Email;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Infrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClientPortal.IntegrationTests.Email;

/// <summary>
/// The other tests replace the email sender with a fake; this one checks the real SMTP sender against a throwaway Mailpit
/// container (the same catcher used in development), read back through Mailpit's HTTP API.
/// </summary>
public sealed class SmtpEmailSenderTests : IAsyncLifetime
{
    private const int SmtpPort = 1025;
    private const int ApiPort = 8025;

    private readonly IContainer mailpit = new ContainerBuilder("axllent/mailpit:v1.31.2")
        .WithPortBinding(SmtpPort, assignRandomHostPort: true)
        .WithPortBinding(ApiPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPath("/livez").ForPort(ApiPort)))
        .Build();

    public Task InitializeAsync() => mailpit.StartAsync();

    public Task DisposeAsync() => mailpit.DisposeAsync().AsTask();

    [Fact]
    public async Task Send_DeliversTheMessageOverSmtp()
    {
        var sender = BuildSender();

        await sender.SendAsync(
            new EmailMessage("elena@acme.example", "You're invited", "Open this link to join.", "<p>Open this link to join.</p>"),
            CancellationToken.None);

        using var http = new HttpClient { BaseAddress = new Uri($"http://{mailpit.Hostname}:{mailpit.GetMappedPublicPort(ApiPort)}") };
        var inbox = await http.GetFromJsonAsync<MailpitMessages>("/api/v1/messages");
        var message = Assert.Single(inbox!.Messages);
        Assert.Equal("You're invited", message.Subject);
        Assert.Equal("elena@acme.example", Assert.Single(message.To).Address);
        Assert.Equal("no-reply@client-portal.test", message.From.Address);
    }

    // Built from AddInfrastructure, so the test also covers the options binding and validation.
    private IEmailSender BuildSender()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=unused", // required by AddInfrastructure, never opened here
                ["Email:From"] = "Client Portal <no-reply@client-portal.test>",
                ["Email:SmtpHost"] = mailpit.Hostname,
                ["Email:SmtpPort"] = mailpit.GetMappedPublicPort(SmtpPort).ToString(),
                ["Email:UseTls"] = "false",
            })
            .Build();

        var services = new ServiceCollection().AddLogging().AddInfrastructure(configuration);
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IEmailSender>();
    }

    private sealed record MailpitMessages([property: JsonPropertyName("messages")] MailpitMessage[] Messages);

    private sealed record MailpitMessage(
        [property: JsonPropertyName("Subject")] string Subject,
        [property: JsonPropertyName("From")] MailpitAddress From,
        [property: JsonPropertyName("To")] MailpitAddress[] To);

    private sealed record MailpitAddress([property: JsonPropertyName("Address")] string Address);
}
