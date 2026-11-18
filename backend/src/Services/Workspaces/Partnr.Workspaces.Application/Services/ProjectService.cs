using Partnr.Contracts.Events;
using Partnr.Workspaces.Application.Authorization;
using Partnr.Workspaces.Application.DTOs;
using Partnr.Workspaces.Application.DTOs.ClientView;
using Partnr.Workspaces.Application.DTOs.Projects;
using Partnr.Workspaces.Application.Exceptions;
using Partnr.Workspaces.Application.Interfaces.Repositories;
using Partnr.Workspaces.Application.Interfaces.Services;
using Partnr.Workspaces.Application.Mappings;
using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Domain.Enums;

namespace Partnr.Workspaces.Application.Services;

internal sealed class ProjectService(
    IProjectRepository projects,
    ProjectAccess projectAccess,
    IClientRepository clients,
    ProjectResponseBuilder responses,
    ClientProjectResponseBuilder clientView,
    WorkspaceAccess access,
    ClientAccess clientAccess,
    CallerAccess callers,
    IEventPublisher events,
    IUnitOfWork unitOfWork) : IProjectService
{
    public async Task<ProjectResponse> CreateAsync(
        Guid workspaceId,
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);

        var clientId = request.ClientId!.Value;
        var client = await clients.GetByIdAsync(workspaceId, clientId, cancellationToken)
            ?? throw new NotFoundException($"The client with id '{clientId}' was not found.");
        if (client.IsArchived)
        {
            throw new ConflictException("This client is archived. Restore the client before starting a project for them.");
        }

        var project = Project.Create(workspaceId, client.Id, request.Name, request.Description, request.StartDate, request.DueDate);
        projects.Add(project);
        await events.PublishAsync(new ProjectCreated(project.Id, workspaceId, client.Id, project.Name, project.CreatedAtUtc), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(project, cancellationToken);
    }

    public async Task<RoleResponse<IReadOnlyList<ProjectResponse>, IReadOnlyList<ClientProjectSummaryResponse>>> ListAsync(
        Guid workspaceId,
        bool includeArchived,
        Guid? clientId,
        CancellationToken cancellationToken)
    {
        var caller = await callers.RequireAsync(workspaceId, cancellationToken);
        if (caller.IsClient)
        {
            var shared = await clientAccess.ListProjectsAsync(caller.UserId, workspaceId, cancellationToken);

            return RoleResponse<IReadOnlyList<ProjectResponse>, IReadOnlyList<ClientProjectSummaryResponse>>.ForClient(
                await clientView.SummariesAsync(shared, caller.Workspace, cancellationToken));
        }

        var list = await projects.ListAsync(workspaceId, includeArchived, clientId, cancellationToken);

        return RoleResponse<IReadOnlyList<ProjectResponse>, IReadOnlyList<ClientProjectSummaryResponse>>.ForMember(
            await responses.BuildAsync(workspaceId, list, cancellationToken));
    }

    public async Task<RoleResponse<ProjectResponse, ClientProjectResponse>> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken)
    {
        var caller = await callers.RequireAsync(workspaceId, cancellationToken);
        if (caller.IsClient)
        {
            var shared = await clientAccess.FindProjectInWorkspaceAsync(caller.UserId, workspaceId, id, cancellationToken)
                ?? throw new NotFoundException($"The project with id '{id}' was not found.");

            return RoleResponse<ProjectResponse, ClientProjectResponse>.ForClient(
                await clientView.BuildAsync(shared, caller.Workspace, cancellationToken));
        }

        var project = await projectAccess.RequireProjectAsync(workspaceId, id, cancellationToken);

        return RoleResponse<ProjectResponse, ClientProjectResponse>.ForMember(await responses.BuildAsync(project, cancellationToken));
    }

    public async Task<ProjectResponse> UpdateAsync(
        Guid workspaceId,
        Guid id,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var project = await projectAccess.RequireEditableProjectAsync(workspaceId, id, cancellationToken);

        project.UpdateDetails(request.Name, request.Description, request.StartDate, request.DueDate);
        project.ChangeStatus(Enum.Parse<ProjectStatus>(request.Status)); // [AllowedValues] already checked the name
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(project, cancellationToken);
    }

    public Task<ProjectResponse> AddStageAsync(
        Guid workspaceId,
        Guid projectId,
        CreateStageRequest request,
        CancellationToken cancellationToken) =>
        ChangeStagesAsync(
            workspaceId,
            projectId,
            project => project.AddStage(request.Name, request.Description, request.DueDate),
            cancellationToken);

    public Task<ProjectResponse> UpdateStageAsync(
        Guid workspaceId,
        Guid projectId,
        Guid stageId,
        UpdateStageRequest request,
        CancellationToken cancellationToken) =>
        ChangeStagesAsync(
            workspaceId,
            projectId,
            project => project.UpdateStage(
                RequireStage(project, stageId),
                request.Name,
                request.Description,
                request.DueDate,
                Enum.Parse<StageStatus>(request.Status)), // [AllowedValues] already checked the name
            cancellationToken);

    public Task<ProjectResponse> MoveStageAsync(
        Guid workspaceId,
        Guid projectId,
        Guid stageId,
        MoveStageRequest request,
        CancellationToken cancellationToken) =>
        ChangeStagesAsync(
            workspaceId,
            projectId,
            project => project.MoveStage(RequireStage(project, stageId), up: request.Direction == "up"),
            cancellationToken);

    public Task<ProjectResponse> RemoveStageAsync(
        Guid workspaceId,
        Guid projectId,
        Guid stageId,
        CancellationToken cancellationToken) =>
        ChangeStagesAsync(workspaceId, projectId, project => project.RemoveStage(RequireStage(project, stageId)), cancellationToken);

    public async Task<ProjectResponse> ArchiveAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireOwnerAsync(workspaceId, cancellationToken);
        var project = await projectAccess.RequireProjectAsync(workspaceId, id, cancellationToken);

        project.Archive();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(project, cancellationToken);
    }

    public async Task<ProjectResponse> RestoreAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireOwnerAsync(workspaceId, cancellationToken);
        var project = await projectAccess.RequireProjectAsync(workspaceId, id, cancellationToken);

        project.Restore();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(project, cancellationToken);
    }

    // Every stage change has the same shape: find the project, refuse if archived, change it, save once, answer with the whole project.
    private async Task<ProjectResponse> ChangeStagesAsync(
        Guid workspaceId,
        Guid projectId,
        Action<Project> change,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var project = await projectAccess.RequireEditableProjectAsync(workspaceId, projectId, cancellationToken);

        change(project);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(project, cancellationToken);
    }

    /// <summary>Returns the id once it is known to belong to the project, so an unknown or foreign stage is a 404, not a 400.</summary>
    private static Guid RequireStage(Project project, Guid stageId) =>
        project.HasStage(stageId) ? stageId : throw new NotFoundException($"The stage with id '{stageId}' was not found.");
}
