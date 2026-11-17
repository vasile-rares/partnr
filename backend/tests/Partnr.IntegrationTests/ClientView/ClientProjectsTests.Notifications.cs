using System.Net;
using System.Net.Http.Json;
using Partnr.Application.DTOs.Email;
using Partnr.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Partnr.IntegrationTests.ClientView;

// The emails that approvals and invitations send. The worker is off in these tests: they run the email service themselves.
public sealed partial class ClientProjectsTests
{
    [Fact]
    public async Task AskingForApproval_EmailsTheClient_WithALinkToTheDeliverablesTab()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");

        await studio.RequestApproval(project, mockup, "Check the mobile layout.");

        var message = Assert.Single(await EmailsTo(elena.Email, "asked for your approval"));
        Assert.Equal("Studio asked for your approval: Mockup", message.Subject);
        Assert.Contains($"/w/{studio.WorkspaceId}/projects/{project.Id}?tab=deliverables", message.TextBody);
        Assert.StartsWith("Hi Elena,", message.TextBody);
    }

    [Fact]
    public async Task TheClientsAnswer_EmailsTheMemberWhoAsked_NotTheClient()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        var approved = await studio.RequestApproval(project, mockup, null);
        Assert.Equal(HttpStatusCode.OK, (await elena.Http.PostAsJsonAsync(ApprovalUrl(studio, project, mockup, approved.Id, "approve"), new { })).StatusCode);
        var again = await studio.RequestApproval(project, mockup, "Once more.");
        Assert.Equal(
            HttpStatusCode.OK,
            (await elena.Http.PostAsJsonAsync(ApprovalUrl(studio, project, mockup, again.Id, "request-changes"), new { message = "Bigger logo." })).StatusCode);

        var approvedMail = Assert.Single(await EmailsTo(studio.Email, "approved Mockup"));
        var changesMail = Assert.Single(await EmailsTo(studio.Email, "requested changes to Mockup"));

        Assert.Equal("Elena approved Mockup", approvedMail.Subject);
        Assert.Contains($"/w/{studio.WorkspaceId}/projects/{project.Id}?tab=deliverables", approvedMail.TextBody);
        Assert.Equal("Elena requested changes to Mockup", changesMail.Subject);
        Assert.DoesNotContain("Bigger logo.", changesMail.TextBody);
        Assert.DoesNotContain("Bigger logo.", changesMail.HtmlBody);
    }

    [Fact]
    public async Task AcceptingAnInvitation_EmailsTheMemberWhoInvited()
    {
        var studio = await Studio();

        var elena = await studio.AddClient("Elena");

        var message = Assert.Single(await EmailsTo(studio.Email, "joined your client portal"));
        Assert.Equal("Elena joined your client portal", message.Subject);
        Assert.Contains($"/w/{studio.WorkspaceId}/clients/{elena.ClientId}", message.TextBody);
    }

    [Fact]
    public async Task ARequestTakenBackBeforeTheEmailLeft_IsNeverEmailed()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        var taken = await studio.RequestApproval(project, mockup, null);
        var withdrawn = await studio.Http.PostAsync($"{studio.DeliverablesUrl(project)}/{mockup.Id}/approvals/{taken.Id}/withdraw", null);
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);

        // A second request is the control: once its email is out, the first one had every chance to follow it.
        await studio.RequestApproval(project, mockup, "The one that counts.");
        await EmailsTo(elena.Email, "asked for your approval");

        Assert.Single(factory.Emails.SentTo(elena.Email), m => m.Subject.Contains("asked for your approval"));
    }

    [Fact]
    public async Task UnsharingOrArchivingTheDeliverable_StopsTheEmailOfItsOpenRequest()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        var other = await studio.AddDeliverable(project, "Other", null, share: true);
        await studio.RequestApproval(project, mockup, null);
        await studio.RequestApproval(project, other, null);
        Assert.Equal(HttpStatusCode.OK, (await studio.Http.PostAsync($"{studio.DeliverablesUrl(project)}/{mockup.Id}/unshare", null)).StatusCode);

        var messages = await EmailsTo(elena.Email, "asked for your approval");

        Assert.Equal("Studio asked for your approval: Other", Assert.Single(messages).Subject);
    }

    [Fact]
    public async Task TwoWorkersRunningTogether_NeverSendTheSameEmailTwice()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        await studio.RequestApproval(project, mockup, null);

        await Task.WhenAll(SendNotificationEmails(), SendNotificationEmails(), SendNotificationEmails());
        await EmailsTo(elena.Email, "asked for your approval");
        await SendNotificationEmails();

        Assert.Single(factory.Emails.SentTo(elena.Email), m => m.Subject.Contains("asked for your approval"));
    }

    private async Task SendNotificationEmails()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<INotificationEmailService>().SendDueAsync(CancellationToken.None);
    }

    // The tests share one database, so another test's call may have claimed this email a moment ago: keep running the service until it shows up.
    private async Task<IReadOnlyList<EmailMessage>> EmailsTo(string address, string subjectPart)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (true)
        {
            await SendNotificationEmails();
            var found = factory.Emails.SentTo(address).Where(m => m.Subject.Contains(subjectPart)).ToList();
            if (found.Count > 0 || DateTime.UtcNow > deadline)
            {
                return found;
            }

            await Task.Delay(50);
        }
    }
}
