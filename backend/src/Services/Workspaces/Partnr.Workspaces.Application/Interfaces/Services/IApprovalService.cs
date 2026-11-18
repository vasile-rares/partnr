using Partnr.Workspaces.Application.DTOs.Approvals;
using Partnr.Workspaces.Application.DTOs.Deliverables;
using Partnr.Workspaces.Application.DTOs.ClientView;

namespace Partnr.Workspaces.Application.Interfaces.Services;

/// <summary>
/// Approvals: a member asks the client and can take the request back (each answers with the whole deliverable), and the client answers it.
/// The client's answer is for a client only (a member gets NotFound); anything the client may not see (a draft or someone else's
/// project, an unshared deliverable, an unknown round) is NotFound too, and a round no longer open is a Conflict.
/// </summary>
public interface IApprovalService
{
    Task<DeliverableResponse> RequestAsync(
        Guid workspaceId,
        Guid projectId,
        Guid deliverableId,
        RequestApprovalRequest request,
        CancellationToken cancellationToken);

    Task<DeliverableResponse> WithdrawAsync(
        Guid workspaceId,
        Guid projectId,
        Guid deliverableId,
        Guid approvalId,
        CancellationToken cancellationToken);

    Task<ClientDeliverableResponse> ApproveAsync(
        Guid workspaceId,
        Guid projectId,
        Guid deliverableId,
        Guid approvalId,
        ApproveRequest request,
        CancellationToken cancellationToken);

    Task<ClientDeliverableResponse> RequestChangesAsync(
        Guid workspaceId,
        Guid projectId,
        Guid deliverableId,
        Guid approvalId,
        RequestChangesRequest request,
        CancellationToken cancellationToken);
}
