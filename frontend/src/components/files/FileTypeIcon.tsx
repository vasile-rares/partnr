import { FileType, getFileType, getFileTypeCategory } from "@/components/reui/file-type";

/** A decorative icon: the file's name and size already say what it is in words. */
export function FileTypeIcon({ fileName, contentType }: { fileName: string; contentType?: string }) {
  const byName = getFileType(fileName);
  // The name first: browsers often report a design file or an archive as generic binary data.
  const type = getFileTypeCategory(byName) === "file" && contentType ? getFileType(contentType) : byName;

  return <FileType type={type} aria-hidden="true" />;
}
