import { useId, type ReactNode } from "react";

type SectionProps = {
  title: string;
  /** A link or button beside the title. */
  action?: ReactNode;
  /** Starts with the section's kebab-case name. */
  className?: string;
  children: ReactNode;
};

/** A titled block of a page: the heading labels the section for screen readers. */
export function Section({ title, action, className, children }: SectionProps) {
  const titleId = useId();
  const heading = (
    <h2 id={titleId} className="font-heading text-xl font-semibold">
      {title}
    </h2>
  );

  return (
    <section aria-labelledby={titleId} className={className}>
      {action ? (
        <div className="section-header mb-4 flex items-baseline justify-between gap-3">
          {heading}
          {action}
        </div>
      ) : (
        <div className="section-header mb-4">{heading}</div>
      )}
      {children}
    </section>
  );
}
