import {
  FileArchiveIcon,
  FileAudioIcon,
  FileCodeIcon,
  FileIcon,
  FileImageIcon,
  FileSpreadsheetIcon,
  FileTextIcon,
  FileVideoIcon,
  PresentationIcon,
} from "lucide-react";

type Kind = "image" | "video" | "audio" | "archive" | "spreadsheet" | "presentation" | "code" | "text" | "other";

const kindByExtension: Record<string, Kind> = {
  png: "image", jpg: "image", jpeg: "image", gif: "image", webp: "image", svg: "image", avif: "image", heic: "image",
  bmp: "image", ico: "image", psd: "image", ai: "image", fig: "image", sketch: "image", xd: "image",
  mp4: "video", mov: "video", webm: "video", avi: "video", mkv: "video",
  mp3: "audio", wav: "audio", ogg: "audio", m4a: "audio", flac: "audio",
  zip: "archive", rar: "archive", "7z": "archive", tar: "archive", gz: "archive",
  xls: "spreadsheet", xlsx: "spreadsheet", csv: "spreadsheet", ods: "spreadsheet",
  ppt: "presentation", pptx: "presentation", key: "presentation", odp: "presentation",
  json: "code", xml: "code", html: "code", css: "code", js: "code", ts: "code",
  pdf: "text", doc: "text", docx: "text", odt: "text", rtf: "text", txt: "text", md: "text",
};

const kindByMimePrefix: [string, Kind][] = [
  ["image/", "image"],
  ["video/", "video"],
  ["audio/", "audio"],
  ["text/", "text"],
];

function kindOf(fileName: string, contentType?: string): Kind {
  const dot = fileName.lastIndexOf(".");
  const extension = dot >= 0 ? fileName.slice(dot + 1).toLowerCase() : "";
  // The extension first: browsers often report a design file or an archive as generic binary data.
  return kindByExtension[extension] ?? kindByMimePrefix.find(([prefix]) => contentType?.startsWith(prefix))?.[1] ?? "other";
}

const icon = {
  image: <FileImageIcon className="size-5" />,
  video: <FileVideoIcon className="size-5" />,
  audio: <FileAudioIcon className="size-5" />,
  archive: <FileArchiveIcon className="size-5" />,
  spreadsheet: <FileSpreadsheetIcon className="size-5" />,
  presentation: <PresentationIcon className="size-5" />,
  code: <FileCodeIcon className="size-5" />,
  text: <FileTextIcon className="size-5" />,
  other: <FileIcon className="size-5" />,
} satisfies Record<Kind, React.ReactNode>;

/** A decorative tile: the file's name and size already say what it is in words. */
export function FileTypeIcon({ fileName, contentType }: { fileName: string; contentType?: string }) {
  return (
    <span aria-hidden className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-muted text-muted-foreground">
      {icon[kindOf(fileName, contentType)]}
    </span>
  );
}
