import type { GateProgressDto } from '@/types/cardioTracks';

/** The Walk → run running gate, either part-way or fully met. */
export function runningGate(met: boolean): GateProgressDto {
  const conditions = [
    {
      condition: { type: 'TrendWeightAtOrBelow' as const, kg: 90 },
      met,
      label: 'Trend weight ≤ 90 kg',
      progress: met ? 'Trend 89.6 kg' : 'Trend 91.3 kg, 1.3 kg to go',
    },
    {
      condition: { type: 'TrackAtOrAbove' as const, trackTemplateKey: 'walk-builder', minutes: 45, intensity: 8, effort: 4, weeks: 2 },
      met,
      label: 'Walk builder ≥ 45 min at ≥ 8 % incline, rated Easy, for 2 weeks',
      progress: met ? '2 of 2 weeks' : '1 of 2 weeks',
    },
    {
      condition: { type: 'NoPainInAreas' as const, areas: ['knee', 'shin', 'Achilles'], weeks: 4 },
      met,
      label: 'No knee/shin/Achilles pain for 4 weeks',
      progress: met ? 'Clear' : 'knee pain on 12 Nov; clear from 10 Dec',
    },
  ];
  return { conditions, allMet: met, metCount: met ? 3 : 0 };
}
