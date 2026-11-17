import { DownloadIcon } from "lucide-react";
import { FileTypeIcon } from "@/components/files/FileTypeIcon";
import { DividedList } from "@/components/layout/DividedList";
import { Section } from "@/components/layout/Section";
import { buttonVariants } from "@/components/ui/button";
import type { ClientFile } from "@/lib/files/types";
import { formatFileSize } from "@/lib/format";

/** Hidden when nothing is shared, like the deliverables. Each file is a plain download link: the API answers with an attachment. */
export function ClientFileList({ workspaceId, projectId, files }: { workspaceId: string; projectId: string; files: ClientFile[] }) {
  if (files.length === 0) {
    return null;
  }

  return (
    <Section title="Files" className="client-files">
      <DividedList className="client-files-list">
        {files.map((file) => (
          <li key={file.id} className="client-files-item flex flex-wrap items-start justify-between gap-3 p-4">
            <div className="client-files-main flex min-w-0 gap-3">
              <FileTypeIcon fileName={file.fileName} />
              <div className="client-files-info min-w-0">
                <p className="mb-1.5 break-all text-sm text-muted-foreground">
                  {file.fileName} · {formatFileSize(file.sizeBytes)}
                </p>
                <p className="break-words font-medium">{file.name}</p>
                {file.description ? <p className="mt-1 whitespace-pre-line text-sm text-muted-foreground">{file.description}</p> : null}
              </div>
            </div>
            <a
              href={`/api/workspaces/${workspaceId}/projects/${projectId}/files/${file.id}/download`}
              download
              className={buttonVariants({ variant: "outline", size: "sm" })}
              aria-label={`Download ${file.name}`}
            >
              <DownloadIcon aria-hidden />
              Download
            </a>
          </li>
        ))}
      </DividedList>
    </Section>
  );
}
