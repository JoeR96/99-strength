import * as React from "react"
import { Slot } from "@radix-ui/react-slot"
import { cva, type VariantProps } from "class-variance-authority"

import { cn } from "@/lib/utils"

const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap border border-transparent font-semibold transition-[background-color,color,border-color,scale,opacity] duration-150 ease-out active:scale-[0.97] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background disabled:pointer-events-none disabled:opacity-40 [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0",
  {
    variants: {
      variant: {
        // The one primary action on a screen: solid burnt orange
        default: "bg-primary text-primary-foreground hover:bg-primary/90",

        destructive: "bg-destructive text-destructive-foreground hover:bg-destructive/90",

        // Secondary action: a quiet grey fill with no outline (still named "outline" for its call sites)
        outline: "bg-foreground/[0.07] text-foreground hover:bg-foreground/[0.12]",

        secondary: "bg-secondary text-secondary-foreground hover:bg-secondary/80",

        // A repeated action that should read as orange without competing with the primary
        tinted: "bg-primary/15 text-primary hover:bg-primary/25",

        ghost: "text-muted-foreground hover:bg-foreground/10 hover:text-foreground",

        link: "text-primary underline-offset-4 hover:underline active:scale-100",

        success: "bg-success text-success-foreground hover:bg-success/90",

        accent: "bg-accent text-accent-foreground hover:bg-accent/90",
      },
      size: {
        default: "h-12 px-6 py-3 text-base rounded-md",
        sm: "h-11 px-4 py-2 text-sm rounded-md",
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
