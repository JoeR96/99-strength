/**
 * Phone navigation: five fixed tabs, 56 px tall, safe-area aware.
 * Hidden on large screens where the top Navbar carries the links.
 */

import { Link, useLocation } from 'react-router-dom';
import { TABS, isTabActive } from './tabs';

export function BottomTabBar() {
  const { pathname } = useLocation();

  return (
    <nav
      aria-label="Primary"
      className="fixed inset-x-0 bottom-0 z-40 border-t border-border bg-card/95 backdrop-blur lg:hidden"
      style={{ paddingBottom: 'env(safe-area-inset-bottom)' }}
    >
      <ul className="grid grid-cols-5">
        {TABS.map((tab) => {
          const active = isTabActive(tab, pathname);
          const Icon = tab.icon;
          return (
            <li key={tab.to}>
              <Link
                to={tab.to}
                aria-current={active ? 'page' : undefined}
                className={`flex h-14 min-w-14 flex-col items-center justify-center gap-0.5 text-[11px] font-medium transition-colors ${
                  active ? 'text-primary' : 'text-muted-foreground hover:text-foreground'
                }`}
              >
                <Icon className="h-6 w-6" aria-hidden="true" strokeWidth={active ? 2.5 : 2} />
                <span>{tab.label}</span>
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
