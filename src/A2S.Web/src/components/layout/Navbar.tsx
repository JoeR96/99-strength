import { UserButton, useUser } from '@clerk/clerk-react';
import { Link, useLocation } from 'react-router-dom';
import { TABS, isTabActive } from './tabs';
import { TabBadge } from './TabBadge';
import { usePendingSyncCount } from '@/offline/context';

/**
 * Top bar. On phones it is just the logo and the account button — navigation lives in the
 * BottomTabBar. On large screens the five tabs render inline as links.
 */
export function Navbar() {
  const { user } = useUser();
  const location = useLocation();
  const pending = usePendingSyncCount();

  return (
    <nav className="sticky top-0 z-30 border-b border-border surface-bar" style={{ paddingTop: 'env(safe-area-inset-top)' }}>
      <div className="container-page">
        <div className="flex h-12 items-center justify-between lg:h-14">
          <div className="flex items-center gap-8">
            <Link to="/today" className="flex min-h-11 min-w-11 items-center gap-2.5" aria-label="99 Strength home">
              <div className="flex h-9 w-9 items-center justify-center rounded-[10px] bg-primary">
                <span className="text-base font-bold font-display text-primary-foreground">99</span>
              </div>
              <span className="hidden text-lg font-semibold tracking-tight text-foreground sm:block">Strength</span>
            </Link>

            <div className="hidden items-center gap-1 lg:flex">
              {TABS.map((tab) => {
                const active = isTabActive(tab, location.pathname);
                return (
                  <Link
                    key={tab.to}
                    to={tab.to}
                    aria-current={active ? 'page' : undefined}
                    className={`rounded-full px-3.5 py-1.5 text-sm font-medium transition-colors ${
                      active
                        ? 'bg-foreground/10 text-foreground'
                        : 'text-muted-foreground hover:text-foreground'
                    }`}
                  >
                    <span className="relative">
                      {tab.label}
                      {tab.badge === 'pendingSync' && <TabBadge count={pending} />}
                    </span>
                  </Link>
                );
              })}
            </div>
          </div>

          <div className="flex items-center gap-3">
            <span className="hidden text-sm font-medium text-muted-foreground sm:block">
              {user?.firstName || 'Guest'}
            </span>
            <UserButton
              afterSignOutUrl="/sign-in"
              appearance={{ elements: { avatarBox: 'h-9 w-9 rounded-full' } }}
            />
          </div>
        </div>
      </div>
    </nav>
  );
}
