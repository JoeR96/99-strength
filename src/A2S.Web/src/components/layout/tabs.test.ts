import { describe, it, expect } from 'vitest';
import { TABS, isTabActive } from './tabs';

const activeFor = (pathname: string) => TABS.filter((tab) => isTabActive(tab, pathname)).map((tab) => tab.label);

describe('tabs', () => {
  it('lights exactly one tab for each main route', () => {
    expect(activeFor('/today')).toEqual(['Today']);
    expect(activeFor('/workout/session/1')).toEqual(['Plan']);
    expect(activeFor('/habits')).toEqual(['Habits']);
    expect(activeFor('/progress')).toEqual(['Progress']);
    expect(activeFor('/settings')).toEqual(['Settings']);
  });

  it('the Hevy inbox belongs to Today, Hevy data to Progress and Hevy routines to Settings', () => {
    expect(activeFor('/hevy/inbox')).toEqual(['Today']);
    expect(activeFor('/hevy/data')).toEqual(['Progress']);
    expect(activeFor('/hevy')).toEqual(['Settings']);
  });

  it('matches whole path segments only', () => {
    expect(activeFor('/todayish')).toEqual([]);
    expect(activeFor('/hevy/inboxes')).toEqual(['Settings']);
  });
});
