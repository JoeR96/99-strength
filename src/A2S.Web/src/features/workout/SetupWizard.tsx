import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ExerciseSelectionV2 } from "./ExerciseSelectionV2/ExerciseSelectionV2";
import { useCreateWorkout, useExerciseLibrary } from "@/hooks/useWorkouts";
import { ProgramVariant, WeightUnit, ExerciseCategory } from "@/types/workout";
import type { SelectedExercise, CreateExerciseRequest, DayNumber, ExerciseTemplate } from "@/types/workout";
import toast from "react-hot-toast";
import { workoutTemplates, type WorkoutTemplate } from "@/data/workoutTemplates";

type WizardStep = "welcome" | "template" | "exercises" | "confirm";
type SetupMode = "template" | "scratch";

export function SetupWizard() {
  const navigate = useNavigate();
  const createWorkout = useCreateWorkout();
  const { data: exerciseLibrary } = useExerciseLibrary();

  const [currentStep, setCurrentStep] = useState<WizardStep>("welcome");
  const [setupMode, setSetupMode] = useState<SetupMode | null>(null);
  const [selectedTemplate, setSelectedTemplate] = useState<WorkoutTemplate | null>(null);
  const [workoutName, setWorkoutName] = useState("My A2S Program");
  const [variant, setVariant] = useState<ProgramVariant>(ProgramVariant.FourDay);
  const [totalWeeks, setTotalWeeks] = useState(21);
  const [selectedExercises, setSelectedExercises] = useState<SelectedExercise[]>([]);

  // Convert template exercises to SelectedExercise format
  const applyTemplate = (template: WorkoutTemplate) => {
    if (!exerciseLibrary) return;

    const converted: SelectedExercise[] = template.exercises.map((ex, index) => {
      const templateData = exerciseLibrary.templates.find(t => t.name === ex.templateName);
      return {
        id: `template-${index}`,
        template: templateData || {
          name: ex.templateName,
          equipment: 0,
          description: '',
        } as ExerciseTemplate,
        category: ex.category,
        progressionType: ex.progressionType as 'Linear' | 'RepsPerSet',
        assignedDay: ex.assignedDay as DayNumber,
        orderInDay: ex.orderInDay,
        trainingMax: ex.trainingMaxValue ? {
          value: ex.trainingMaxValue,
          unit: ex.trainingMaxUnit || WeightUnit.Kilograms,
        } : undefined,
        isPrimary: ex.category === ExerciseCategory.MainLift,
        baseSetsPerExercise: templateData?.defaultSets || 4,
        repRange: templateData?.defaultRepRange,
        currentSets: templateData?.defaultSets || 3,
        targetSets: (templateData?.defaultSets || 3) + 2,
        startingWeight: ex.startingWeight,
        weightUnit: ex.weightUnit || WeightUnit.Kilograms,
      };
    });

    setSelectedExercises(converted);
    setWorkoutName(template.name);
    setVariant(template.variant as ProgramVariant);
    setTotalWeeks(template.totalWeeks);
  };

  const getSteps = (): WizardStep[] => {
    if (setupMode === "template") {
      return ["welcome", "template", "exercises", "confirm"];
    }
    return ["welcome", "exercises", "confirm"];
  };

  const handleNext = () => {
    const steps = getSteps();
    const currentIndex = steps.indexOf(currentStep);
    if (currentIndex < steps.length - 1) {
      setCurrentStep(steps[currentIndex + 1]);
    }
  };

  const handleBack = () => {
    const steps = getSteps();
    const currentIndex = steps.indexOf(currentStep);
    if (currentIndex > 0) {
      setCurrentStep(steps[currentIndex - 1]);
    }
  };

  const handleSelectTemplate = (template: WorkoutTemplate) => {
    setSelectedTemplate(template);
    applyTemplate(template);
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
          <div className="space-y-8">
            <div className="text-center">
              <div className="mb-4 flex justify-center">
                <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-primary/10 border border-primary/20">
                  <svg className="h-8 w-8 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
                  </svg>
                </div>
              </div>
              <h2 className="text-3xl font-bold mb-3">Welcome to Average to Savage 2.0</h2>
              <p className="text-muted-foreground max-w-md mx-auto">
                Let's set up your personalized training program. Choose how you'd like to start.
              </p>
            </div>

            <div className="grid gap-4 max-w-2xl mx-auto md:grid-cols-2">
              {/* Template Option */}
              <button
                onClick={() => setSetupMode("template")}
                className={`p-6 rounded-xl border-2 text-left transition-all duration-200 ${
                  setupMode === "template"
                    ? "border-primary bg-primary/5 ring-2 ring-primary/20"
                    : "border-border/50 bg-muted/20 hover:border-primary/30 hover:bg-muted/30"
                }`}
              >
                <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-primary/10 mb-4">
                  <svg className="h-6 w-6 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01" />
                  </svg>
                </div>
                <h3 className="font-bold text-lg mb-2">Start from Template</h3>
                <p className="text-sm text-muted-foreground">
                  Use a pre-configured program with exercises already set up. You can customize it after.
                </p>
                {setupMode === "template" && (
                  <div className="mt-3 flex items-center gap-2 text-primary text-sm font-medium">
                    <svg className="h-4 w-4" fill="currentColor" viewBox="0 0 20 20">
                      <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                    </svg>
                    Selected
                  </div>
                )}
              </button>

              {/* Build from Scratch Option */}
              <button
                onClick={() => setSetupMode("scratch")}
                className={`p-6 rounded-xl border-2 text-left transition-all duration-200 ${
                  setupMode === "scratch"
                    ? "border-primary bg-primary/5 ring-2 ring-primary/20"
                    : "border-border/50 bg-muted/20 hover:border-primary/30 hover:bg-muted/30"
                }`}
              >
                <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-muted/50 mb-4">
                  <svg className="h-6 w-6 text-muted-foreground" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 6v6m0 0v6m0-6h6m-6 0H6" />
                  </svg>
                </div>
                <h3 className="font-bold text-lg mb-2">Build from Scratch</h3>
                <p className="text-sm text-muted-foreground">
                  Create a custom program by selecting your own exercises and configuring each one.
                </p>
                {setupMode === "scratch" && (
                  <div className="mt-3 flex items-center gap-2 text-primary text-sm font-medium">
                    <svg className="h-4 w-4" fill="currentColor" viewBox="0 0 20 20">
                      <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                    </svg>
                    Selected
                  </div>
                )}
              </button>
            </div>

            {/* Show program settings if building from scratch */}
            {setupMode === "scratch" && (
              <div className="space-y-5 max-w-md mx-auto pt-6 border-t border-border/50">
                <div className="space-y-2">
                  <label className="block text-sm font-semibold text-foreground">Program Name</label>
                  <Input
                    type="text"
                    value={workoutName}
                    onChange={(e) => setWorkoutName(e.target.value)}
                    placeholder="My A2S Program"
                  />
                </div>

                <div className="space-y-2">
                  <label className="block text-sm font-semibold text-foreground">Program Variant</label>
                  <select
                    value={variant}
                    onChange={(e) => setVariant(Number(e.target.value) as ProgramVariant)}
                    className="flex h-11 w-full rounded-lg border border-border/50 bg-muted/30 px-4 py-2.5 text-base text-foreground transition-all duration-200 hover:border-border focus:border-primary focus:bg-background focus:outline-none focus:ring-2 focus:ring-primary/20 md:text-sm"
                  >
                    <option value={ProgramVariant.FourDay}>4-Day Program</option>
                    <option value={ProgramVariant.FiveDay}>5-Day Program</option>
                    <option value={ProgramVariant.SixDay}>6-Day Program</option>
                  </select>
                </div>

                <div className="space-y-2">
                  <label className="block text-sm font-semibold text-foreground">Total Weeks</label>
                  <Input
                    type="number"
                    value={totalWeeks}
                    onChange={(e) => setTotalWeeks(Number(e.target.value))}
                    min="1"
                    max="21"
                  />
                  <p className="text-xs text-muted-foreground mt-1.5">
                    Standard program is 21 weeks (3 blocks of 7 weeks)
                  </p>
                </div>
              </div>
            )}
          </div>
        );

      case "template":
        return (
          <div className="space-y-8">
            <div className="text-center">
              <div className="mb-4 flex justify-center">
                <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-primary/10 border border-primary/20">
                  <svg className="h-8 w-8 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01" />
                  </svg>
                </div>
              </div>
              <h2 className="text-3xl font-bold mb-3">Choose a Template</h2>
              <p className="text-muted-foreground max-w-md mx-auto">
                Select a pre-configured workout template. You can customize the exercises in the next step.
              </p>
            </div>

            <div className="space-y-4 max-w-2xl mx-auto">
              {workoutTemplates.map((template) => (
                <button
                  key={template.id}
                  onClick={() => handleSelectTemplate(template)}
                  className={`w-full p-6 rounded-xl border-2 text-left transition-all duration-200 ${
                    selectedTemplate?.id === template.id
                      ? "border-primary bg-primary/5 ring-2 ring-primary/20"
                      : "border-border/50 bg-muted/20 hover:border-primary/30 hover:bg-muted/30"
                  }`}
                >
                  <div className="flex items-start justify-between">
                    <div className="flex-1">
                      <h3 className="font-bold text-lg mb-2">{template.name}</h3>
                      <p className="text-sm text-muted-foreground mb-4">{template.description}</p>
                      <div className="flex flex-wrap gap-2">
                        <span className="inline-flex items-center px-2.5 py-1 rounded-md bg-muted/50 text-xs font-medium">
                          {template.variant}-Day Split
                        </span>
                        <span className="inline-flex items-center px-2.5 py-1 rounded-md bg-muted/50 text-xs font-medium">
                          {template.totalWeeks} Weeks
                        </span>
                        <span className="inline-flex items-center px-2.5 py-1 rounded-md bg-muted/50 text-xs font-medium">
                          {template.exercises.length} Exercises
                        </span>
                      </div>
                    </div>
                    {selectedTemplate?.id === template.id && (
                      <div className="flex-shrink-0 ml-4">
                        <div className="flex h-8 w-8 items-center justify-center rounded-full bg-primary text-white">
                          <svg className="h-5 w-5" fill="currentColor" viewBox="0 0 20 20">
                            <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                          </svg>
                        </div>
                      </div>
                    )}
                  </div>

                  {/* Show exercise preview */}
                  {selectedTemplate?.id === template.id && (
                    <div className="mt-4 pt-4 border-t border-border/50">
                      <p className="text-sm font-medium text-foreground mb-3">Exercise Preview:</p>
                      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                        {[1, 2, 3, 4].map((day) => {
                          const dayExercises = template.exercises.filter(e => e.assignedDay === day);
                          return (
                            <div key={day} className="text-xs">
                              <p className="font-semibold text-primary mb-1">Day {day}</p>
                              <ul className="space-y-0.5 text-muted-foreground">
                                {dayExercises.slice(0, 3).map((ex, i) => (
                                  <li key={i} className="truncate">{ex.templateName}</li>
                                ))}
                                {dayExercises.length > 3 && (
                                  <li className="text-muted-foreground/60">+{dayExercises.length - 3} more</li>
                                )}
                              </ul>
                            </div>
                          );
                        })}
                      </div>
                    </div>
                  )}
                </button>
              ))}
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
          <div className="space-y-8">
            <div className="text-center">
              <div className="mb-4 flex justify-center">
                <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-green-500/10 border border-green-500/20">
                  <svg className="h-8 w-8 text-green-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                </div>
              </div>
              <h2 className="text-3xl font-bold mb-3">Review Your Program</h2>
              <p className="text-muted-foreground">
                Please review your selections before creating your workout program.
              </p>
            </div>

            <div className="space-y-6">
              <div className="rounded-xl border border-border/50 bg-muted/20 p-5">
                <h3 className="font-bold text-lg mb-4 flex items-center gap-2">
                  <svg className="h-5 w-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
                  </svg>
                  Program Details
                </h3>
                <div className="grid gap-3 sm:grid-cols-3">
                  <div className="rounded-lg bg-background/50 p-3 border border-border/30">
                    <p className="text-xs text-muted-foreground uppercase tracking-wider">Name</p>
                    <p className="font-semibold mt-1">{workoutName}</p>
                  </div>
                  <div className="rounded-lg bg-background/50 p-3 border border-border/30">
                    <p className="text-xs text-muted-foreground uppercase tracking-wider">Variant</p>
                    <p className="font-semibold mt-1">{variant}-Day Program</p>
                  </div>
                  <div className="rounded-lg bg-background/50 p-3 border border-border/30">
                    <p className="text-xs text-muted-foreground uppercase tracking-wider">Duration</p>
                    <p className="font-semibold mt-1">{totalWeeks} weeks</p>
                  </div>
                </div>
              </div>

              <div className="rounded-xl border border-border/50 bg-muted/20 p-5">
                <h3 className="font-bold text-lg mb-4 flex items-center gap-2">
                  <svg className="h-5 w-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
                  </svg>
                  Selected Exercises
                </h3>
                {selectedExercises.length === 0 ? (
                  <div className="text-center py-8 rounded-lg border-2 border-dashed border-border/50 bg-background/30">
                    <p className="text-sm text-muted-foreground">No exercises selected yet</p>
                  </div>
                ) : (
                  <div className="space-y-4">
                    {Object.entries(
                      selectedExercises.reduce((acc, ex) => {
                        if (!acc[ex.assignedDay]) acc[ex.assignedDay] = [];
                        acc[ex.assignedDay].push(ex);
                        return acc;
                      }, {} as Record<number, SelectedExercise[]>)
                    )
                      .sort(([a], [b]) => Number(a) - Number(b))
                      .map(([day, exercises]) => (
                        <div key={day} className="rounded-lg bg-background/50 p-4 border border-border/30">
                          <h4 className="text-sm font-bold text-primary mb-3">Day {day}</h4>
                          <ul className="space-y-3">
                            {exercises
                              .sort((a, b) => a.orderInDay - b.orderInDay)
                              .map((ex) => (
                                <li key={ex.id} className="flex items-start gap-3 p-2 rounded-md hover:bg-muted/30 transition-colors">
                                  <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary/10 text-primary text-xs font-bold flex-shrink-0">
                                    {ex.orderInDay}
                                  </div>
                                  <div className="flex-1 min-w-0">
                                    <div className="font-semibold text-foreground">
                                      {ex.template.name}
                                    </div>
                                    <div className="text-xs text-muted-foreground mt-0.5">
                                      {ex.progressionType === 'Linear' ? 'A2S Linear' : 'A2S Reps Per Set'}
                                    </div>
                                    {ex.progressionType === 'Linear' && ex.trainingMax && (
                                      <div className="text-xs text-muted-foreground flex flex-wrap gap-x-2">
                                        <span>TM: {ex.trainingMax.value}{ex.trainingMax.unit === WeightUnit.Kilograms ? 'kg' : 'lbs'}</span>
                                        <span>{ex.isPrimary ? 'Primary' : 'Auxiliary'}</span>
                                        <span>{ex.baseSetsPerExercise} sets</span>
                                      </div>
                                    )}
                                    {ex.progressionType === 'RepsPerSet' && ex.repRange && (
                                      <div className="text-xs text-muted-foreground flex flex-wrap gap-x-2">
                                        <span>Reps: {ex.repRange.minimum}-{ex.repRange.target}-{ex.repRange.maximum}</span>
                                        <span>Sets: {ex.currentSets} → {ex.targetSets}</span>
                                        <span>Start: {ex.startingWeight}{ex.weightUnit === WeightUnit.Kilograms ? 'kg' : 'lbs'}</span>
                                      </div>
                                    )}
                                  </div>
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
        if (setupMode === "scratch") {
          return workoutName.trim().length > 0;
        }
        return setupMode !== null;
      case "template":
        return selectedTemplate !== null;
      case "exercises":
        return true; // Optional exercise selection
      case "confirm":
        return true;
      default:
        return false;
    }
  };

  const getVisibleSteps = () => {
    const baseSteps = [
      { id: "welcome", label: "Start", icon: "M13 10V3L4 14h7v7l9-11h-7z" },
    ];

    if (setupMode === "template") {
      baseSteps.push({ id: "template", label: "Template", icon: "M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01" });
    }

    baseSteps.push(
      { id: "exercises", label: "Exercises", icon: "M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" },
      { id: "confirm", label: "Confirm", icon: "M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" }
    );

    return baseSteps;
  };

  const steps = getVisibleSteps();
  const currentStepIndex = steps.findIndex(s => s.id === currentStep);

  return (
    <div className="min-h-screen bg-background py-8 px-4">
      <div className="max-w-4xl mx-auto">
        {/* Header */}
        <div className="text-center mb-8">
          <h1 className="text-2xl font-bold text-foreground">Setup Wizard</h1>
          <p className="text-muted-foreground mt-1">Configure your training program</p>
        </div>

        {/* Progress indicator */}
        <div className="mb-8">
          <div className="flex items-center justify-between max-w-md mx-auto">
            {steps.map((step, index) => (
              <div key={step.id} className="flex items-center">
                <div className="flex flex-col items-center">
                  <div
                    className={`flex h-12 w-12 items-center justify-center rounded-xl border-2 transition-all duration-300 ${
                      index <= currentStepIndex
                        ? "border-primary bg-primary/10 text-primary"
                        : "border-border/50 bg-muted/20 text-muted-foreground"
                    }`}
                  >
                    <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d={step.icon} />
                    </svg>
                  </div>
                  <span className={`mt-2 text-xs font-medium ${
                    index <= currentStepIndex ? "text-primary" : "text-muted-foreground"
                  }`}>
                    {step.label}
                  </span>
                </div>
                {index < steps.length - 1 && (
                  <div className={`h-0.5 w-16 sm:w-24 mx-2 transition-colors duration-300 ${
                    index < currentStepIndex ? "bg-primary" : "bg-border/50"
                  }`} />
                )}
              </div>
            ))}
          </div>
        </div>

        <Card className="p-8 border-border/50">
          {/* Step content */}
          <div className="min-h-[450px]">{renderStep()}</div>

          {/* Navigation buttons */}
          <div className="mt-10 flex justify-between items-center pt-6 border-t border-border/50">
            <Button
              variant="ghost"
              onClick={handleBack}
              disabled={currentStep === "welcome"}
              className="gap-2"
            >
              <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
              </svg>
              Back
            </Button>

            {currentStep === "confirm" ? (
              <Button
                variant="glow"
                onClick={handleCreateWorkout}
                disabled={!canProceed() || createWorkout.isPending}
                className="gap-2"
              >
                {createWorkout.isPending ? (
                  <>
                    <svg className="h-4 w-4 animate-spin" fill="none" viewBox="0 0 24 24">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                    </svg>
                    Creating...
                  </>
                ) : (
                  <>
                    Create Program
                    <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                    </svg>
                  </>
                )}
              </Button>
            ) : (
              <Button onClick={handleNext} disabled={!canProceed()} className="gap-2">
                Next
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                </svg>
              </Button>
            )}
          </div>
        </Card>
      </div>
    </div>
  );
}
