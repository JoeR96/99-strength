/**
 * Apple Design System Theme
 * Premium, minimalist design language inspired by Apple's MacBook Pro marketing pages.
 * Clean, elegant, and functional with generous whitespace and subtle animations.
 */

export const appleDesign = {
  name: "Apple Design",
  colors: {
    appleBlack: {
      hex: "#1d1d1f",
      hsl: "240 2% 11%",
      description:
        "Apple's signature almost-black for primary text on light backgrounds. Provides premium contrast without harsh pure black.",
    },
    appleGray: {
      hex: "#6e6e73",
      hsl: "240 3% 44%",
      description:
        "Medium gray for secondary text and supporting content. Perfect for captions and de-emphasized elements.",
    },
    appleBlue: {
      hex: "#0071e3",
      hsl: "207 100% 45%",
      description:
        "Apple's signature blue for links, CTAs, and interactive elements. Vibrant yet professional.",
    },
    appleOffWhite: {
      hex: "#f5f5f7",
      hsl: "240 9% 96%",
      description:
        "Soft off-white for text on dark backgrounds. Reduces eye strain while maintaining readability.",
    },
    spaceBlack: {
      hex: "#000000",
      hsl: "0 0% 0%",
      description:
        "Pure black for dark mode backgrounds and premium hero sections. Creates dramatic depth.",
    },
    spaceGray: {
      hex: "#2c2c2e",
      hsl: "240 2% 18%",
      description:
        "Dark gray for elevated surfaces in dark mode. Provides subtle layering without being too bright.",
    },
  },
  light: {
    // Backgrounds - INVERTED (Black backgrounds)
    background: "0 0% 0%", // Pure black
    foreground: "240 9% 96%", // Apple Off-White (#f5f5f7)

    // Cards & elevated surfaces
    card: "240 2% 18%", // Space Gray (#2c2c2e)
    cardForeground: "240 9% 96%", // Apple Off-White text

    // Popover & dropdown surfaces
    popover: "240 2% 18%",
    popoverForeground: "240 9% 96%",

    // Primary actions (Apple Blue - brighter for dark bg)
    primary: "207 100% 50%", // Slightly brighter #0071e3
    primaryForeground: "0 0% 100%", // White text on blue

    // Secondary elements
    secondary: "240 2% 18%", // Space Gray
    secondaryForeground: "240 9% 96%", // Apple Off-White

    // Muted/disabled states
    muted: "240 2% 18%",
    mutedForeground: "240 5% 65%", // Lighter gray for readability

    // Accent
    accent: "207 100% 50%",
    accentForeground: "0 0% 100%",

    // Destructive actions
    destructive: "0 72% 51%", // Slightly muted red for dark bg
    destructiveForeground: "0 0% 100%",

    // Borders & inputs
    border: "0 0% 100% / 0.1", // White with 10% opacity
    input: "0 0% 100% / 0.1",
    ring: "207 100% 50%", // Apple Blue for focus rings

    // Success state
    success: "142 71% 45%", // Slightly brighter green
    successForeground: "0 0% 100%",

    // Warning state
    warning: "38 92% 55%", // Slightly brighter orange
    warningForeground: "0 0% 100%",
  },
  dark: {
    // Backgrounds - INVERTED (White backgrounds)
    background: "0 0% 100%", // Pure white
    foreground: "240 2% 11%", // Apple Black (#1d1d1f)

    // Cards & elevated surfaces
    card: "0 0% 100%", // White cards
    cardForeground: "240 2% 11%",

    // Popover & dropdown surfaces
    popover: "0 0% 100%",
    popoverForeground: "240 2% 11%",

    // Primary actions (Apple Blue)
    primary: "207 100% 45%", // #0071e3
    primaryForeground: "0 0% 100%",

    // Secondary elements
    secondary: "240 5% 96%", // Very light gray
    secondaryForeground: "240 2% 11%",

    // Muted/disabled states
    muted: "240 5% 96%",
    mutedForeground: "240 3% 44%", // Apple Gray (#6e6e73)

    // Accent
    accent: "207 100% 45%",
    accentForeground: "0 0% 100%",

    // Destructive actions
    destructive: "0 84% 60%", // Red
    destructiveForeground: "0 0% 100%",

    // Borders & inputs
    border: "240 6% 90%", // Subtle light gray border
    input: "240 6% 90%",
    ring: "207 100% 45%",

    // Success state
    success: "142 76% 36%", // Green
    successForeground: "0 0% 100%",

    // Warning state
    warning: "38 92% 50%", // Orange
    warningForeground: "0 0% 100%",
  },
} as const;

export type Theme = typeof appleDesign;
