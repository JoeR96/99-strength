import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { ThemeProvider, useTheme } from './ThemeContext';

function ThemeConsumer() {
  const { mode, toggleMode, setMode } = useTheme();
  return (
    <div>
      <span data-testid="mode">{mode}</span>
      <button onClick={toggleMode}>Toggle</button>
      <button onClick={() => setMode('osrs')}>Set OSRS</button>
      <button onClick={() => setMode('apple')}>Set Apple</button>
      <button onClick={() => setMode('retro')}>Set Retro</button>
    </div>
  );
}

describe('ThemeContext', () => {
  it('defaults to retro theme', () => {
    render(
      <ThemeProvider>
        <ThemeConsumer />
      </ThemeProvider>
    );
    expect(screen.getByTestId('mode').textContent).toBe('retro');
  });

  it('cycles through themes on toggle: retro → osrs → apple → retro', () => {
    render(
      <ThemeProvider>
        <ThemeConsumer />
      </ThemeProvider>
    );
    const toggleBtn = screen.getByText('Toggle');

    expect(screen.getByTestId('mode').textContent).toBe('retro');

    act(() => fireEvent.click(toggleBtn));
    expect(screen.getByTestId('mode').textContent).toBe('osrs');

    act(() => fireEvent.click(toggleBtn));
    expect(screen.getByTestId('mode').textContent).toBe('apple');

    act(() => fireEvent.click(toggleBtn));
    expect(screen.getByTestId('mode').textContent).toBe('retro');
  });

  it('sets mode directly via setMode', () => {
    render(
      <ThemeProvider>
        <ThemeConsumer />
      </ThemeProvider>
    );

    act(() => fireEvent.click(screen.getByText('Set Apple')));
    expect(screen.getByTestId('mode').textContent).toBe('apple');

    act(() => fireEvent.click(screen.getByText('Set OSRS')));
    expect(screen.getByTestId('mode').textContent).toBe('osrs');
  });

  it('persists theme to localStorage', () => {
    render(
      <ThemeProvider>
        <ThemeConsumer />
      </ThemeProvider>
    );

    act(() => fireEvent.click(screen.getByText('Set Apple')));
    expect(localStorage.setItem).toHaveBeenCalledWith('99-strength-theme-mode', 'apple');
  });

  it('reads initial theme from localStorage', () => {
    vi.mocked(localStorage.getItem).mockReturnValue('osrs');

    render(
      <ThemeProvider>
        <ThemeConsumer />
      </ThemeProvider>
    );
    expect(screen.getByTestId('mode').textContent).toBe('osrs');
  });

  it('migrates legacy "light" to "retro"', () => {
    vi.mocked(localStorage.getItem).mockReturnValue('light');

    render(
      <ThemeProvider>
        <ThemeConsumer />
      </ThemeProvider>
    );
    expect(screen.getByTestId('mode').textContent).toBe('retro');
  });

  it('migrates legacy "dark" to "osrs"', () => {
    vi.mocked(localStorage.getItem).mockReturnValue('dark');

    render(
      <ThemeProvider>
        <ThemeConsumer />
      </ThemeProvider>
    );
    expect(screen.getByTestId('mode').textContent).toBe('osrs');
  });

  it('applies "dark" class for osrs theme', () => {
    render(
      <ThemeProvider>
        <ThemeConsumer />
      </ThemeProvider>
    );

    act(() => fireEvent.click(screen.getByText('Set OSRS')));
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    expect(document.documentElement.classList.contains('apple-theme')).toBe(false);
  });

  it('applies "apple-theme" class for apple theme', () => {
    render(
      <ThemeProvider>
        <ThemeConsumer />
      </ThemeProvider>
    );

    act(() => fireEvent.click(screen.getByText('Set Apple')));
    expect(document.documentElement.classList.contains('apple-theme')).toBe(true);
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });

  it('removes theme classes for retro theme', () => {
    render(
      <ThemeProvider>
        <ThemeConsumer />
      </ThemeProvider>
    );

    act(() => fireEvent.click(screen.getByText('Set OSRS')));
    act(() => fireEvent.click(screen.getByText('Set Retro')));
    expect(document.documentElement.classList.contains('dark')).toBe(false);
    expect(document.documentElement.classList.contains('apple-theme')).toBe(false);
  });

  it('throws when useTheme is used outside ThemeProvider', () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
    expect(() => render(<ThemeConsumer />)).toThrow(
      'useTheme must be used within a ThemeProvider'
    );
    consoleError.mockRestore();
  });
});
