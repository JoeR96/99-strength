import { UserButton, useUser } from '@clerk/clerk-react';
import { Link, useLocation } from 'react-router-dom';
import { useTheme } from '@/contexts/ThemeContext';

const navLinks = [
  { href: '/dashboard', label: 'Dashboard' },
  { href: '/workout', label: 'Workout' },
  { href: '/history', label: 'History' },
  { href: '/programs', label: 'Programs' },
  { href: '/exercises', label: 'Exercises' },
  { href: '/hevy', label: 'Hevy' },
  { href: '/settings', label: 'Settings' },
];

export function Navbar() {
  const { user } = useUser();
  const location = useLocation();
  const { mode, toggleMode } = useTheme();

  return (
    <nav className="sticky top-0 z-50 bg-card/95 backdrop-blur border-b border-border">
      <div className="container-apple">
        <div className="flex h-16 justify-between items-center">
          {/* Logo */}
          <div className="flex items-center gap-8">
            <Link to="/dashboard" className="flex items-center gap-3 group">
              <div className="flex h-11 w-11 items-center justify-center rounded-md bg-primary">
                <span className="text-lg font-bold text-white font-[Orbitron,sans-serif]">99</span>
              </div>
              <h1 className="text-xl font-bold text-white tracking-wide uppercase font-[Orbitron,sans-serif] hidden sm:block">
                99 Strength
              </h1>
            </Link>

            {/* Navigation Links */}
            <div className="hidden lg:flex items-center gap-1">
              {navLinks.map((link) => {
                const isActive = location.pathname === link.href;
                return (
                  <Link
                    key={link.href}
                    to={link.href}
                    className={`px-4 py-2 rounded text-base font-medium uppercase tracking-wide font-[Orbitron,sans-serif] transition-all duration-150 ${
                      isActive
                        ? 'bg-primary/20 text-primary'
                        : 'text-gray-400 hover:text-white hover:bg-white/5'
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
            {/* Theme Toggle */}
            <button
              onClick={toggleMode}
              className="flex h-11 w-11 items-center justify-center rounded-md border border-gray-600 bg-transparent text-gray-400 hover:text-white hover:border-gray-500 transition-all duration-150"
              aria-label={mode === 'dark' ? 'Switch to CRT mode' : 'Switch to Neon mode'}
            >
              {mode === 'dark' ? (
                <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                </svg>
              ) : (
                <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
                </svg>
              )}
            </button>

            {/* Player Name */}
            <div className="hidden sm:flex items-center gap-2 px-4 py-2 rounded bg-white/5 border border-gray-700">
              <span className="text-sm font-medium text-gray-500 uppercase tracking-wide font-[Orbitron,sans-serif]">
                Player:
              </span>
              <span className="text-lg font-semibold text-white font-[VT323,monospace]">
                {user?.firstName || 'Guest'}
              </span>
            </div>

            <UserButton
              afterSignOutUrl="/sign-in"
              appearance={{
                elements: {
                  avatarBox: "h-11 w-11 rounded-md border border-gray-600"
                }
              }}
            />
          </div>
        </div>

        {/* Mobile Navigation */}
        <div className="lg:hidden pb-3 flex gap-2 overflow-x-auto scrollbar-hide">
          {navLinks.map((link) => {
            const isActive = location.pathname === link.href;
            return (
              <Link
                key={link.href}
                to={link.href}
                className={`px-4 py-2 rounded text-sm font-medium uppercase tracking-wide font-[Orbitron,sans-serif] whitespace-nowrap transition-all duration-150 ${
                  isActive
                    ? 'bg-primary/20 text-primary'
                    : 'text-gray-400 hover:text-white hover:bg-white/5'
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
