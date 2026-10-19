import * as React from 'react';
import { cn } from '@/lib/utils';

export interface ToggleButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  /** Selected state — drives the filled style and `aria-pressed`. */
  pressed: boolean;
}

/**
 * Compact on/off chip for segmented filters (time period, metric, series picker).
 * Pressed: solid primary (near-black on orange, ~5.9:1). Unpressed: muted text on the
 * muted surface (~5.9:1). Replaces several hand-rolled copies of the same chip.
 */
export const ToggleButton = React.forwardRef<HTMLButtonElement, ToggleButtonProps>(
  ({ pressed, className, type = 'button', ...props }, ref) => (
    <button
      ref={ref}
      type={type}
      aria-pressed={pressed}
      className={cn(
        'inline-flex min-h-9 items-center justify-center rounded-md px-3 py-1.5 text-sm font-medium transition-colors',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background',
        pressed
          ? 'bg-primary text-primary-foreground'
          : 'bg-muted text-muted-foreground hover:text-foreground',
        className
      )}
      {...props}
    />
  )
);
ToggleButton.displayName = 'ToggleButton';
