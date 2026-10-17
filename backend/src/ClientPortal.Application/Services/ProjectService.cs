using ClientPortal.Application.Authorization;
using ClientPortal.Application.DTOs.Projects;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Application.Mappings;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.Application.Services;

internal sealed class ProjectService(
    IProjectRepository projects,
    IClientRepository clients,
    ProjectResponseBuilder responses,
    WorkspaceAccess access,
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
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(project, cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectResponse>> ListAsync(
        Guid workspaceId,
        bool includeArchived,
        Guid? clientId,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);

        var list = await projects.ListAsync(workspaceId, includeArchived, clientId, cancellationToken);

        return await responses.BuildAsync(workspaceId, list, cancellationToken);
    }

    public async Task<ProjectResponse> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);

        var project = await RequireProjectAsync(workspaceId, id, cancellationToken);

        return await responses.BuildAsync(project, cancellationToken);
    }

    public async Task<ProjectResponse> UpdateAsync(
        Guid workspaceId,
        Guid id,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var project = await RequireProjectAsync(workspaceId, id, cancellationToken);
        EnsureNotArchived(project);

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
        var project = await RequireProjectAsync(workspaceId, id, cancellationToken);

        project.Archive();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(project, cancellationToken);
    }

    public async Task<ProjectResponse> RestoreAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireOwnerAsync(workspaceId, cancellationToken);
        var project = await RequireProjectAsync(workspaceId, id, cancellationToken);

        project.Restore();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(project, cancellationToken);
    }

    private async Task<Project> RequireProjectAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        await projects.GetByIdAsync(workspaceId, id, cancellationToken)
            ?? throw new NotFoundException($"The project with id '{id}' was not found.");

    // Every stage change has the same shape: find the project, refuse if archived, change it, save once, answer with the whole project.
    private async Task<ProjectResponse> ChangeStagesAsync(
        Guid workspaceId,
        Guid projectId,
        Action<Project> change,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var project = await RequireProjectAsync(workspaceId, projectId, cancellationToken);
        EnsureNotArchived(project);

        change(project);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(project, cancellationToken);
    }

    /// <summary>Returns the id once it is known to belong to the project, so an unknown or foreign stage is a 404, not a 400.</summary>
    private static Guid RequireStage(Project project, Guid stageId) =>
        project.HasStage(stageId) ? stageId : throw new NotFoundException($"The stage with id '{stageId}' was not found.");

    private static void EnsureNotArchived(Project project)
    {
        if (project.IsArchived)
        {
            throw new ConflictException("This project is archived. Restore it before making changes.");
        }
    }
}
