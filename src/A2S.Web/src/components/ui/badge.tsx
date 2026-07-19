import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

const badgeVariants = cva(
  "inline-flex items-center justify-center whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium",
  {
    variants: {
      variant: {
        neutral: "bg-muted text-muted-foreground",
        primary: "bg-primary/10 text-primary",
        accent: "bg-accent/15 text-accent-foreground",
        info: "bg-secondary text-secondary-foreground",
        warning: "bg-warning/15 text-warning",
        success: "bg-success/15 text-success",
      },
    },
    defaultVariants: {
      variant: "neutral",
    },
  }
);

export interface BadgeProps
  extends React.HTMLAttributes<HTMLSpanElement>,
    VariantProps<typeof badgeVariants> {}

/** Small token-coloured label pill. `whitespace-nowrap` prevents shape collapse. */
export function Badge({ className, variant, ...props }: BadgeProps) {
  return <span className={cn(badgeVariants({ variant }), className)} {...props} />;
}

/** Circular numeric day/order badge; min-width stops it collapsing to an oval. */
export function DayBadge({
  value,
  className,
}: {
  value: React.ReactNode;
  className?: string;
}) {
  return (
    <span
      className={cn(
        "inline-flex items-center justify-center min-w-7 h-7 rounded-full bg-primary/10 text-primary text-sm font-bold shrink-0",
        className
      )}
    >
      {value}
    </span>
  );
}
