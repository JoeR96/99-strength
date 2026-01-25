import { UserButton, useUser } from '@clerk/clerk-react';
import { Link, useLocation } from 'react-router-dom';
import { useTheme } from '@/contexts/ThemeContext';

const navLinks = [
  { href: '/dashboard', label: 'Dashboard' },
  { href: '/workout', label: 'Current Workout' },
  { href: '/programs', label: 'My Programs' },
];

export function Navbar() {
  const { user } = useUser();
  const location = useLocation();
  const { mode, toggleMode } = useTheme();

  return (
    <nav className="sticky top-0 z-50 glass border-b border-border/10">
      <div className="container-apple">
        <div className="flex h-16 justify-between items-center">
          {/* Logo - Apple minimalist style */}
          <div className="flex items-center gap-8">
            <Link to="/dashboard" className="flex items-center gap-3 group">
              <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-foreground transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] group-hover:scale-105">
                <span className="text-base font-bold text-background">A</span>
              </div>
              <h1 className="text-xl font-semibold text-foreground tracking-tight">
                A2S Tracker
              </h1>
            </Link>

            {/* Navigation Links - Apple style */}
            <div className="hidden md:flex items-center gap-2">
              {navLinks.map((link) => {
                const isActive = location.pathname === link.href;
                return (
                  <Link
                    key={link.href}
                    to={link.href}
                    className={`px-4 py-2 rounded-lg text-[0.9375rem] font-normal transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] ${
                      isActive
                        ? 'bg-muted text-foreground'
                        : 'text-muted-foreground hover:text-foreground'
                    }`}
                  >
                    {link.label}
                  </Link>
                );
              })}
            </div>
          </div>

          {/* Right Side */}
          <div className="flex items-center gap-4">
            {/* Dark Mode Toggle */}
            <button
              onClick={toggleMode}
              className="flex h-9 w-9 items-center justify-center rounded-lg bg-muted/30 text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)]"
              aria-label={mode === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
            >
              {mode === 'dark' ? (
                // Sun icon - currently in dark mode, click to go light
                <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z" />
                </svg>
              ) : (
                // Moon icon - currently in light mode, click to go dark
                <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z" />
                </svg>
              )}
            </button>

            <span className="text-[0.9375rem] font-normal text-muted-foreground hidden sm:inline">
              {user?.firstName || 'User'}
            </span>
            <UserButton afterSignOutUrl="/sign-in" />
          </div>
        </div>

        {/* Mobile Navigation */}
        <div className="md:hidden pb-3 flex gap-2 overflow-x-auto">
          {navLinks.map((link) => {
            const isActive = location.pathname === link.href;
            return (
              <Link
                key={link.href}
                to={link.href}
                className={`px-4 py-2 rounded-lg text-sm font-normal whitespace-nowrap transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] ${
                  isActive
                    ? 'bg-muted text-foreground'
                    : 'text-muted-foreground hover:text-foreground'
                }`}
              >
                {link.label}
              </Link>
            );
          })}
        </div>
      </div>
    </nav>
  );
}
