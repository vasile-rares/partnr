using Partnr.Application.DTOs.Messages;

namespace Partnr.Application.Interfaces.Services;

/// <summary>The project conversations, for both sides: a member of the workspace, or a client of it (only on the projects shared with them).</summary>
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
