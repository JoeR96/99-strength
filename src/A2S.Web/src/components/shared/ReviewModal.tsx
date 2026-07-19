import * as React from "react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

export interface ReviewModalAction {
  label: string;
  onClick: () => void;
  variant?: React.ComponentProps<typeof Button>["variant"];
  disabled?: boolean;
}

interface ReviewModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: React.ReactNode;
  icon?: React.ReactNode;
  /** Optional accent tint for the header, using token utilities only. */
  headerClassName?: string;
  children: React.ReactNode;
  /** Footer actions. Rendered stacked at <=390px, side-by-side above it. */
  actions: ReviewModalAction[];
}

/**
 * Shared review/decision modal shell: tinted header, scrollable token-surfaced
 * body, and a footer that stacks vertically on narrow viewports so action
 * buttons never overflow at 390px.
 */
export function ReviewModal({
  open,
  onOpenChange,
  title,
  description,
  icon,
  headerClassName,
  children,
  actions,
}: ReviewModalProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg max-h-[80vh] overflow-hidden flex flex-col p-0">
        <DialogHeader className={cn("p-4 border-b", headerClassName)}>
          <div className="flex items-center gap-2">
            {icon}
            <DialogTitle>{title}</DialogTitle>
          </div>
          {description && (
            <DialogDescription className="text-sm mt-1">{description}</DialogDescription>
          )}
        </DialogHeader>

        <div className="flex-1 overflow-y-auto p-4 space-y-4 bg-card">{children}</div>

        <div className="p-4 border-t border-border flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          {actions.map((action, i) => (
            <Button
              key={i}
              variant={action.variant}
              onClick={action.onClick}
              disabled={action.disabled}
              className="w-full sm:w-auto"
            >
              {action.label}
            </Button>
          ))}
        </div>
      </DialogContent>
    </Dialog>
  );
}
