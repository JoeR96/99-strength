import * as React from "react"
import { Slot } from "@radix-ui/react-slot"
import { cva, type VariantProps } from "class-variance-authority"

import { cn } from "@/lib/utils"

const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap font-semibold uppercase tracking-wide transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0 font-[Orbitron,sans-serif]",
  {
    variants: {
      variant: {
        // Primary button - magenta with subtle shadow
        default:
          "bg-primary text-white border border-primary shadow-md hover:bg-primary/90 hover:shadow-lg active:translate-y-0.5 transition-all duration-150",

        // Destructive button - red
        destructive:
          "bg-destructive text-white border border-destructive shadow-md hover:bg-destructive/90 hover:shadow-lg active:translate-y-0.5 transition-all duration-150",

        // Outlined button - clean border
        outline:
          "border border-gray-500 bg-transparent text-white hover:bg-white/5 hover:border-gray-400 transition-all duration-150",

        // Secondary button - subtle dark
        secondary:
          "bg-secondary text-white border border-secondary shadow-md hover:bg-secondary/80 active:translate-y-0.5 transition-all duration-150",

        // Ghost button - minimal
        ghost:
          "text-gray-300 hover:bg-white/10 hover:text-white transition-all duration-150",

        // Link button
        link:
          "text-primary underline-offset-4 hover:underline transition-all duration-150",

        // Success button - green
        success:
          "bg-success text-white border border-success shadow-md hover:bg-success/90 hover:shadow-lg active:translate-y-0.5 transition-all duration-150",

        // Accent button - yellow
        accent:
          "bg-accent text-black border border-accent shadow-md hover:bg-accent/90 hover:shadow-lg active:translate-y-0.5 transition-all duration-150",
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
