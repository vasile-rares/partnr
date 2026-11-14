using Partnr.Application.DTOs.Messages;

namespace Partnr.Application.Interfaces.Services;

/// <summary>The firm's side of the project conversations. Any member reads and writes.</summary>
public interface IMessageService
{
    Task<IReadOnlyList<ConversationSummaryResponse>> ListAsync(Guid workspaceId, CancellationToken cancellationToken);

    Task<UnreadMessagesResponse> UnreadCountAsync(Guid workspaceId, CancellationToken cancellationToken);

    Task<ConversationResponse> GetAsync(Guid workspaceId, Guid projectId, Guid? beforeId, CancellationToken cancellationToken);

    Task<MessageResponse> SendAsync(Guid workspaceId, Guid projectId, SaveMessageRequest request, CancellationToken cancellationToken);

    Task<MessageResponse> EditAsync(Guid workspaceId, Guid projectId, Guid messageId, SaveMessageRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid workspaceId, Guid projectId, Guid messageId, CancellationToken cancellationToken);

    Task MarkReadAsync(Guid workspaceId, Guid projectId, MarkConversationReadRequest request, CancellationToken cancellationToken);
}

/// <summary>The client's side: the same conversations, reached through the projects shared with them by that firm.</summary>
public interface IPortalMessageService
{
    Task<IReadOnlyList<ConversationSummaryResponse>> ListAsync(Guid workspaceId, CancellationToken cancellationToken);

    Task<UnreadMessagesResponse> UnreadCountAsync(Guid workspaceId, CancellationToken cancellationToken);

    Task<ConversationResponse> GetAsync(Guid workspaceId, Guid projectId, Guid? beforeId, CancellationToken cancellationToken);

    Task<MessageResponse> SendAsync(Guid workspaceId, Guid projectId, SaveMessageRequest request, CancellationToken cancellationToken);

    Task<MessageResponse> EditAsync(Guid workspaceId, Guid projectId, Guid messageId, SaveMessageRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid workspaceId, Guid projectId, Guid messageId, CancellationToken cancellationToken);

    Task MarkReadAsync(Guid workspaceId, Guid projectId, MarkConversationReadRequest request, CancellationToken cancellationToken);
}
