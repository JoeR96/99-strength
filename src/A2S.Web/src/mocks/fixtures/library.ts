/**
 * GET /workouts/exercises/library — the exercise definitions the setup wizard and
 * exercise picker browse. Built from the same Hevy catalogue the backend seeds from.
 */
import { HEVY_EXERCISE_MAPPING } from '@/data/hevyExercises';
import { EquipmentType, type ExerciseLibrary, type ExerciseTemplate } from '@/types/workout';

function equipmentOf(title: string, hevyEquipment: string): EquipmentType {
  if (/\(Smith Machine\)/i.test(title)) return EquipmentType.SmithMachine;
  if (/\(Cable\)|Cable|Pushdown|Pulldown|Face Pull/i.test(title)) return EquipmentType.Cable;
  switch (hevyEquipment) {
    case 'barbell':
      return EquipmentType.Barbell;
    case 'dumbbell':
    case 'kettlebell':
      return EquipmentType.Dumbbell;
    case 'none':
    case 'other':
      return EquipmentType.Bodyweight;
    default:
      return EquipmentType.Machine;
  }
}

const LABELS: Record<string, string> = {
  quadriceps: 'Quads',
  upper_back: 'Upper back',
  lower_back: 'Lower back',
  full_body: 'Full body',
};

export const exerciseLibrary: ExerciseLibrary = {
  templates: Object.values(HEVY_EXERCISE_MAPPING)
    .filter((e) => !e.is_custom)
    .map((e): ExerciseTemplate => {
      const equipment = equipmentOf(e.title, e.equipment);
      const heavy = equipment === EquipmentType.Barbell;
      const group =
        LABELS[e.muscle_group] ?? e.muscle_group.charAt(0).toUpperCase() + e.muscle_group.slice(1);
      return {
        name: e.title,
        equipment,
        defaultRepRange: heavy ? { minimum: 6, maximum: 10 } : { minimum: 10, maximum: 15 },
        defaultSets: heavy ? 4 : 3,
        description: group,
      };
    })
    .sort((a, b) => a.name.localeCompare(b.name)),
};
