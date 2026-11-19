using Partnr.Contracts.Events;
using Partnr.Notifications.Application.Interfaces.Repositories;
using Partnr.Notifications.Application.Interfaces.Services;
using Partnr.Notifications.Domain.Entities;

namespace Partnr.Notifications.Application.Services;

internal sealed class NotificationEventService(
    INotificationRepository notifications,
    IInboxPublisher inbox,
    IConversationPublisher conversations,
    IUnitOfWork unitOfWork,
    TimeProvider time) : INotificationEventService
{
    public async Task ApplyAsync(ApprovalRequested requested, CancellationToken cancellationToken)
    {
        // Taken back before this arrived: there is nothing to tell any more.
        if (await notifications.IsWithdrawnAsync(requested.ApprovalRequestId, cancellationToken))
        {
            return;
        }

        await AddAsync(
            Notification.ApprovalRequested(
                requested.RecipientUserId,
                requested.RecipientEmail,
                requested.RecipientName,
                requested.WorkspaceId,
                requested.WorkspaceName,
                requested.ProjectId,
                requested.ProjectName,
                requested.DeliverableTitle,
                requested.ApprovalRequestId,
                Now()),
            cancellationToken);
    }

    public Task ApplyAsync(ApprovalDecided decided, CancellationToken cancellationToken) =>
        AddAsync(
            Notification.ApprovalDecided(
                decided.Decision == ApprovalDecision.Approved,
                decided.RecipientUserId,
                decided.RecipientEmail,
                decided.RecipientName,
                decided.WorkspaceId,
                decided.WorkspaceName,
                decided.ProjectId,
                decided.ProjectName,
                decided.DeliverableTitle,
                decided.ApprovalRequestId,
                decided.ActorName,
                Now()),
            cancellationToken);

    public Task ApplyAsync(InvitationAccepted accepted, CancellationToken cancellationToken) =>
        AddAsync(
            Notification.InvitationAccepted(
                accepted.RecipientUserId,
                accepted.RecipientEmail,
                accepted.RecipientName,
                accepted.WorkspaceId,
                accepted.WorkspaceName,
                accepted.ClientId,
                accepted.ActorName,
                Now()),
            cancellationToken);

    public async Task WithdrawAsync(Guid approvalRequestId, CancellationToken cancellationToken)
    {
        var notification = await notifications.GetApprovalRequestedAsync(approvalRequestId, cancellationToken);
        if (notification is null)
        {
            // Either it was never here (the withdrawal came first) or it is already gone: remember, so a late "asked" says nothing.
            if (!await notifications.IsWithdrawnAsync(approvalRequestId, cancellationToken))
            {
                notifications.AddWithdrawn(WithdrawnRequest.Create(approvalRequestId, Now()));
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        notification.MarkObsolete(Now());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await inbox.InboxChangedAsync([notification.RecipientUserId], cancellationToken);
    }

    public Task ApplyAsync(ConversationChanged changed, CancellationToken cancellationToken) =>
        conversations.ConversationChangedAsync(changed.UserIds, changed.ProjectId, cancellationToken);

    // The inbox is told after the save that worked, so a browser that reloads at once finds the notification.
    private async Task AddAsync(Notification notification, CancellationToken cancellationToken)
    {
        if (await notifications.ExistsAsync(notification.DedupeKey, cancellationToken))
        {
            return;
        }

        notifications.Add(notification);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await inbox.InboxChangedAsync([notification.RecipientUserId], cancellationToken);
    }

    private DateTime Now() => time.GetUtcNow().UtcDateTime;
}
