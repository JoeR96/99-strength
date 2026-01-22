import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { ExerciseSelectionV2 } from "./ExerciseSelectionV2/ExerciseSelectionV2";
import { useCreateWorkout } from "@/hooks/useWorkouts";
import { ProgramVariant, WeightUnit, ExerciseCategory } from "@/types/workout";
import type { SelectedExercise, CreateExerciseRequest, DayNumber } from "@/types/workout";
import toast from "react-hot-toast";

type WizardStep = "welcome" | "exercises" | "confirm";

export function SetupWizard() {
  const navigate = useNavigate();
  const createWorkout = useCreateWorkout();

  const [currentStep, setCurrentStep] = useState<WizardStep>("welcome");
  const [workoutName, setWorkoutName] = useState("My A2S Program");
  const [variant, setVariant] = useState<ProgramVariant>(ProgramVariant.FourDay);
  const [totalWeeks, setTotalWeeks] = useState(21);
  const [selectedExercises, setSelectedExercises] = useState<SelectedExercise[]>([]);

  const handleNext = () => {
    const steps: WizardStep[] = ["welcome", "exercises", "confirm"];
    const currentIndex = steps.indexOf(currentStep);
    if (currentIndex < steps.length - 1) {
      setCurrentStep(steps[currentIndex + 1]);
    }
  };

  const handleBack = () => {
    const steps: WizardStep[] = ["welcome", "exercises", "confirm"];
    const currentIndex = steps.indexOf(currentStep);
    if (currentIndex > 0) {
      setCurrentStep(steps[currentIndex - 1]);
    }
  };

  const handleCreateWorkout = async () => {
    try {
      // Map selected exercises to API request format
      const exercises: CreateExerciseRequest[] = selectedExercises.map((ex) => ({
        templateName: ex.template.name,
        category: ex.category ?? ExerciseCategory.MainLift,
        progressionType: ex.progressionType,
        assignedDay: ex.assignedDay as DayNumber,
        orderInDay: ex.orderInDay,
        // Linear progression fields
        trainingMaxValue: ex.trainingMax?.value,
        trainingMaxUnit: ex.trainingMax?.unit,
        // RepsPerSet progression fields
        startingWeight: ex.startingWeight,
        weightUnit: ex.weightUnit,
      }));

      await createWorkout.mutateAsync({
        name: workoutName,
        variant,
        totalWeeks,
        exercises: exercises.length > 0 ? exercises : undefined,
      });

      toast.success("Workout created successfully!");
      navigate("/dashboard");
    } catch (error: unknown) {
      const errorMessage = error instanceof Error ? error.message : "Failed to create workout";
      toast.error(errorMessage);
    }
  };

  const renderStep = () => {
    switch (currentStep) {
      case "welcome":
        return (
          <div className="space-y-6">
            <div>
              <h2 className="text-2xl font-bold mb-2">Welcome to Average to Savage 2.0</h2>
              <p className="text-muted-foreground">
                Let's set up your personalized training program. This wizard will guide you through
                selecting exercises and configuring your progression settings.
              </p>
            </div>

            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium mb-1">Program Name</label>
                <input
                  type="text"
                  value={workoutName}
                  onChange={(e) => setWorkoutName(e.target.value)}
                  className="w-full px-3 py-2 border rounded-md"
                  placeholder="My A2S Program"
                />
              </div>

              <div>
                <label className="block text-sm font-medium mb-1">Program Variant</label>
                <select
                  value={variant}
                  onChange={(e) => setVariant(Number(e.target.value) as ProgramVariant)}
                  className="w-full px-3 py-2 border rounded-md"
                >
                  <option value={ProgramVariant.FourDay}>4-Day</option>
                  <option value={ProgramVariant.FiveDay}>5-Day</option>
                  <option value={ProgramVariant.SixDay}>6-Day</option>
                </select>
              </div>

              <div>
                <label className="block text-sm font-medium mb-1">Total Weeks</label>
                <input
                  type="number"
                  value={totalWeeks}
                  onChange={(e) => setTotalWeeks(Number(e.target.value))}
                  className="w-full px-3 py-2 border rounded-md"
                  min="1"
                  max="21"
                />
                <p className="text-sm text-muted-foreground mt-1">
                  Standard program is 21 weeks (3 blocks of 7 weeks)
                </p>
              </div>
            </div>
          </div>
        );

      case "exercises":
        return (
          <ExerciseSelectionV2
            selectedExercises={selectedExercises}
            onUpdate={setSelectedExercises}
            programVariant={variant}
          />
        );

      case "confirm":
        return (
          <div className="space-y-6">
            <div>
              <h2 className="text-2xl font-bold mb-2">Review Your Program</h2>
              <p className="text-muted-foreground">
                Please review your selections before creating your workout program.
              </p>
            </div>

            <div className="space-y-4">
              <div>
                <h3 className="font-semibold">Program Details</h3>
                <ul className="mt-2 space-y-1 text-sm">
                  <li><span className="text-muted-foreground">Name:</span> {workoutName}</li>
                  <li><span className="text-muted-foreground">Variant:</span> {variant}-Day</li>
                  <li><span className="text-muted-foreground">Duration:</span> {totalWeeks} weeks</li>
                </ul>
              </div>

              <div>
                <h3 className="font-semibold">Selected Exercises</h3>
                {selectedExercises.length === 0 ? (
                  <p className="mt-2 text-sm text-muted-foreground">
                    No exercises selected yet
                  </p>
                ) : (
                  <div className="mt-2 space-y-3">
                    {Object.entries(
                      selectedExercises.reduce((acc, ex) => {
                        if (!acc[ex.assignedDay]) acc[ex.assignedDay] = [];
                        acc[ex.assignedDay].push(ex);
                        return acc;
                      }, {} as Record<number, SelectedExercise[]>)
                    )
                      .sort(([a], [b]) => Number(a) - Number(b))
                      .map(([day, exercises]) => (
                        <div key={day}>
                          <h4 className="text-sm font-medium">Day {day}</h4>
                          <ul className="mt-1 space-y-2 text-sm text-muted-foreground ml-4">
                            {exercises
                              .sort((a, b) => a.orderInDay - b.orderInDay)
                              .map((ex) => (
                                <li key={ex.id} className="space-y-1">
                                  <div className="font-medium text-foreground">
                                    {ex.template.name}
                                  </div>
                                  <div className="text-xs">
                                    {ex.progressionType === 'Linear' ? 'A2S Linear' : 'A2S Reps Per Set'}
                                  </div>
                                  {ex.progressionType === 'Linear' && ex.trainingMax && (
                                    <div className="text-xs">
                                      Training Max: {ex.trainingMax.value}{ex.trainingMax.unit === WeightUnit.Kilograms ? 'kg' : 'lbs'}
                                      {' • '}
                                      {ex.isPrimary ? 'Primary' : 'Auxiliary'}
                                      {' • '}
                                      {ex.baseSetsPerExercise} sets
                                    </div>
                                  )}
                                  {ex.progressionType === 'RepsPerSet' && ex.repRange && (
                                    <div className="text-xs">
                                      Reps: {ex.repRange.minimum}-{ex.repRange.target}-{ex.repRange.maximum}
                                      {' • '}
                                      Sets: {ex.currentSets} → {ex.targetSets}
                                      {' • '}
                                      Starting: {ex.startingWeight}{ex.weightUnit === WeightUnit.Kilograms ? 'kg' : 'lbs'}
                                    </div>
                                  )}
                                </li>
                              ))}
                          </ul>
                        </div>
                      ))}
                  </div>
                )}
              </div>
            </div>
          </div>
        );

      default:
        return null;
    }
  };

  const canProceed = () => {
    switch (currentStep) {
      case "welcome":
        return workoutName.trim().length > 0;
      case "exercises":
        return true; // Optional exercise selection
      case "confirm":
        return true;
      default:
        return false;
    }
  };

  return (
    <div className="max-w-3xl mx-auto p-6">
      <Card className="p-8">
        {/* Progress indicator */}
        <div className="mb-8">
          <div className="flex items-center justify-between text-sm">
            <span className={currentStep === "welcome" ? "font-bold" : ""}>Welcome</span>
            <span className={currentStep === "exercises" ? "font-bold" : ""}>Exercises</span>
            <span className={currentStep === "confirm" ? "font-bold" : ""}>Confirm</span>
          </div>
          <div className="mt-2 h-2 bg-muted rounded-full overflow-hidden">
            <div
              className="h-full bg-primary transition-all duration-300"
              style={{
                width:
                  currentStep === "welcome"
                    ? "33%"
                    : currentStep === "exercises"
                    ? "66%"
                    : "100%",
              }}
            />
          </div>
        </div>

        {/* Step content */}
        <div className="min-h-[400px]">{renderStep()}</div>

        {/* Navigation buttons */}
        <div className="mt-8 flex justify-between">
          <Button
            variant="outline"
            onClick={handleBack}
            disabled={currentStep === "welcome"}
          >
            Back
          </Button>

          {currentStep === "confirm" ? (
            <Button
              onClick={handleCreateWorkout}
              disabled={!canProceed() || createWorkout.isPending}
            >
              {createWorkout.isPending ? "Creating..." : "Create Program"}
            </Button>
          ) : (
            <Button onClick={handleNext} disabled={!canProceed()}>
              Next
            </Button>
          )}
        </div>
      </Card>
    </div>
  );
}
