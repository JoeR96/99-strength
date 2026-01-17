/**
 * Golden Twilight Theme
 * Midnight navy and sapphire set off sparkling yellows,
 * capturing the intrigue of nightfall's gold.
 */

export const goldenTwilight = {
  name: "Golden Twilight",
  colors: {
    inkBlack: {
      hex: "#000814",
      hsl: "222 100% 3%",
      description:
        "Ultra-dark with a hint of blue, reminiscent of deep inkwells and infinite space, sparks drama and imagination in design.",
    },
    prussianBlue: {
      hex: "#001d3d",
      hsl: "210 100% 11%",
      description:
        "Inky, profound blue filled with gravitas and mystery, conjures historical intrigue and academic tradition.",
    },
    oxfordNavy: {
      hex: "#003566",
      hsl: "210 100% 20%",
      description:
        "Deep, dignified blue reminiscent of midnight sky and academia; evokes confidence, tradition, and wise authority.",
    },
    schoolBusYellow: {
      hex: "#ffc300",
      hsl: "45 100% 50%",
      description:
        "Dynamic, iconic golden yellow synonymous with safety, high visibility, youthful energy, and buzzing urban motion.",
    },
    gold: {
      hex: "#ffd60a",
      hsl: "45 100% 52%",
      description:
        "Radiant golden spark shimmer evokes triumph and prestige, catching every eye with dazzling, positive illumination.",
    },
  },
  light: {
    background: "0 0% 100%",
    foreground: "222 100% 3%",
    card: "0 0% 100%",
    cardForeground: "222 100% 3%",
    popover: "0 0% 100%",
    popoverForeground: "222 100% 3%",
    primary: "45 100% 52%",
    primaryForeground: "222 100% 3%",
    secondary: "210 100% 20%",
    secondaryForeground: "45 100% 52%",
    muted: "210 100% 11%",
    mutedForeground: "45 100% 65%",
    accent: "45 100% 50%",
    accentForeground: "222 100% 3%",
    destructive: "0 84% 60%",
    destructiveForeground: "0 0% 100%",
    border: "210 100% 11%",
    input: "210 100% 11%",
    ring: "45 100% 52%",
  },
  dark: {
    background: "222 100% 3%",
    foreground: "45 100% 52%",
    card: "210 100% 11%",
    cardForeground: "45 100% 52%",
    popover: "210 100% 11%",
    popoverForeground: "45 100% 52%",
    primary: "45 100% 52%",
    primaryForeground: "222 100% 3%",
    secondary: "210 100% 20%",
    secondaryForeground: "45 100% 52%",
    muted: "210 100% 11%",
    mutedForeground: "45 100% 65%",
    accent: "45 100% 50%",
    accentForeground: "222 100% 3%",
    destructive: "0 62% 30%",
    destructiveForeground: "0 0% 100%",
    border: "210 100% 20%",
    input: "210 100% 20%",
    ring: "45 100% 52%",
  },
} as const;

export type Theme = typeof goldenTwilight;
