import { describe, it, expect } from 'vitest';
import { TABS, isTabActive } from './tabs';

const activeFor = (pathname: string) => TABS.filter((tab) => isTabActive(tab, pathname)).map((tab) => tab.label);

describe('tabs', () => {
  it('lights exactly one tab for each main route', () => {
    expect(activeFor('/today')).toEqual(['Workout']);
    expect(activeFor('/current')).toEqual(['Workout']);
    expect(activeFor('/gym/1')).toEqual(['Workout']);
    expect(activeFor('/workout/session/1')).toEqual(['Workout']);
    expect(activeFor('/routines')).toEqual(['Mobility']);
    expect(activeFor('/cardio')).toEqual(['Progress']);
    expect(activeFor('/habits')).toEqual(['Habits']);
    expect(activeFor('/progress')).toEqual(['Progress']);
    expect(activeFor('/settings')).toEqual(['Settings']);
  });

  it('the Hevy inbox belongs to Workout, Hevy data to Progress and Hevy routines to Settings', () => {
    expect(activeFor('/hevy/inbox')).toEqual(['Workout']);
    expect(activeFor('/hevy/data')).toEqual(['Progress']);
    expect(activeFor('/hevy')).toEqual(['Settings']);
  });

  it('matches whole path segments only', () => {
    expect(activeFor('/todayish')).toEqual([]);
    expect(activeFor('/hevy/inboxes')).toEqual(['Settings']);
  });
});
