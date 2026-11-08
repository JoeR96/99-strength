/** Parses a typed weight ("94,6", "94.6") into kg to one decimal; null when it is not a plausible weight. */

export function parseKg(raw: string): number | null {
  const normalised = raw.trim().replace(',', '.');
  if (!/^\d{1,3}([.]\d{0,2})?$/.test(normalised)) return null;
  const kg = Number(normalised);
  if (!Number.isFinite(kg) || kg < 20 || kg > 400) return null;
  return Math.round(kg * 10) / 10;
}
