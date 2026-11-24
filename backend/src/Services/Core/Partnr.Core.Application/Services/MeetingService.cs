using Partnr.Contracts.Events;
using Partnr.Core.Application.Authorization;
using Partnr.Core.Application.DTOs;
using Partnr.Core.Application.DTOs.Meetings;
using Partnr.Core.Application.Exceptions;
using Partnr.Core.Application.Interfaces.Repositories;
using Partnr.Core.Application.Interfaces.Services;
using Partnr.Core.Application.Mappings;
using Partnr.Core.Domain.Entities;
using Partnr.Core.Domain.Enums;

namespace Partnr.Core.Application.Services;

internal sealed class MeetingService(
    IMeetingRepository meetings,
    IClientRepository clients,
    IProjectRepository projects,
    IGoogleConnectionStatus googleConnection,
    IEventPublisher events,
    MeetingResponseBuilder responses,
    WorkspaceAccess access,
    ClientAccess clientAccess,
    CallerAccess callers,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IMeetingService
{
    /// <summary>Cancelled meetings stay visible to a client for a while, so they see what was called off; older history is not the client's business.</summary>
    private static readonly TimeSpan ClientHistory = TimeSpan.FromDays(30);

    public async Task<MeetingResponse> CreateAsync(Guid workspaceId, CreateMeetingRequest request, CancellationToken cancellationToken)
    {
        var member = await access.RequireMemberAsync(workspaceId, cancellationToken);

        var client = await clients.GetByIdAsync(workspaceId, request.ClientId, cancellationToken)
            ?? throw new NotFoundException($"The client with id '{request.ClientId}' was not found.");
        if (client.IsArchived)
        {
            throw new ConflictException("This client is archived. Restore the client before scheduling a meeting.");
        }

        await RequireProjectOfAsync(workspaceId, client.Id, request.ProjectId, cancellationToken);

        // The invitation and the reminders are Google's, so a meeting without a connected account would reach nobody.
        if (!await googleConnection.IsConnectedAsync(member.UserId, cancellationToken))
        {
            throw new ConflictException("Connect your Google account first: the client is invited, and reminded, through your Google Calendar.");
        }

        var meeting = Meeting.Schedule(
            workspaceId,
            client.Id,
            member.UserId,
            request.ProjectId,
            request.Title,
            request.Description,
            request.StartsAt!.Value.UtcDateTime,
            request.EndsAt!.Value.UtcDateTime,
            Now());
        meetings.Add(meeting);
        await events.PublishAsync(Changed(meeting, client.Email), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(meeting, cancellationToken);
    }

    public async Task<RoleResponse<IReadOnlyList<MeetingResponse>, IReadOnlyList<ClientMeetingResponse>>> ListAsync(
        Guid workspaceId,
        bool includeCancelled,
        Guid? clientId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        var caller = await callers.RequireAsync(workspaceId, cancellationToken);
        if (caller.IsClient)
        {
            return RoleResponse<IReadOnlyList<MeetingResponse>, IReadOnlyList<ClientMeetingResponse>>.ForClient(
                await ListForClientAsync(caller, cancellationToken));
        }

        return RoleResponse<IReadOnlyList<MeetingResponse>, IReadOnlyList<ClientMeetingResponse>>.ForMember(
            await responses.BuildAsync(
                workspaceId,
                await meetings.ListAsync(workspaceId, includeCancelled, clientId, projectId, cancellationToken),
                cancellationToken));
    }

    private async Task<IReadOnlyList<ClientMeetingResponse>> ListForClientAsync(Caller caller, CancellationToken cancellationToken)
    {
        var workspaceId = caller.Workspace.Id;
        var list = await meetings.ListForClientAccountAsync(workspaceId, caller.UserId, Now() - ClientHistory, cancellationToken);
        // The same door as the client's projects: a project in Planning or archived is not theirs to see, so it is not named.
        var open = (await clientAccess.ListProjectsAsync(caller.UserId, workspaceId, cancellationToken)).ToDictionary(p => p.Id, p => p.Name);

        return list.Select(m => new ClientMeetingResponse(
            m.Id,
            m.ProjectId is { } projectId && open.TryGetValue(projectId, out var name) ? new MeetingProjectResponse(projectId, name) : null,
            m.Title,
            m.Description,
            m.StartsAtUtc,
            m.EndsAtUtc,
            m.Status.ToString(),
            m.IsCancelled ? null : m.MeetUrl)).ToList();
    }

    public async Task<MeetingResponse> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);

        return await responses.BuildAsync(await RequireMeetingAsync(workspaceId, id, cancellationToken), cancellationToken);
    }

    public async Task<MeetingResponse> UpdateAsync(Guid workspaceId, Guid id, SaveMeetingRequest request, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var meeting = await RequireOpenMeetingAsync(workspaceId, id, cancellationToken);

        var client = await clients.GetByIdAsync(workspaceId, meeting.ClientId, cancellationToken);
        if (client is null || client.IsArchived)
        {
            throw new ConflictException("This meeting's client is archived. Restore the client before changing the meeting.");
        }

        await RequireProjectOfAsync(workspaceId, meeting.ClientId, request.ProjectId, cancellationToken);

        meeting.Update(request.ProjectId, request.Title, request.Description, request.StartsAt!.Value.UtcDateTime, request.EndsAt!.Value.UtcDateTime, Now());
        await events.PublishAsync(Changed(meeting, client.Email), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(meeting, cancellationToken);
    }

    public async Task<MeetingResponse> CancelAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var meeting = await RequireOpenMeetingAsync(workspaceId, id, cancellationToken);

        meeting.Cancel(Now());
        var client = await clients.GetByIdAsync(workspaceId, meeting.ClientId, cancellationToken)
            ?? throw new NotFoundException($"The client with id '{meeting.ClientId}' was not found.");
        await events.PublishAsync(Changed(meeting, client.Email), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(meeting, cancellationToken);
    }

    public async Task<MeetingResponse> RetrySyncAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var meeting = await RequireMeetingAsync(workspaceId, id, cancellationToken);
        if (meeting.SyncStatus != MeetingSyncStatus.Failed || meeting.HasEndedAt(Now()))
        {
            throw new ConflictException("There is nothing to retry.");
        }

        // A cancelled meeting can be retried too: its event may still be in Google.
        meeting.RetrySync(Now());
        await events.PublishAsync(new MeetingSyncRetryRequested(meeting.Id), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(meeting, cancellationToken);
    }

    // A cancelled meeting and one that already took place are history: nothing about them changes.
    private async Task<Meeting> RequireOpenMeetingAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken)
    {
        var meeting = await RequireMeetingAsync(workspaceId, id, cancellationToken);
        if (meeting.IsCancelled)
        {
            throw new ConflictException("This meeting was cancelled.");
        }

        if (meeting.HasEndedAt(Now()))
        {
            throw new ConflictException("This meeting already took place.");
        }

        return meeting;
    }

    private async Task<Meeting> RequireMeetingAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        await meetings.GetByIdAsync(workspaceId, id, cancellationToken)
            ?? throw new NotFoundException($"The meeting with id '{id}' was not found.");

    // The project, when there is one, is of the same workspace, belongs to the meeting's client and is not archived.
    private async Task RequireProjectOfAsync(Guid workspaceId, Guid clientId, Guid? projectId, CancellationToken cancellationToken)
    {
        if (projectId is null)
        {
            return;
        }

        var project = await projects.GetByIdAsync(workspaceId, projectId.Value, cancellationToken)
            ?? throw new NotFoundException($"The project with id '{projectId}' was not found.");
        if (project.ClientId != clientId)
        {
            throw new ConflictException("This project belongs to another client.");
        }

        if (project.IsArchived)
        {
            throw new ConflictException("This project is archived. Restore it before scheduling a meeting about it.");
        }
    }

    // Everything the calendar needs, so the Integrations service never has to ask back.
    private static MeetingChanged Changed(Meeting meeting, string clientEmail) =>
        new(meeting.Id, meeting.Version, meeting.OrganizerUserId, meeting.Title, meeting.Description, meeting.StartsAtUtc, meeting.EndsAtUtc, clientEmail, meeting.IsCancelled);

    private DateTime Now() => time.GetUtcNow().UtcDateTime;
}
