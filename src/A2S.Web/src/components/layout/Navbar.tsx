import { useState, useEffect, useRef } from 'react';
import { UserButton, useUser } from '@clerk/clerk-react';
import { Link, useLocation } from 'react-router-dom';
import { useTheme } from '@/contexts/ThemeContext';

const navLinks = [
  { href: '/dashboard', label: 'Dashboard' },
  { href: '/workout', label: 'Workout' },
  { href: '/history', label: 'History' },
  { href: '/programs', label: 'Programs' },
  { href: '/exercises', label: 'Exercises' },
  { href: '/simulate', label: 'Simulator' },
  { href: '/hevy', label: 'Hevy' },
  { href: '/hevy/data', label: 'Hevy Data' },
  { href: '/settings', label: 'Settings' },
];

export function Navbar() {
  const { user } = useUser();
  const location = useLocation();
  const { mode, toggleMode } = useTheme();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  // Close mobile menu on route change
  useEffect(() => {
    setMobileMenuOpen(false);
  }, [location.pathname]);

  // Close on outside click
  useEffect(() => {
    if (!mobileMenuOpen) return;
    function handleClick(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setMobileMenuOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, [mobileMenuOpen]);

  // Prevent body scroll when menu open
  useEffect(() => {
    if (mobileMenuOpen) {
      document.body.style.overflow = "hidden";
    } else {
      document.body.style.overflow = "";
    }
    return () => { document.body.style.overflow = ""; };
  }, [mobileMenuOpen]);

  return (
    <nav className="sticky top-0 z-50 border-b border-border bg-card backdrop-blur">
      <div className="container-page">
        <div className="flex h-16 justify-between items-center">
          {/* Logo */}
          <div className="flex items-center gap-8">
            <Link to="/dashboard" className="flex items-center gap-3 group">
              <div className="flex h-11 w-11 items-center justify-center rounded-md bg-primary">
                <span className="text-lg font-bold font-display text-primary-foreground">99</span>
              </div>
              <h1 className="text-xl font-bold font-display tracking-wide uppercase hidden sm:block text-foreground">
                Strength
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
                    className={`px-4 py-2 rounded text-base font-medium font-display uppercase tracking-wide transition-all duration-150 ${
                      isActive
                        ? 'bg-primary/20 text-primary'
                        : 'text-muted-foreground hover:text-foreground hover:bg-muted'
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
            {/* Hamburger Menu Button — mobile only */}
            <button
              onClick={() => setMobileMenuOpen((prev) => !prev)}
              className="lg:hidden flex h-11 w-11 items-center justify-center rounded-md border border-border bg-transparent text-muted-foreground transition-all duration-150 hover:text-foreground hover:bg-muted"
              aria-label={mobileMenuOpen ? 'Close menu' : 'Open menu'}
              aria-expanded={mobileMenuOpen}
            >
              {mobileMenuOpen ? (
                <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                </svg>
              ) : (
                <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
                </svg>
              )}
            </button>

            {/* Theme Toggle */}
            <button
              onClick={toggleMode}
              className="flex h-11 w-11 items-center justify-center rounded-md border border-border bg-transparent text-muted-foreground transition-all duration-150 hover:text-foreground hover:bg-muted"
              aria-label={`Switch theme (current: ${mode === 'retro' ? 'Retro Arcade' : mode === 'osrs' ? 'OSRS' : 'Apple'})`}
              title={`Theme: ${mode === 'retro' ? 'Retro Arcade' : mode === 'osrs' ? 'OSRS' : 'Apple'}`}
            >
              {mode === 'retro' ? (
                /* Sword icon — click to go to OSRS */
                <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M14.828 14.828L21 21m-7.071-7.071L21 3M3 21l7.071-7.071m0 0L3 3" />
                </svg>
              ) : mode === 'osrs' ? (
                /* Apple icon — click to go to Apple */
                <svg className="h-5 w-5" fill="currentColor" viewBox="0 0 24 24">
                  <path d="M18.71 19.5c-.83 1.24-1.71 2.45-3.05 2.47-1.34.03-1.77-.79-3.29-.79-1.53 0-2 .77-3.27.82-1.31.05-2.3-1.32-3.14-2.53C4.25 17 2.94 12.45 4.7 9.39c.87-1.52 2.43-2.48 4.12-2.51 1.28-.02 2.5.87 3.29.87.78 0 2.26-1.07 3.8-.91.65.03 2.47.26 3.64 1.98-.09.06-2.17 1.28-2.15 3.81.03 3.02 2.65 4.03 2.68 4.04-.03.07-.42 1.44-1.38 2.83M13 3.5c.73-.83 1.94-1.46 2.94-1.5.13 1.17-.34 2.35-1.04 3.19-.69.85-1.83 1.51-2.95 1.42-.15-1.15.41-2.35 1.05-3.11z"/>
                </svg>
              ) : (
                /* CRT/Arcade icon — click to go to Retro */
                <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                </svg>
              )}
            </button>

            {/* Player Name */}
            <div className="hidden sm:flex items-center gap-2 px-4 py-2 rounded border border-border bg-muted">
              <span className="text-sm font-medium font-display uppercase tracking-wide text-muted-foreground">
                Player:
              </span>
              <span className="text-lg font-semibold text-foreground">
                {user?.firstName || 'Guest'}
              </span>
            </div>

            <UserButton
              afterSignOutUrl="/sign-in"
              appearance={{
                elements: {
                  avatarBox: "h-11 w-11 rounded-md border border-border"
                }
              }}
            />
          </div>
        </div>

        {/* Mobile Slide-Out Menu */}
        {mobileMenuOpen && (
          <div className="fixed inset-0 z-40 lg:hidden" aria-modal="true" role="dialog">
            {/* Backdrop */}
            <div className="absolute inset-0 bg-black/80" aria-hidden="true" />

            {/* Menu panel */}
            <div
              ref={menuRef}
              className="absolute top-16 left-0 right-0 max-h-[calc(100vh-4rem)] overflow-y-auto border-b border-border bg-card shadow-lg"
            >
              <nav className="px-4 py-3 space-y-1">
                {navLinks.map((link) => {
                  const isActive = location.pathname === link.href;
                  return (
                    <Link
                      key={link.href}
                      to={link.href}
                      className={`block px-4 py-3 rounded text-base font-medium font-display uppercase tracking-wide transition-all duration-150 ${
                        isActive
                          ? 'bg-primary/20 text-primary'
                          : 'text-muted-foreground hover:text-foreground hover:bg-muted'
                      }`}
                    >
                      {link.label}
                    </Link>
                  );
                })}
              </nav>

              {/* Player name — visible on small screens in menu */}
              <div className="sm:hidden px-4 py-3 border-t border-border">
                <div className="flex items-center gap-2">
                  <span className="text-sm font-medium font-display uppercase tracking-wide text-muted-foreground">Player:</span>
                  <span className="text-lg font-semibold text-foreground">{user?.firstName || 'Guest'}</span>
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </nav>
  );
}
