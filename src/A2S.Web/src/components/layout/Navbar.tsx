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
  { href: '/hevy', label: 'Hevy' },
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
    <nav className={`sticky top-0 z-50 border-b ${
      mode === 'dark'
        ? 'bg-gradient-to-b from-[hsl(30,30%,18%)] to-[hsl(30,28%,14%)] border-[hsl(30,40%,30%)]'
        : 'bg-card/95 backdrop-blur border-border'
    }`}>
      <div className="container-apple">
        <div className="flex h-16 justify-between items-center">
          {/* Logo */}
          <div className="flex items-center gap-8">
            <Link to="/dashboard" className="flex items-center gap-3 group">
              <div className={`flex h-11 w-11 items-center justify-center rounded-md ${
                mode === 'dark'
                  ? 'bg-gradient-to-b from-[hsl(30,35%,35%)] to-[hsl(30,45%,20%)] border-2 border-[hsl(45,80%,45%)]'
                  : 'bg-primary'
              }`}>
                <span className={`text-lg font-bold ${
                  mode === 'dark'
                    ? 'text-[hsl(45,100%,55%)] font-[RuneScape_UF,Times_New_Roman,serif] drop-shadow-[1px_1px_0_rgba(0,0,0,0.8)]'
                    : 'text-white font-[Orbitron,sans-serif]'
                }`}>99</span>
              </div>
              <h1 className={`text-xl font-bold tracking-wide uppercase hidden sm:block ${
                mode === 'dark'
                  ? 'text-[hsl(45,100%,55%)] font-[RuneScape_UF,Times_New_Roman,serif] drop-shadow-[2px_2px_0_rgba(0,0,0,0.5)]'
                  : 'text-white font-[Orbitron,sans-serif]'
              }`}>
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
                    className={`px-4 py-2 rounded text-base font-medium uppercase tracking-wide transition-all duration-150 ${
                      mode === 'dark'
                        ? `font-[RuneScape_UF,Times_New_Roman,serif] ${
                            isActive
                              ? 'bg-[hsl(45,100%,45%)]/20 text-[hsl(45,100%,55%)]'
                              : 'text-[hsl(40,20%,65%)] hover:text-[hsl(45,100%,55%)] hover:bg-[hsl(30,30%,20%)]'
                          }`
                        : `font-[Orbitron,sans-serif] ${
                            isActive
                              ? 'bg-primary/20 text-primary'
                              : 'text-gray-400 hover:text-white hover:bg-white/5'
                          }`
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
              className={`lg:hidden flex h-11 w-11 items-center justify-center rounded-md border transition-all duration-150 ${
                mode === 'dark'
                  ? 'border-[hsl(45,80%,45%)] bg-[hsl(30,30%,20%)] text-[hsl(45,100%,55%)] hover:bg-[hsl(30,35%,25%)]'
                  : 'border-gray-600 bg-transparent text-gray-400 hover:text-white hover:border-gray-500'
              }`}
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
              className={`flex h-11 w-11 items-center justify-center rounded-md border transition-all duration-150 ${
                mode === 'dark'
                  ? 'border-[hsl(45,80%,45%)] bg-[hsl(30,30%,20%)] text-[hsl(45,100%,55%)] hover:bg-[hsl(30,35%,25%)]'
                  : 'border-gray-600 bg-transparent text-gray-400 hover:text-white hover:border-gray-500'
              }`}
              aria-label={mode === 'dark' ? 'Switch to Arcade mode' : 'Switch to OSRS mode'}
              title={mode === 'dark' ? 'Switch to Arcade mode' : 'Switch to OSRS mode'}
            >
              {mode === 'dark' ? (
                /* Arcade/CRT icon when in OSRS mode */
                <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                </svg>
              ) : (
                /* Sword icon when in Arcade mode - represents OSRS */
                <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M14.828 14.828L21 21m-7.071-7.071L21 3M3 21l7.071-7.071m0 0L3 3" />
                </svg>
              )}
            </button>

            {/* Player Name */}
            <div className={`hidden sm:flex items-center gap-2 px-4 py-2 rounded border ${
              mode === 'dark'
                ? 'bg-[hsl(30,30%,18%)] border-[hsl(30,40%,30%)]'
                : 'bg-white/5 border-gray-700'
            }`}>
              <span className={`text-sm font-medium uppercase tracking-wide ${
                mode === 'dark'
                  ? 'text-[hsl(40,20%,60%)] font-[RuneScape_UF,Times_New_Roman,serif]'
                  : 'text-gray-500 font-[Orbitron,sans-serif]'
              }`}>
                Player:
              </span>
              <span className={`text-lg font-semibold ${
                mode === 'dark'
                  ? 'text-[hsl(45,100%,55%)] font-[RuneScape_UF,Times_New_Roman,serif]'
                  : 'text-white font-[VT323,monospace]'
              }`}>
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

        {/* Mobile Slide-Out Menu */}
        {mobileMenuOpen && (
          <div className="fixed inset-0 z-40 lg:hidden" aria-modal="true" role="dialog">
            {/* Backdrop */}
            <div className="absolute inset-0 bg-black/60" aria-hidden="true" />

            {/* Menu panel */}
            <div
              ref={menuRef}
              className={`absolute top-16 left-0 right-0 max-h-[calc(100vh-4rem)] overflow-y-auto border-b shadow-lg ${
                mode === 'dark'
                  ? 'bg-[hsl(30,28%,14%)] border-[hsl(30,40%,30%)]'
                  : 'bg-card border-border'
              }`}
            >
              <nav className="px-4 py-3 space-y-1">
                {navLinks.map((link) => {
                  const isActive = location.pathname === link.href;
                  return (
                    <Link
                      key={link.href}
                      to={link.href}
                      className={`block px-4 py-3 rounded text-base font-medium uppercase tracking-wide transition-all duration-150 ${
                        mode === 'dark'
                          ? `font-[RuneScape_UF,Times_New_Roman,serif] ${
                              isActive
                                ? 'bg-[hsl(45,100%,45%)]/20 text-[hsl(45,100%,55%)]'
                                : 'text-[hsl(40,20%,65%)] hover:text-[hsl(45,100%,55%)] hover:bg-[hsl(30,30%,20%)]'
                            }`
                          : `font-[Orbitron,sans-serif] ${
                              isActive
                                ? 'bg-primary/20 text-primary'
                                : 'text-gray-400 hover:text-white hover:bg-white/5'
                            }`
                      }`}
                    >
                      {link.label}
                    </Link>
                  );
                })}
              </nav>

              {/* Player name — visible on small screens in menu */}
              <div className={`sm:hidden px-4 py-3 border-t ${
                mode === 'dark' ? 'border-[hsl(30,40%,30%)]' : 'border-gray-700'
              }`}>
                <div className="flex items-center gap-2">
                  <span className={`text-sm font-medium uppercase tracking-wide ${
                    mode === 'dark'
                      ? 'text-[hsl(40,20%,60%)] font-[RuneScape_UF,Times_New_Roman,serif]'
                      : 'text-gray-500 font-[Orbitron,sans-serif]'
                  }`}>Player:</span>
                  <span className={`text-lg font-semibold ${
                    mode === 'dark'
                      ? 'text-[hsl(45,100%,55%)] font-[RuneScape_UF,Times_New_Roman,serif]'
                      : 'text-white font-[VT323,monospace]'
                  }`}>{user?.firstName || 'Guest'}</span>
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </nav>
  );
}
