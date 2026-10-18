using ClientPortal.Application.DTOs.Approvals;
using ClientPortal.Application.DTOs.Portal;

namespace ClientPortal.Application.Interfaces.Services;

/// <summary>
/// The client's answer to an approval request. Anything the client may not see (a draft project, someone else's project,
/// an unshared deliverable, an unknown round) is NotFound; a round no longer open is a Conflict.
/// </summary>
public interface IPortalApprovalService
{
    Task<PortalDeliverableResponse> ApproveAsync(
        Guid projectId,
        Guid deliverableId,
        Guid approvalId,
        ApproveRequest request,
        CancellationToken cancellationToken);

    Task<PortalDeliverableResponse> RequestChangesAsync(
        Guid projectId,
        Guid deliverableId,
        Guid approvalId,
        RequestChangesRequest request,
        CancellationToken cancellationToken);
}
