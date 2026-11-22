/** Plain-English helpers shared by the Tracks page, Today and Progress. */

import { CardioTrackStatus, TrackStageKind, type CardioTrackDto, type TrackStageDto, intensityLabel, IntensityUnit, VolumeUnit } from '@/types/cardioTracks';

/** "1 more session to step up", "Waiting for you", or "Paused". */
export function stepUpText(track: CardioTrackDto): string {
  if (track.status === CardioTrackStatus.Paused) return 'Paused: sessions are recorded but the track holds';
  if (track.suggestionText) return 'Waiting for your decision';
  const n = track.sessionsToStepUp;
  if (track.state.stageComplete && track.stages.length - 1 === track.state.stageIndex) return 'At the top: holding here';
  return n === 1 ? '1 more session to step up' : `${n} more sessions to step up`;
}

/** One line describing a stage's rule, e.g. "20 → 45 min, +5 a step · 2 good sessions per step". */
export function describeStage(stage: TrackStageDto): string {
  if (stage.kind === TrackStageKind.WeekProgram && stage.weekProgram) {
    const w = stage.weekProgram;
    return `${w.weeks.length} weeks, ${w.sessionsPerWeek} a week · move on after ${w.sessionsToAdvance} good sessions`;
  }
  const d = stage.doubleProgression!;
  const unit = d.volumeUnit === VolumeUnit.Rounds ? 'rounds' : 'min';
  const step = d.volumeStepPercent ? `+${d.volumeStepPercent}%` : `+${d.volumeStep}`;
  const volume = d.volumeMin === d.volumeMax ? `hold ${d.volumeMin} ${unit}` : `${d.volumeMin} → ${d.volumeMax} ${unit}, ${step} a step`;
  const intensity =
    d.intensityUnit === IntensityUnit.None
      ? ''
      : ` · then ${intensityLabel[d.intensityUnit].toLowerCase()} +${d.intensityStep}${d.intensityMax !== null ? ` up to ${d.intensityMax}` : ''}`;
  return `${volume}${intensity} · ${d.sessionsToAdvance} good sessions per step`;
}
