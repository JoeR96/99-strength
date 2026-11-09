import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import { Navbar } from '@/components/layout/Navbar';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { mealsApi } from '@/api/meals';
import { useGenerateShoppingList, useShoppingList, useToggleShoppingItem } from '@/hooks/useMeals';
import type { ShoppingListItemDto } from '@/types/meals';
import { WeekPicker } from './WeekPicker';
import { resolveWeek } from './week';
import { copyText, downloadTextFile, itemLabel, shareText } from './shoppingListShare';

type ExportKind = 'copy' | 'share' | 'csv';

/**
 * The week's shopping list on the phone: big tick rows, progress, and Copy / Share / CSV using the
 * server's export so what you send is exactly what the API renders.
 */
export function ShoppingListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const week = resolveWeek(searchParams.get('week'));
  const [busy, setBusy] = useState<ExportKind | null>(null);

  const list = useShoppingList(week);
  const toggle = useToggleShoppingItem(week);
  const generate = useGenerateShoppingList(week);

  const changeWeek = (next: string) => setSearchParams({ week: next });
  const data = list.data;
  const hasItems = !!data && data.items.length > 0;

  const onToggle = (item: ShoppingListItemDto) => {
    if (!data?.id) return;
    toggle.mutate(
      { listId: data.id, name: item.name, unit: item.unit },
      { onError: () => toast.error('Could not update that item') }
    );
  };

  const exportAs = async (kind: ExportKind) => {
    setBusy(kind);
    try {
      if (kind === 'csv') {
        const csv = await mealsApi.exportShoppingList(week, 'csv');
        downloadTextFile(csv, `shopping-list-${week}.csv`, 'text/csv;charset=utf-8');
        return;
      }
      const text = await mealsApi.exportShoppingList(week, 'text');
      if (kind === 'copy') {
        if (await copyText(text)) toast.success('Copied to clipboard');
        else toast.error('Could not copy the list');
        return;
      }
      const outcome = await shareText(`Shopping list ${week}`, text);
      if (outcome === 'copied') toast.success('Sharing is not available here, so it is on your clipboard');
      else if (outcome === 'unsupported') toast.error('Could not share the list');
    } catch {
      toast.error('Could not export the list');
    } finally {
      setBusy(null);
    }
  };

  const regenerate = async () => {
    try {
      const next = await generate.mutateAsync();
      toast.success(`List rebuilt: ${next.totalCount} items`);
    } catch {
      toast.error('Could not rebuild the list');
    }
  };

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <main className="container-page py-8 space-y-6">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <h1 className="text-hero">Shopping list</h1>
            <p className="text-caption mt-2">One list for the week, summed across your meals. Tap a row to tick it off.</p>
          </div>
          <Button asChild variant="outline">
            <Link to={`/meals?week=${week}`}>Change meals</Link>
          </Button>
        </div>

        <WeekPicker week={week} onChange={changeWeek} />

        {list.isLoading && <p className="text-caption text-center py-12">Loading list...</p>}
        {list.isError && (
          <Card className="p-6 text-center">
            <p className="text-destructive">Failed to load the shopping list</p>
            <Button className="mt-4" onClick={() => list.refetch()}>Retry</Button>
          </Card>
        )}

        {data && !hasItems && (
          <Card className="p-8 text-center space-y-4">
            <h2 className="text-2xl font-semibold text-foreground">Nothing on the list yet</h2>
            <p className="text-caption">
              {data.sourceMealIds.length > 0
                ? 'You have picked meals for this week but have not generated the list.'
                : 'Pick some meals for this week first.'}
            </p>
            <div className="flex flex-wrap justify-center gap-3">
              <Button asChild variant="outline">
                <Link to={`/meals?week=${week}`}>Pick meals</Link>
              </Button>
              {data.sourceMealIds.length > 0 && (
                <Button onClick={regenerate} disabled={generate.isPending}>
                  {generate.isPending ? 'Generating...' : 'Generate list'}
                </Button>
              )}
            </div>
          </Card>
        )}

        {data && hasItems && (
          <>
            <Progress checked={data.checkedCount} total={data.totalCount} />

            <ul className="space-y-2" aria-label="Shopping list items">
              {data.items.map((item) => (
                <ItemRow key={`${item.name}|${item.unit ?? ''}`} item={item} onToggle={() => onToggle(item)} />
              ))}
            </ul>

            <div className="sticky bottom-4 grid grid-cols-3 gap-3 rounded-lg border border-border bg-card p-4">
              <Button variant="secondary" size="lg" disabled={busy !== null} onClick={() => exportAs('copy')}>
                {busy === 'copy' ? 'Copying...' : 'Copy'}
              </Button>
              <Button variant="secondary" size="lg" disabled={busy !== null} onClick={() => exportAs('share')}>
                {busy === 'share' ? 'Sharing...' : 'Share'}
              </Button>
              <Button variant="secondary" size="lg" disabled={busy !== null} onClick={() => exportAs('csv')}>
                {busy === 'csv' ? 'Saving...' : 'CSV'}
              </Button>
            </div>

            <div className="text-center">
              <Button variant="ghost" onClick={regenerate} disabled={generate.isPending}>
                {generate.isPending ? 'Rebuilding...' : 'Rebuild from this week’s meals'}
              </Button>
            </div>
          </>
        )}
      </main>
    </div>
  );
}

function Progress({ checked, total }: { checked: number; total: number }) {
  const percent = total === 0 ? 0 : Math.round((checked / total) * 100);
  return (
    <div className="space-y-2">
      <div className="flex items-baseline justify-between">
        <span className="text-body font-semibold">
          {checked} of {total} ticked
        </span>
        <span className="text-caption">{percent}%</span>
      </div>
      <div
        className="h-2 w-full overflow-hidden rounded-full bg-muted"
        role="progressbar"
        aria-valuemin={0}
        aria-valuemax={total}
        aria-valuenow={checked}
        aria-label="Items ticked"
      >
        <div className="h-full bg-primary transition-all duration-300" style={{ width: `${percent}%` }} />
      </div>
    </div>
  );
}

function ItemRow({ item, onToggle }: { item: ShoppingListItemDto; onToggle: () => void }) {
  return (
    <li>
      <button
        type="button"
        onClick={onToggle}
        aria-pressed={item.checked}
        className={cn(
          'flex min-h-14 w-full items-center gap-4 rounded-lg border bg-card px-4 text-left transition-colors',
          item.checked ? 'border-border/60' : 'border-border hover:border-foreground/40'
        )}
      >
        <span
          aria-hidden="true"
          className={cn(
            'flex h-7 w-7 shrink-0 items-center justify-center rounded-md border-2',
            item.checked ? 'border-primary bg-primary text-primary-foreground' : 'border-muted-foreground'
          )}
        >
          {item.checked && (
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={3} className="h-4 w-4">
              <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
            </svg>
          )}
        </span>
        <span className={cn('text-body flex-1', item.checked && 'text-muted-foreground line-through')}>
          {itemLabel(item)}
        </span>
        {item.fromMealIds.length > 1 && (
          <span className="text-caption shrink-0">{item.fromMealIds.length} meals</span>
        )}
      </button>
    </li>
  );
}
