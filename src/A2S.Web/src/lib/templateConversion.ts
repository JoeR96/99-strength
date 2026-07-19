import { WeightUnit, ExerciseCategory } from "@/types/workout";
import type { SelectedExercise, DayNumber, ExerciseTemplate, ExerciseLibrary } from "@/types/workout";
import type { WorkoutTemplate } from "@/data/workoutTemplates";

// Convert template exercises to SelectedExercise format
export function convertTemplateToSelectedExercises(
  template: WorkoutTemplate,
  exerciseLibrary: ExerciseLibrary
): SelectedExercise[] {
  if (!exerciseLibrary) return [];

  const converted: SelectedExercise[] = template.exercises.map((ex, index) => {
    const templateData = exerciseLibrary.templates.find(t => t.name === ex.templateName);
    return {
      id: `template-${index}`,
      hevyExerciseTemplateId: ex.externalTemplateId || '', // Use template's ID or empty string
      template: templateData || {
        name: ex.templateName,
        equipment: 0,
        description: '',
      } as ExerciseTemplate,
      category: ex.category,
      progressionType: ex.progressionType as 'Linear' | 'RepsPerSet' | 'MinimalSets',
      assignedDay: ex.assignedDay as DayNumber,
      orderInDay: ex.orderInDay,
      trainingMax: ex.trainingMaxValue ? {
        value: ex.trainingMaxValue,
        unit: ex.trainingMaxUnit || WeightUnit.Kilograms,
      } : undefined,
      isPrimary: ex.category === ExerciseCategory.MainLift,
      baseSetsPerExercise: templateData?.defaultSets || 4,
      repRange: (ex.repRangeMinimum != null && ex.repRangeMaximum != null)
        ? { minimum: ex.repRangeMinimum, maximum: ex.repRangeMaximum }
        : templateData?.defaultRepRange,
      currentSets: ex.startingSets ?? templateData?.defaultSets ?? 3,
      targetSets: ex.targetSets ?? (templateData?.defaultSets ?? 3) + 2,
      startingWeight: ex.startingWeight,
      weightUnit: ex.weightUnit || WeightUnit.Kilograms,
      isUnilateral: ex.isUnilateral,
      targetTotalReps: ex.targetTotalReps,
    };
  });

  return converted;
}
