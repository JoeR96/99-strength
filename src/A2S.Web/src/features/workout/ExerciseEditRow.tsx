import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { ExerciseEditState } from "./deriveExerciseEditStates";

interface ExerciseEditRowProps {
  state: ExerciseEditState;
  isExpanded: boolean;
  onToggleExpanded: () => void;
  onRemoveRequested: () => void;
  updateState: (exerciseId: string, updates: Partial<ExerciseEditState>) => void;
}

/**
 * Single exercise row within EditExercisesModal: header, compact weight/TM
 * input, and expanded edit section (rep range/sets, swap forms). Pure move
 * from the modal's editStates.map() body — identical markup and behavior.
 */
export function ExerciseEditRow({
  state,
  isExpanded,
  onToggleExpanded,
  onRemoveRequested,
  updateState,
}: ExerciseEditRowProps) {
  const isLinear = state.progressionType === "Linear" && !state.wantSwap;
  const isRepsPerSet = state.progressionType === "RepsPerSet" && !state.wantSwap;
  const swapTarget = state.progressionType === "Linear" ? "Reps Per Set" : "Linear (Hypertrophy)";

  return (
    <div
      className={`p-4 border rounded-lg transition-colors ${
        state.hasChanged ? "bg-primary/5 border-primary/30" : "bg-card/50"
      }`}
    >
      {/* Exercise header - always visible */}
      <div className="flex justify-between items-start mb-3">
        <div className="flex-1">
          <div className="flex items-center gap-2">
            <h3 className="font-semibold text-lg">{state.name}</h3>
            {state.hasChanged && (
              <span className="text-xs px-2 py-0.5 rounded-full bg-primary/20 text-primary">
                Modified
              </span>
            )}
            {state.wantSwap && (
              <span className="text-xs px-2 py-0.5 rounded-full bg-primary/20 text-primary">
                Swapping
              </span>
            )}
          </div>
          <span className="text-sm text-muted-foreground">
            {state.wantSwap
              ? `→ ${swapTarget}`
              : state.progressionType === "Linear"
              ? "Linear (Hypertrophy)"
              : state.progressionType === "RepsPerSet"
              ? "Reps Per Set"
              : "Minimal Sets"}
          </span>
        </div>
        <div className="flex items-center gap-1">
          <button
            onClick={onRemoveRequested}
            className="p-1.5 hover:bg-destructive/10 rounded transition-colors text-muted-foreground hover:text-destructive"
            title="Remove exercise"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
            </svg>
          </button>
          <button
            onClick={onToggleExpanded}
            className="p-1.5 hover:bg-muted rounded transition-colors text-muted-foreground"
            title={isExpanded ? "Collapse" : "Expand to edit"}
          >
            <svg
              className={`w-4 h-4 transition-transform ${isExpanded ? "rotate-180" : ""}`}
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
            </svg>
          </button>
        </div>
      </div>

      {/* Compact weight/TM input - always visible when not swapping */}
      {!state.wantSwap && (
        <div className="flex items-center gap-3">
          <Label className="text-sm text-muted-foreground whitespace-nowrap">
            {isLinear ? "TM" : "Weight"}
          </Label>
          <Input
            type="number"
            step="any"
            min="0"
            value={state.newValue}
            onChange={(e) => {
              const val = e.target.value;
              updateState(state.exerciseId, {
                newValue: val === "" ? 0 : Number(val),
              });
            }}
            className="w-24 text-base font-medium"
            aria-label={`${isLinear ? "Training max" : "Weight"} for ${state.name}`}
          />
          <span className="text-sm text-muted-foreground">{state.unit}</span>
          {state.newValue !== state.originalValue && (
            <span className="text-xs text-muted-foreground">
              (was {state.originalValue})
            </span>
          )}
        </div>
      )}

      {/* Expanded section */}
      {isExpanded && (
        <div className="mt-4 pt-4 border-t border-border space-y-4">
          {/* RepsPerSet: rep range + unilateral */}
          {isRepsPerSet && (
            <>
              <div>
                <Label className="text-sm font-medium mb-1 block">Rep Range</Label>
                <div className="grid grid-cols-2 gap-2">
                  <div>
                    <label className="text-xs text-muted-foreground">Min</label>
                    <Input
                      type="number"
                      value={state.repRangeMin}
                      onChange={(e) =>
                        updateState(state.exerciseId, {
                          repRangeMin: Number(e.target.value),
                        })
                      }
                      min={1}
                      max={30}
                    />
                  </div>
                  <div>
                    <label className="text-xs text-muted-foreground">Max</label>
                    <Input
                      type="number"
                      value={state.repRangeMax}
                      onChange={(e) =>
                        updateState(state.exerciseId, {
                          repRangeMax: Number(e.target.value),
                        })
                      }
                      min={1}
                      max={30}
                    />
                  </div>
                </div>
              </div>

              <div>
                <Label className="text-sm font-medium mb-1 block">Sets</Label>
                <div className="grid grid-cols-2 gap-2">
                  <div>
                    <label className="text-xs text-muted-foreground">Starting</label>
                    <Input
                      type="number"
                      value={state.startingSets}
                      onChange={(e) =>
                        updateState(state.exerciseId, {
                          startingSets: Number(e.target.value),
                        })
                      }
                      min={1}
                      max={10}
                    />
                  </div>
                  <div>
                    <label className="text-xs text-muted-foreground">Current</label>
                    <Input
                      type="number"
                      value={state.currentSets}
                      onChange={(e) =>
                        updateState(state.exerciseId, {
                          currentSets: Number(e.target.value),
                        })
                      }
                      min={1}
                      max={10}
                    />
                  </div>
                  <div>
                    <label className="text-xs text-muted-foreground">Target</label>
                    <Input
                      type="number"
                      value={state.targetSets}
                      onChange={(e) =>
                        updateState(state.exerciseId, {
                          targetSets: Number(e.target.value),
                        })
                      }
                      min={1}
                      max={10}
                    />
                  </div>
                </div>
              </div>
            </>
          )}

          {/* Linear: show info */}
          {isLinear && (
            <div className="text-sm text-muted-foreground bg-muted/50 rounded p-3">
              <div>Sets: {state.linearSets}</div>
              <div>AMRAP: {state.linearAmrap ? "Yes" : "No"}</div>
            </div>
          )}

          {/* Swap to RPS form (when Linear exercise wants to swap) */}
          {state.wantSwap && state.progressionType === "Linear" && (
            <div className="space-y-3 p-3 bg-primary/10 rounded-lg border border-primary/30">
              <p className="text-sm text-primary font-medium">
                Configure Reps Per Set
              </p>
              <div>
                <Label className="text-sm">Starting Weight ({state.unit})</Label>
                <Input
                  type="number"
                  step="2.5"
                  value={state.swapWeight}
                  onChange={(e) =>
                    updateState(state.exerciseId, {
                      swapWeight: parseFloat(e.target.value) || 0,
                    })
                  }
                  className="mt-1"
                />
              </div>
              <div>
                <Label className="text-sm">Rep Range</Label>
                <div className="grid grid-cols-2 gap-2 mt-1">
                  <div>
                    <label className="text-xs text-muted-foreground">Min</label>
                    <Input
                      type="number"
                      value={state.swapRepMin}
                      onChange={(e) =>
                        updateState(state.exerciseId, { swapRepMin: Number(e.target.value) })
                      }
                    />
                  </div>
                  <div>
                    <label className="text-xs text-muted-foreground">Max</label>
                    <Input
                      type="number"
                      value={state.swapRepMax}
                      onChange={(e) =>
                        updateState(state.exerciseId, { swapRepMax: Number(e.target.value) })
                      }
                    />
                  </div>
                </div>
              </div>
              <div>
                <Label className="text-sm">Target Sets</Label>
                <Input
                  type="number"
                  value={state.swapTargetSets}
                  onChange={(e) =>
                    updateState(state.exerciseId, { swapTargetSets: Number(e.target.value) })
                  }
                  min={1}
                  max={10}
                  className="mt-1"
                />
              </div>
            </div>
          )}

          {/* Swap to Linear form (when RPS exercise wants to swap) */}
          {state.wantSwap && state.progressionType === "RepsPerSet" && (
            <div className="space-y-3 p-3 bg-primary/10 rounded-lg border border-primary/30">
              <p className="text-sm text-primary font-medium">
                Configure Linear (Hypertrophy)
              </p>
              <div>
                <Label className="text-sm">Training Max ({state.unit})</Label>
                <Input
                  type="number"
                  step="2.5"
                  value={state.swapTrainingMax}
                  onChange={(e) =>
                    updateState(state.exerciseId, {
                      swapTrainingMax: parseFloat(e.target.value) || 0,
                    })
                  }
                  className="mt-1"
                />
                <p className="text-xs text-muted-foreground mt-1">
                  ~90-95% of your 1RM
                </p>
              </div>
            </div>
          )}

          {/* Swap toggle button - only for Linear and RepsPerSet */}
          {(state.progressionType === "Linear" || state.progressionType === "RepsPerSet") && (
            <button
              type="button"
              onClick={() =>
                updateState(state.exerciseId, { wantSwap: !state.wantSwap })
              }
              className={`w-full px-3 py-2.5 text-sm font-medium rounded-lg border-2 transition-all flex items-center justify-center gap-2 ${
                state.wantSwap
                  ? "border-primary bg-primary/10 text-primary"
                  : "border-border hover:border-primary/50 hover:bg-muted/50 text-muted-foreground"
              }`}
            >
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
              </svg>
              {state.wantSwap
                ? `Cancel — keep ${state.progressionType === "Linear" ? "Linear (Hypertrophy)" : "Reps Per Set"}`
                : `Swap to ${swapTarget}`}
            </button>
          )}
        </div>
      )}
    </div>
  );
}
