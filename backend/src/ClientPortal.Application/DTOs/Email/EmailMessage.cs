namespace ClientPortal.Application.DTOs.Email;

/// <summary>One email to one recipient. The plain-text body is always sent; the HTML body is an optional richer version.</summary>
public sealed record EmailMessage(string To, string Subject, string TextBody, string? HtmlBody = null);
