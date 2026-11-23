import { describe, it, expect } from 'vitest';
import { hevyFallbackNote, shortDate, suggestionLine } from './startingNumbers';
import { freshSuggestion, staleSuggestion, startingNumbers } from '@/mocks/fixtures/startingNumbers';

describe('starting-number suggestion wording', () => {
  it('names the value, the set and the estimated 1RM', () => {
    expect(suggestionLine(freshSuggestion)).toBe('Suggested 77.5 kg — from 75 kg × 5 on 14 Nov (est. 1RM 87.5 kg)');
  });

  it('says when it comes from older history', () => {
    expect(suggestionLine(staleSuggestion)).toBe('Suggested 80 kg — from 100 kg × 1 on 7 May (est. 1RM 100 kg) · older history — estimated');
  });

  it('reads the date as a calendar day', () => {
    expect(shortDate('2026-01-01')).toBe('1 Jan');
  });

  it('explains a missing suggestion only when Hevy could not be read', () => {
    expect(hevyFallbackNote(startingNumbers.none(), false)).toBeNull();
    expect(hevyFallbackNote(startingNumbers.unavailable(), false)).toBe('Hevy unavailable — enter it yourself.');
    expect(hevyFallbackNote(undefined, true)).toBe('Hevy unavailable — enter it yourself.');
    expect(hevyFallbackNote(startingNumbers.noKey(), false)).toMatch(/Connect Hevy/);
  });
});
