import { DownloadIcon } from "lucide-react";
import { FileTypeIcon } from "@/components/files/FileTypeIcon";
import { buttonVariants } from "@/components/ui/button";
import { formatFileSize } from "@/lib/format";
import type { PortalProjectFile } from "@/lib/portal/types";

/** Hidden when nothing is shared, like the deliverables. Each file is a plain download link: the API answers with an attachment. */
export function PortalFileList({ projectId, files }: { projectId: string; files: PortalProjectFile[] }) {
  if (files.length === 0) {
    return null;
  }

  return (
    <section aria-labelledby="files-title">
      <h2 id="files-title" className="mb-4 font-heading text-xl font-semibold">
        Files
      </h2>
      <ul className="flex flex-col divide-y rounded-lg border">
        {files.map((file) => (
          <li key={file.id} className="flex flex-wrap items-start justify-between gap-3 p-4">
            <div className="flex min-w-0 gap-3">
              <FileTypeIcon fileName={file.fileName} />
              <div className="min-w-0">
                <p className="mb-1.5 break-all text-sm text-muted-foreground">
                  {file.fileName} · {formatFileSize(file.sizeBytes)}
                </p>
                <p className="break-words font-medium">{file.name}</p>
                {file.description ? <p className="mt-1 whitespace-pre-line text-sm text-muted-foreground">{file.description}</p> : null}
              </div>
            </div>
            <a
              href={`/api/portal/projects/${projectId}/files/${file.id}/download`}
              download
              className={buttonVariants({ variant: "outline", size: "sm" })}
              aria-label={`Download ${file.name}`}
            >
              <DownloadIcon aria-hidden />
              Download
            </a>
          </li>
        ))}
      </ul>
    </section>
  );
}
