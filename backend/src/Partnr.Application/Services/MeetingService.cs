using Partnr.Application.Authorization;
using Partnr.Application.DTOs.Meetings;
using Partnr.Application.Exceptions;
using Partnr.Application.Interfaces.Repositories;
using Partnr.Application.Interfaces.Services;
using Partnr.Application.Mappings;
using Partnr.Domain.Entities;
using Partnr.Domain.Enums;

namespace Partnr.Application.Services;

internal sealed class MeetingService(
    IMeetingRepository meetings,
    IClientRepository clients,
    IProjectRepository projects,
    IGoogleConnectionRepository googleConnections,
    MeetingResponseBuilder responses,
    WorkspaceAccess access,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IMeetingService
{
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
        var google = await googleConnections.GetForUserAsync(member.UserId, cancellationToken);
        if (google is null || google.Status != GoogleConnectionStatus.Connected)
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
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(meeting, cancellationToken);
    }

    public async Task<IReadOnlyList<MeetingResponse>> ListAsync(
        Guid workspaceId,
        bool includeCancelled,
        Guid? clientId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);

        return await responses.BuildAsync(
            workspaceId,
            await meetings.ListAsync(workspaceId, includeCancelled, clientId, projectId, cancellationToken),
            cancellationToken);
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
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(meeting, cancellationToken);
    }

    public async Task<MeetingResponse> CancelAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var meeting = await RequireOpenMeetingAsync(workspaceId, id, cancellationToken);

        meeting.Cancel(Now());
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

    private DateTime Now() => time.GetUtcNow().UtcDateTime;
}
