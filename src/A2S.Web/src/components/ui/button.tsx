import * as React from "react"
import { Slot } from "@radix-ui/react-slot"
import { cva, type VariantProps } from "class-variance-authority"

import { cn } from "@/lib/utils"

const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-xl font-medium transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0",
  {
    variants: {
      variant: {
        // Apple Blue primary button
        default:
          "bg-primary text-primary-foreground shadow-sm hover:bg-primary/90 transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)]",

        // Destructive button with Apple's red
        destructive:
          "bg-destructive text-destructive-foreground shadow-sm hover:bg-destructive/90 transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)]",

        // Outlined button - clean and minimal
        outline:
          "border border-input bg-transparent hover:bg-muted transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)]",

        // Secondary button with light gray background
        secondary:
          "bg-secondary text-secondary-foreground hover:bg-secondary/80 transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)]",

        // Ghost button - subtle hover effect
        ghost:
          "hover:bg-muted transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)]",

        // Link button - Apple Blue with no underline by default
        link:
          "text-primary underline-offset-4 hover:underline transition-all duration-200 ease-[cubic-bezier(0.4,0,0.2,1)]",
      },
      size: {
        default: "h-11 px-6 py-3 text-[0.9375rem]", /* 15px text */
        sm: "h-9 px-4 py-2 text-sm rounded-lg",
        lg: "h-12 px-8 py-3 text-base rounded-xl",
        xl: "h-14 px-10 py-4 text-lg rounded-2xl",
        icon: "h-11 w-11 rounded-xl",
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
