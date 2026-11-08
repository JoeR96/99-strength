/** The small count on a tab icon: how many writes are waiting to sync. Hidden at 0. */

export function TabBadge({ count }: { count: number }) {
  if (count <= 0) return null;
  return (
    <span
      className="absolute -right-2.5 -top-1.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-warning px-1 text-[10px] font-semibold leading-none text-background"
      aria-label={`${count} ${count === 1 ? 'change' : 'changes'} waiting to sync`}
      data-testid="tab-pending-badge"
    >
      {count > 99 ? '99+' : count}
    </span>
  );
}
