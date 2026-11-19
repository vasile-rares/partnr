using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Partnr.Notifications.Application.DTOs.Email;
using Partnr.Notifications.Application.Interfaces.Services;
using Partnr.Notifications.Infrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Partnr.Notifications.IntegrationTests.Email;

/// <summary>Checks the real SMTP sender against a throwaway Mailpit container.</summary>
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
        Assert.Equal("no-reply@partnr.test", message.From.Address);
    }

    private IEmailSender BuildSender()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=unused", // required by AddInfrastructure, never opened here
                ["App:FrontendUrl"] = "http://localhost:3000", // required by AddInfrastructure, never used here
                ["Email:From"] = "Partnr <no-reply@partnr.test>",
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
