using ClientPortal.Domain.Enums;
using ClientPortal.Domain.Exceptions;

namespace ClientPortal.Domain.Entities;

/// <summary>A file the freelancer keeps with a project and can share with the client. Its own aggregate, with no approvals.</summary>
public sealed class ProjectFile : Entity
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const int FileNameMaxLength = 255;
    public const long MaxSizeBytes = 25L * 1024 * 1024;

    // Checked by extension only, and served from here: the browser's declared type is never trusted.
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".odt"] = "application/vnd.oasis.opendocument.text",
        [".ods"] = "application/vnd.oasis.opendocument.spreadsheet",
        [".odp"] = "application/vnd.oasis.opendocument.presentation",
        [".rtf"] = "application/rtf",
        [".txt"] = "text/plain",
        [".md"] = "text/markdown",
        [".csv"] = "text/csv",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif",
        [".svg"] = "image/svg+xml",
        [".heic"] = "image/heic",
        [".tif"] = "image/tiff",
        [".tiff"] = "image/tiff",
        [".bmp"] = "image/bmp",
        [".fig"] = "application/octet-stream",
        [".psd"] = "image/vnd.adobe.photoshop",
        [".ai"] = "application/illustrator",
        [".sketch"] = "application/octet-stream",
        [".xd"] = "application/octet-stream",
        [".indd"] = "application/octet-stream",
        [".eps"] = "application/postscript",
        [".mp4"] = "video/mp4",
        [".mov"] = "video/quicktime",
        [".webm"] = "video/webm",
        [".mp3"] = "audio/mpeg",
        [".wav"] = "audio/wav",
        [".ttf"] = "font/ttf",
        [".otf"] = "font/otf",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".zip"] = "application/zip",
    };

    private ProjectFile()
    {
    }

    private ProjectFile(Guid workspaceId, Guid projectId, DateTime createdAtUtc)
    {
        WorkspaceId = workspaceId;
        ProjectId = projectId;
        CreatedAtUtc = createdAtUtc;
        Status = ProjectFileStatus.Draft;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid ProjectId { get; private set; }

    /// <summary>What people see in the lists; starts as the file name and can be changed.</summary>
    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>The name of the uploaded file, as the download is called. Never changes.</summary>
    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public ProjectFileStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ArchivedAtUtc { get; private set; }

    public bool IsArchived => ArchivedAtUtc is not null;

    /// <summary>Built from ids only, never from the file name the user chose.</summary>
    public string StorageKey => $"{WorkspaceId}/{ProjectId}/{Id}";

    public static ProjectFile Create(
        Guid workspaceId,
        Guid projectId,
        string? name,
        string? description,
        string fileName,
        long sizeBytes)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new DomainException("A file belongs to a workspace.");
        }

        if (projectId == Guid.Empty)
        {
            throw new DomainException("A file belongs to a project.");
        }

        var cleanFileName = CleanFileName(fileName);
        var extension = Path.GetExtension(cleanFileName);
        if (!ContentTypes.TryGetValue(extension, out var contentType))
        {
            throw new DomainException("Files of this type are not allowed.");
        }

        if (sizeBytes <= 0)
        {
            throw new DomainException("The file is empty.");
        }

        if (sizeBytes > MaxSizeBytes)
        {
            throw new DomainException($"The file is larger than {MaxSizeBytes / (1024 * 1024)} MB.");
        }

        var file = new ProjectFile(workspaceId, projectId, DateTime.UtcNow)
        {
            FileName = cleanFileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
        };
        file.UpdateDetails(string.IsNullOrWhiteSpace(name) ? cleanFileName : name, description);

        return file;
    }

    public void UpdateDetails(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("File name is required.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void Share() => Status = ProjectFileStatus.Shared;

    public void Unshare() => Status = ProjectFileStatus.Draft;

    /// <summary>Also back to Draft, so a restored file is never visible to the client without sharing it again.</summary>
    public void Archive()
    {
        ArchivedAtUtc ??= DateTime.UtcNow;
        Status = ProjectFileStatus.Draft;
    }

    public void Restore() => ArchivedAtUtc = null;

    // Browsers send the bare name, but some clients send a path; keep only the last segment and drop control characters.
    private static string CleanFileName(string? fileName)
    {
        var lastSegment = (fileName ?? string.Empty).Replace('\\', '/').Split('/')[^1];
        var cleaned = new string(lastSegment.Where(c => !char.IsControl(c)).ToArray()).Trim();

        if (cleaned.Length == 0)
        {
            throw new DomainException("A file is required.");
        }

        if (cleaned.Length > FileNameMaxLength)
        {
            throw new DomainException($"The file name is longer than {FileNameMaxLength} characters.");
        }

        return cleaned;
    }
}
