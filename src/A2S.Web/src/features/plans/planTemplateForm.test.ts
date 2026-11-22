import { describe, it, expect } from 'vitest';
import { planV3Preview } from '@/mocks/fixtures/planTemplates';
import { replaceConfirmation } from '@/api/planTemplates';
import { buildApplyRequest, snapToMonday } from './planTemplateForm';

describe('snapToMonday', () => {
  it('snaps to the Monday on or after the date (local dates, Europe/London)', () => {
    expect(snapToMonday('2026-11-22')).toBe('2026-11-23');
    expect(snapToMonday('2026-11-23')).toBe('2026-11-23');
    expect(snapToMonday('2026-11-25')).toBe('2026-11-30');
    expect(snapToMonday('2027-03-28')).toBe('2027-03-29');
    expect(snapToMonday('nope')).toBeNull();
  });
});

describe('buildApplyRequest', () => {
  it('sends blank optional training maxes as null (pending) and the default start weight', () => {
    const { request, errors } = buildApplyRequest(planV3Preview.inputs, { benchTm: '80', startWeightKg: '' }, '2026-11-23');

    expect(errors).toEqual({});
    expect(request).toEqual({
      startDate: '2026-11-23',
      trainingMaxes: { benchTm: 80, pauseSquatTm: null, inclineSmithTm: null, frontSquatTm: null },
      startWeightKg: 95,
      confirmReplace: false,
    });
  });

  it('rejects out-of-range values and keeps confirmReplace when resending', () => {
    expect(buildApplyRequest(planV3Preview.inputs, { benchTm: '5' }, '2026-11-23').errors).toEqual({ benchTm: 'Between 20 and 400 kg' });
    expect(buildApplyRequest(planV3Preview.inputs, {}, '2026-11-23', true).request?.confirmReplace).toBe(true);
  });

  it('requires inputs that are not optional and have no default', () => {
    const inputs = [{ key: 'squatTm', label: 'Squat TM', unit: 'kg', default: null, minimum: 20, maximum: 400 }];

    expect(buildApplyRequest(inputs, {}, '2026-11-23').errors).toEqual({ squatTm: 'Enter Squat TM (kg)' });
  });
});

describe('replaceConfirmation', () => {
  it('reads the active plan name from a 409 PlanReplaceNeedsConfirmation', () => {
    const error = { response: { status: 409, data: { code: 'PlanReplaceNeedsConfirmation', activePlanName: 'Cut to 85' } } };

    expect(replaceConfirmation(error)).toEqual({ activePlanName: 'Cut to 85' });
    expect(replaceConfirmation({ response: { status: 500 } })).toBeNull();
  });

  it('reads the name from a plain 409 ProblemDetails detail', () => {
    const error = { response: { status: 409, data: { title: 'Conflict', detail: 'You already have an active plan (Cut to 85 — Nov 2026). Confirm to replace it: resend with confirmReplace.' } } };

    expect(replaceConfirmation(error)).toEqual({ activePlanName: 'Cut to 85 — Nov 2026' });
  });
});
