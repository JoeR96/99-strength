import { goldenTwilight, type Theme } from "./golden-twilight";

export const themes = {
  goldenTwilight,
  // Add more themes here as needed
} as const;

export type ThemeName = keyof typeof themes;

export { goldenTwilight, type Theme };

/**
 * Apply a theme to the document root
 * @param theme - The theme object to apply
 * @param mode - "light" or "dark" mode
 */
export function applyTheme(
  theme: Theme,
  mode: "light" | "dark" = "light"
): void {
  const root = document.documentElement;
  const themeColors = theme[mode];

  // Apply each CSS variable
  Object.entries(themeColors).forEach(([key, value]) => {
    const cssVarName = `--${key.replace(/([A-Z])/g, "-$1").toLowerCase()}`;
    root.style.setProperty(cssVarName, value);
  });

  // Handle dark mode class
  if (mode === "dark") {
    root.classList.add("dark");
  } else {
    root.classList.remove("dark");
  }
}

/**
 * Get the current theme mode from the document
 * @returns "light" or "dark"
 */
export function getCurrentThemeMode(): "light" | "dark" {
  return document.documentElement.classList.contains("dark") ? "dark" : "light";
}

/**
 * Toggle between light and dark mode
 */
export function toggleThemeMode(): void {
  const currentMode = getCurrentThemeMode();
  const newMode = currentMode === "dark" ? "light" : "dark";
  applyTheme(goldenTwilight, newMode);
}
