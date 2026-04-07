import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';

type ThemeMode = 'retro' | 'osrs' | 'apple';

interface ThemeContextType {
  mode: ThemeMode;
  toggleMode: () => void;
  setMode: (mode: ThemeMode) => void;
}

const ThemeContext = createContext<ThemeContextType | undefined>(undefined);

const STORAGE_KEY = '99-strength-theme-mode';
const THEME_ORDER: ThemeMode[] = ['retro', 'osrs', 'apple'];

function getInitialMode(): ThemeMode {
  const stored = localStorage.getItem(STORAGE_KEY);
  if (stored === 'retro' || stored === 'osrs' || stored === 'apple') {
    return stored;
  }
  // Migrate legacy values
  if (stored === 'light') return 'retro';
  if (stored === 'dark') return 'osrs';
  return 'retro';
}

function applyThemeClass(mode: ThemeMode) {
  const root = document.documentElement;
  root.classList.remove('dark', 'apple-theme');
  if (mode === 'osrs') {
    root.classList.add('dark');
  } else if (mode === 'apple') {
    root.classList.add('apple-theme');
  }
  // 'retro' uses :root defaults — no class needed
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [mode, setModeState] = useState<ThemeMode>(getInitialMode);

  useEffect(() => {
    applyThemeClass(mode);
    localStorage.setItem(STORAGE_KEY, mode);
  }, [mode]);

  const toggleMode = () => {
    setModeState(prev => {
      const idx = THEME_ORDER.indexOf(prev);
      return THEME_ORDER[(idx + 1) % THEME_ORDER.length];
    });
  };

  const setMode = (newMode: ThemeMode) => {
    setModeState(newMode);
  };

  return (
    <ThemeContext.Provider value={{ mode, toggleMode, setMode }}>
      {children}
    </ThemeContext.Provider>
  );
}

export function useTheme() {
  const context = useContext(ThemeContext);
  if (!context) {
    throw new Error('useTheme must be used within a ThemeProvider');
  }
  return context;
}
