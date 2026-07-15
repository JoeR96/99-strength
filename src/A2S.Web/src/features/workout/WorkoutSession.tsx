import { Link } from "react-router-dom";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Navbar } from "@/components/layout/Navbar";
import { UndoConfirmationModal } from "@/components/shared/UndoConfirmationModal";
import { ExerciseSubstitutionModal } from "./ExerciseSubstitutionModal";
import { EditExerciseConfigModal } from "./EditExerciseConfigModal";
import { ExerciseCard } from "./ExerciseCard";
import { CompletionSummary } from "./CompletionSummary";
import { WeightDiscrepancyModal } from "./WeightDiscrepancyModal";
import { MissingExercisesModal } from "./MissingExercisesModal";
import { WeightConfirmationModal } from "./WeightConfirmationModal";
import { PulledSubstitutionsModal } from "./PulledSubstitutionsModal";
import { SessionRecoveryModal } from "./SessionRecoveryModal";
import { WorkoutHeader } from "./WorkoutHeader";
import { useWorkoutSession } from "./useWorkoutSession";
import type { LinearProgressionDto } from "./workoutSessionTypes";

export function WorkoutSession() {
  const session = useWorkoutSession();

  if (session.isLoading) {
    return (
      <div className="min-h-screen bg-background">
        <Navbar />
        <main className="max-w-4xl mx-auto px-4 py-8"><p>Loading workout...</p></main>
      </div>
    );
  }

  if (!session.workout) {
    return (
      <div className="min-h-screen bg-background">
        <Navbar />
        <main className="max-w-4xl mx-auto px-4 py-8">
          <Card className="p-6 text-center">
            <h2 className="text-xl font-bold mb-4">No Active Workout</h2>
            <p className="text-muted-foreground mb-4">You need to create a workout program first.</p>
            <Link to="/setup"><Button>Create Workout Program</Button></Link>
          </Card>
        </main>
      </div>
    );
  }

  if (session.showCompletionSummary && session.completionResult) {
    return (
      <CompletionSummary
        result={session.completionResult}
        workout={session.workout}
        dayNumber={session.dayNumber}
        dayName={session.dayName}
        exerciseEntries={session.exerciseEntries}
        workoutStartTime={session.workoutStartTime.current}
        workoutEndTime={session.workoutEndTime.current}
        onContinue={() => session.navigate("/workout")}
      />
    );
  }

  return (
    <div className="min-h-screen bg-background">
      <Navbar />

      {/* Sticky Progress Header */}
      <WorkoutHeader
        dayName={session.dayName}
        dayNumber={session.dayNumber}
        currentWeek={session.workout.currentWeek}
        workoutName={session.workout.name}
        isPrefilled={session.isPrefilled}
        completedSetsCount={session.completedSetsCount}
        totalSetsCount={session.totalSetsCount}
        completedExercisesCount={session.completedExercisesCount}
        totalExercisesCount={session.totalExercisesCount}
        progressPercentage={session.progressPercentage}
      />

      <main className="max-w-4xl mx-auto px-4 py-8">
        {/* Exercises */}
        <div className={session.isPrefilled ? "space-y-3" : "space-y-6"}>
          {session.exerciseEntries
            .filter((entry) => entry.sets.length > 0)
            .map((entry, exerciseIndex) => {
              const substitution = session.temporarySubstitutions.find((s) => s.originalExerciseId === entry.exercise.id);
              return (
                <ExerciseCard
                  key={entry.exercise.id}
                  entry={entry}
                  exerciseIndex={exerciseIndex}
                  onSetChange={session.handleSetChange}
                  onSetComplete={session.handleSetComplete}
                  onSubstitute={session.handleOpenSubstitution}
                  onEdit={session.setExerciseToEdit}
                  isTemporarilySubstituted={!!substitution}
                  originalName={substitution?.originalName}
                  defaultCollapsed={session.isPrefilled}
                />
              );
            })}
        </div>

        {/* Complete Workout Button */}
        <div className="mt-8 flex justify-center gap-4">
          {session.workout.completedDaysInCurrentWeek && session.workout.completedDaysInCurrentWeek.length > 0 && (
            <Button variant="outline" onClick={() => session.setShowUndoModal(true)} className="text-destructive border-destructive hover:bg-destructive/10">
              Undo Last Workout
            </Button>
          )}
          <Button size="lg" onClick={session.handleCompleteWorkout} disabled={!session.allSetsCompleted || session.isSubmitting} data-testid="complete-workout-button" className="min-w-[200px]">
            {session.isSubmitting ? "Completing..." : "Complete Workout"}
          </Button>
        </div>

        {!session.allSetsCompleted && (
          <p className="text-center text-sm text-muted-foreground mt-2">Complete all sets to finish the workout</p>
        )}
      </main>

      {/* Modals */}
      {session.exerciseToSubstitute && (
        <ExerciseSubstitutionModal
          exercise={session.exerciseToSubstitute}
          isOpen={session.substitutionModalOpen}
          onClose={() => { session.setSubstitutionModalOpen(false); session.setExerciseToSubstitute(null); }}
          onTemporarySubstitute={session.handleTemporarySubstitute}
          onPermanentSubstitute={session.handlePermanentSubstitute}
        />
      )}

      {session.exerciseToEdit && (
        <EditExerciseConfigModal
          exercise={session.exerciseToEdit}
          isOpen={session.exerciseToEdit !== null}
          onClose={() => session.setExerciseToEdit(null)}
          onSave={session.handleSaveExerciseConfig}
          onChangeProgression={session.handleChangeProgression}
        />
      )}

      {session.showSubstitutionModal && session.pendingSubstitutions.length > 0 && (
        <PulledSubstitutionsModal
          substitutions={session.pendingSubstitutions}
          onApply={session.handleApplySubstitution}
          onRemove={session.handleRemoveFromSubstitution}
          onComplete={session.handleSubstitutionsComplete}
        />
      )}

      {session.showWeightDiscrepancyModal && session.weightDiscrepancies.length > 0 && session.workout && (
        <WeightDiscrepancyModal
          discrepancies={session.weightDiscrepancies}
          exerciseUnit={session.workout.exercises[0]?.progression.type === 'Linear'
            ? (session.workout.exercises[0].progression as LinearProgressionDto).trainingMax.unit === 1 ? 'Kilograms' : 'Pounds'
            : 'Kilograms'}
          currentWeek={session.workout.currentWeek}
          exercises={session.workout.exercises}
          onApply={session.handleApplyWeightDiscrepancy}
          onComplete={session.handleWeightDiscrepanciesComplete}
        />
      )}

      {session.showMissingExercisesModal && session.missingExercises.length > 0 && session.workout && (
        <MissingExercisesModal
          missingExercises={session.missingExercises}
          exerciseUnit={session.workout.exercises[0]?.progression.type === 'Linear'
            ? (session.workout.exercises[0].progression as LinearProgressionDto).trainingMax.unit === 1 ? 'Kilograms' : 'Pounds'
            : 'Kilograms'}
          onApply={session.handleMissingExercise}
          onComplete={session.handleMissingExercisesComplete}
        />
      )}

      {session.showWeightConfirmationModal && session.pendingWeightExercises.length > 0 && (
        <WeightConfirmationModal
          exercises={session.pendingWeightExercises}
          onConfirm={session.handleConfirmWeights}
          onSkip={session.handleSkipWeightConfirmation}
        />
      )}

      {session.workout && (
        <UndoConfirmationModal
          isOpen={session.showUndoModal}
          onClose={() => session.setShowUndoModal(false)}
          onConfirm={session.handleUndoCompletion}
          dayNumber={session.workout.currentDay}
          weekNumber={session.workout.currentWeek}
          wouldRollbackWeek={false}
        />
      )}

      {session.showRecoveryModal && session.savedProgressData && (
        <SessionRecoveryModal
          savedAt={session.savedProgressData.savedAt}
          completedSets={session.savedProgressData.exercises.reduce(
            (acc, ex) => acc + ex.sets.filter((s) => s.completed).length, 0
          )}
          onResume={session.handleResumeProgress}
          onStartFresh={session.handleStartFresh}
        />
      )}
    </div>
  );
}
