import type { ShoppingListItemDto } from '@/types/meals';

/**
 * Copy, share and download for the shopping list, as in CooksAssistant's shopping-list-export:
 * share through the Web Share API when the browser has it, otherwise fall back to the clipboard.
 */

export type ShareOutcome = 'shared' | 'copied' | 'unsupported';

/** "2 clove garlic", "200 g orzo", or just "salt" when unquantified. Mirrors the server's label. */
export function itemLabel(item: ShoppingListItemDto): string {
  if (item.quantity === null || item.quantity === undefined) return item.name;
  return `${formatQuantity(item.quantity)} ${item.unit} ${item.name}`;
}

/** Up to three decimals, no trailing zeros: 6, 1.2, 0.5. */
export function formatQuantity(quantity: number): string {
  return Number(quantity.toFixed(3)).toString();
}

export async function copyText(text: string): Promise<boolean> {
  if (typeof navigator === 'undefined' || !navigator.clipboard?.writeText) return false;
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    return false;
  }
}

export async function shareText(title: string, text: string): Promise<ShareOutcome> {
  if (typeof navigator !== 'undefined' && typeof navigator.share === 'function') {
    try {
      await navigator.share({ title, text });
      return 'shared';
    } catch (error) {
      // The person closed the share sheet: nothing to fall back to.
      if (error instanceof Error && error.name === 'AbortError') return 'unsupported';
    }
  }
  return (await copyText(text)) ? 'copied' : 'unsupported';
}

export function downloadTextFile(content: string, fileName: string, mimeType: string): void {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}
