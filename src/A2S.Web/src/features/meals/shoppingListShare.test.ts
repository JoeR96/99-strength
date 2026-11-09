import { afterEach, describe, expect, it, vi } from 'vitest';
import { copyText, formatQuantity, itemLabel, shareText } from './shoppingListShare';

const item = (quantity: number | null, unit: string | null, name = 'garlic') => ({
  name,
  quantity,
  unit,
  checked: false,
  fromMealIds: [],
});

describe('shoppingListShare', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('labels items like the server export', () => {
    expect(itemLabel(item(6, 'clove'))).toBe('6 clove garlic');
    expect(itemLabel(item(1.2, 'kg', 'beef chuck'))).toBe('1.2 kg beef chuck');
    expect(itemLabel(item(null, null, 'salt'))).toBe('salt');
  });

  it('formats quantities without trailing zeros', () => {
    expect(formatQuantity(6)).toBe('6');
    expect(formatQuantity(0.5)).toBe('0.5');
    expect(formatQuantity(1.5)).toBe('1.5');
    expect(formatQuantity(0.3333)).toBe('0.333');
  });

  it('copies through the clipboard and reports failure', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { clipboard: { writeText } });

    expect(await copyText('- [ ] salt')).toBe(true);
    expect(writeText).toHaveBeenCalledWith('- [ ] salt');

    vi.stubGlobal('navigator', {});
    expect(await copyText('x')).toBe(false);
  });

  it('shares through navigator.share when available', async () => {
    const share = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { share });

    expect(await shareText('Shopping list', 'text')).toBe('shared');
    expect(share).toHaveBeenCalledWith({ title: 'Shopping list', text: 'text' });
  });

  it('falls back to the clipboard when share is missing', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { clipboard: { writeText } });

    expect(await shareText('Shopping list', 'text')).toBe('copied');
    expect(writeText).toHaveBeenCalledWith('text');
  });

  it('does not copy when the share sheet was dismissed', async () => {
    const abort = Object.assign(new Error('cancelled'), { name: 'AbortError' });
    const writeText = vi.fn();
    vi.stubGlobal('navigator', { share: vi.fn().mockRejectedValue(abort), clipboard: { writeText } });

    expect(await shareText('Shopping list', 'text')).toBe('unsupported');
    expect(writeText).not.toHaveBeenCalled();
  });
});
