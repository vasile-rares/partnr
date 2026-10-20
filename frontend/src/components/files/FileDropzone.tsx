"use client";

import { UploadCloudIcon } from "lucide-react";
import { useRef, useState, type DragEvent } from "react";
import { FileTypeIcon } from "@/components/files/FileTypeIcon";
import { cn } from "@/lib/utils";
import { formatFileSize } from "@/lib/format";

/** One file, picked by clicking or dropped on the area. The real input (visually hidden) is what the form submits. */
export function FileDropzone({
  name,
  describedBy,
  hint,
  file,
  onFileChange,
}: {
  name: string;
  describedBy: string;
  hint: string;
  file: File | null;
  onFileChange: (file: File | null) => void;
}) {
  const input = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);

  function onDrop(event: DragEvent<HTMLLabelElement>) {
    event.preventDefault();
    setDragging(false);
    const dropped = event.dataTransfer.files;
    if (input.current && dropped.length > 0) {
      const transfer = new DataTransfer();
      transfer.items.add(dropped[0]);
      input.current.files = transfer.files;
      onFileChange(dropped[0]);
    }
  }

  return (
    <label
      onDragOver={(event) => {
        event.preventDefault();
        setDragging(true);
      }}
      onDragLeave={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
          setDragging(false);
        }
      }}
      onDrop={onDrop}
      className={cn(
        "flex cursor-pointer flex-col items-center gap-2 rounded-lg border border-dashed px-4 py-6 text-center transition-colors hover:bg-muted/50 has-focus-visible:border-ring has-focus-visible:ring-3 has-focus-visible:ring-ring/50",
        dragging && "border-primary bg-muted/50",
      )}
    >
      <input
        ref={input}
        type="file"
        name={name}
        required
        aria-label="File"
        aria-describedby={describedBy}
        className="sr-only"
        onChange={(event) => onFileChange(event.target.files?.[0] ?? null)}
      />
      {file ? (
        <>
          <FileTypeIcon fileName={file.name} contentType={file.type} />
          <span className="break-all text-sm font-medium">{file.name}</span>
          <span className="text-sm text-muted-foreground">{formatFileSize(file.size)} · Click or drop to replace</span>
        </>
      ) : (
        <>
          <UploadCloudIcon className="size-6 text-muted-foreground" aria-hidden />
          <span className="text-sm font-medium">Drop a file here, or click to browse</span>
        </>
      )}
      <span id={describedBy} className="text-sm text-muted-foreground">
        {hint}
      </span>
    </label>
  );
}
