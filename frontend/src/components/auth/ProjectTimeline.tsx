type Status = "done" | "doing" | "todo";

const statusLabel: Record<Status, string> = {
  done: "Done",
  doing: "In progress",
  todo: "Up next",
};

// An invented project, shown so people see what the client portal looks like before they have any data.
const steps: { title: string; detail: string; status: Status }[] = [
  { title: "Requirements approved", detail: "Signed off by the client on 3 March.", status: "done" },
  { title: "Homepage design", detail: "Waiting for the client's feedback.", status: "doing" },
  { title: "Development", detail: "Starts once the design is approved.", status: "todo" },
];

function Marker({ status }: { status: Status }) {
  if (status === "done") {
    return (
      <span className="flex size-6 items-center justify-center rounded-full bg-status-done text-background">
        <svg viewBox="0 0 16 16" className="size-3.5" fill="none" stroke="currentColor" strokeWidth="2.2" aria-hidden>
          <path d="m3.5 8.5 3 3 6-7" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </span>
    );
  }

  if (status === "doing") {
    return (
      <span className="flex size-6 items-center justify-center rounded-full border-2 border-status-doing">
        <span className="size-2 rounded-full bg-status-doing" />
      </span>
    );
  }

  return <span className="size-6 rounded-full border-2 border-status-todo bg-background" />;
}

export function ProjectTimeline() {
  return (
    <section aria-labelledby="timeline-title" className="w-full max-w-md">
      <h2 id="timeline-title" className="font-heading text-3xl font-semibold leading-tight">
        What your client sees
      </h2>
      <p className="mt-3 text-muted-foreground">
        Every project has a timeline your client can open at any time, so nobody has to ask for an update.
      </p>

      <p className="mt-12 font-heading text-xl font-semibold">Bakery website</p>
      <p className="text-sm text-muted-foreground">Example project</p>

      <ol className="relative mt-6 flex flex-col gap-8">
        <span
          aria-hidden
          className="timeline-line absolute bottom-3 left-3 top-3 w-0.5 -translate-x-1/2 bg-border"
        />
        {steps.map((step, index) => (
          <li
            key={step.title}
            className="timeline-step relative flex gap-4"
            style={{ "--step": index } as React.CSSProperties}
          >
            <Marker status={step.status} />
            <div>
              <p className="font-medium">
                {step.title}
                <span className="sr-only">, {statusLabel[step.status]}</span>
              </p>
              <p className="text-sm text-muted-foreground">{step.detail}</p>
            </div>
          </li>
        ))}
      </ol>
    </section>
  );
}
