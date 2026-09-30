import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

interface PageHeaderProps {
  /** Page title — rendered as the page's single `<h1>` with the `.text-hero` type style. */
  title: ReactNode;
  /** One-line caption under the title. */
  description?: ReactNode;
  /** Right-aligned actions (buttons). Stacks under the title on small screens. */
  actions?: ReactNode;
  className?: string;
}

/**
 * Shared page title block so every route page opens the same way:
 * `.text-hero` title, muted caption, optional actions. Pair with
 * `<main className="container-page py-8">` (see src/AGENTS.md, "Spacing & layout").
 */
export function PageHeader({ title, description, actions, className }: PageHeaderProps) {
  return (
    <div
      className={cn(
        'mb-8 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between',
        className
      )}
    >
      <div className="min-w-0">
        <h1 className="text-hero">{title}</h1>
        {description && <p className="text-caption mt-2">{description}</p>}
      </div>
      {actions && <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div>}
    </div>
  );
}
