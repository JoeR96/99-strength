import * as React from "react"
import { Slot } from "@radix-ui/react-slot"
import { cva, type VariantProps } from "class-variance-authority"

import { cn } from "@/lib/utils"

const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap font-semibold transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0",
  {
    variants: {
      variant: {
        // Primary button - flat burnt-orange fill, legible foreground
        default:
          "bg-primary text-primary-foreground border border-primary hover:bg-primary/90 active:translate-y-0.5 transition-all duration-150",

        // Destructive button - red
        destructive:
          "bg-destructive text-destructive-foreground border border-destructive hover:bg-destructive/90 active:translate-y-0.5 transition-all duration-150",

        // Outlined button - clean border
        outline:
          "border border-border bg-transparent text-foreground hover:bg-foreground/5 hover:border-foreground/40 transition-all duration-150",

        // Secondary button - subtle dark
        secondary:
          "bg-secondary text-secondary-foreground border border-secondary hover:bg-secondary/80 active:translate-y-0.5 transition-all duration-150",

        // Ghost button - minimal
        ghost:
          "text-muted-foreground hover:bg-foreground/10 hover:text-foreground transition-all duration-150",

        // Link button
        link:
          "text-primary underline-offset-4 hover:underline transition-all duration-150",

        // Success button - green
        success:
          "bg-success text-success-foreground border border-success hover:bg-success/90 active:translate-y-0.5 transition-all duration-150",

        // Accent button - yellow
        accent:
          "bg-accent text-accent-foreground border border-accent hover:bg-accent/90 active:translate-y-0.5 transition-all duration-150",
      },
      size: {
        default: "h-12 px-6 py-3 text-base rounded-md",
        sm: "h-10 px-4 py-2 text-sm rounded",
        lg: "h-14 px-8 py-3 text-lg rounded-md",
        xl: "h-16 px-10 py-4 text-xl rounded-lg",
        icon: "h-12 w-12 rounded-md",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  }
)

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  asChild?: boolean
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, asChild = false, ...props }, ref) => {
    const Comp = asChild ? Slot : "button"
    return (
      <Comp
        className={cn(buttonVariants({ variant, size, className }))}
        ref={ref}
        {...props}
      />
    )
  }
)
Button.displayName = "Button"

export { Button, buttonVariants }
