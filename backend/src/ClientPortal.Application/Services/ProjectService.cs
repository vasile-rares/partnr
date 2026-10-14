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

        // [Required] guarantees a value; the lookup is scoped to the workspace, so another workspace's client is not found.
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

    // An archived project is read-only, so it cannot change behind the owner's back; restoring it makes it editable again.
    private static void EnsureNotArchived(Project project)
    {
        if (project.IsArchived)
        {
            throw new ConflictException("This project is archived. Restore it before making changes.");
        }
    }
}
