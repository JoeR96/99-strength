import { useState, useEffect, useRef } from 'react';
import { UserButton, useUser } from '@clerk/clerk-react';
import { Link, useLocation } from 'react-router-dom';

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
              <h1 className="text-xl font-bold hidden sm:block text-foreground">
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
                    className={`px-4 py-2 rounded text-sm font-medium transition-all duration-150 ${
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

            {/* Player Name */}
            <div className="hidden sm:flex items-center gap-2 px-4 py-2 rounded border border-border bg-muted">
              <span className="text-sm font-medium text-muted-foreground">
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
                      className={`block px-4 py-3 rounded text-base font-medium transition-all duration-150 ${
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
                  <span className="text-sm font-medium text-muted-foreground">Player:</span>
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
