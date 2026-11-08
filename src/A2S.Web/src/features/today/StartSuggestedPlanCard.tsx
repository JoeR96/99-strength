import { useId, useState, type FormEvent } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  comingMonday,
  DEFAULT_START_WEIGHT_KG,
  formatStartDay,
  START_WEIGHT_MAX_KG,
  START_WEIGHT_MIN_KG,
  suggestedPlansApi,
  TRAINING_MAX_MAX_KG,
  TRAINING_MAX_MIN_KG,
  type StartSuggestedPlanResult,
} from '@/api/suggestedPlans';

type TmKey = 'benchTm' | 'squatTm' | 'ohpTm' | 'deadliftTm';

const TM_FIELDS: { key: TmKey; label: string }[] = [
  { key: 'benchTm', label: 'Bench press TM' },
  { key: 'squatTm', label: 'Squat TM' },
  { key: 'ohpTm', label: 'Overhead press TM' },
  { key: 'deadliftTm', label: 'Deadlift TM' },
];

export interface StartSuggestedPlanCardProps {
  /** Called after the plan is created (or found already running) so the page can show it. */
  onStarted?: (result: StartSuggestedPlanResult) => void;
  /** Today's date, for the "Starts Monday …" label. Defaults to now. */
  today?: Date;
  /** Starting values for the fields (e.g. from a previous workout). */
  initialValues?: Partial<Record<TmKey | 'startWeightKg', number>>;
}

interface FieldErrors {
  benchTm?: string;
  squatTm?: string;
  ohpTm?: string;
  deadliftTm?: string;
  startWeightKg?: string;
}

function parseKg(value: string): number | null {
  const trimmed = value.trim().replace(',', '.');
  if (trimmed === '') return null;
  const n = Number(trimmed);
  return Number.isFinite(n) ? n : null;
}

function validate(values: Record<TmKey | 'startWeightKg', string>): FieldErrors {
  const errors: FieldErrors = {};
  for (const { key } of TM_FIELDS) {
    const n = parseKg(values[key]);
    if (n === null) {
      errors[key] = 'Enter a weight in kg';
    } else if (n < TRAINING_MAX_MIN_KG || n > TRAINING_MAX_MAX_KG) {
      errors[key] = `Between ${TRAINING_MAX_MIN_KG} and ${TRAINING_MAX_MAX_KG} kg`;
    }
  }
  const weight = parseKg(values.startWeightKg);
  if (weight === null) {
    errors.startWeightKg = 'Enter your weight in kg';
  } else if (weight < START_WEIGHT_MIN_KG || weight > START_WEIGHT_MAX_KG) {
    errors.startWeightKg = `Between ${START_WEIGHT_MIN_KG} and ${START_WEIGHT_MAX_KG} kg`;
  }
  return errors;
}

function serverMessage(error: unknown): string {
  const data = (error as { response?: { data?: { detail?: string; title?: string } } })?.response?.data;
  return data?.detail ?? data?.title ?? 'Could not start the plan. Check your connection and try again.';
}

/**
 * First-run card on Today: "Start this plan — Cut to 85, Nov 2026". Four training maxes and a
 * start weight (prefilled 95 kg) are the only inputs; one tap creates the whole plan starting the
 * coming Monday. The card is self-contained (TanStack mutation inside); the page mounts it.
 */
export function StartSuggestedPlanCard({ onStarted, today, initialValues }: StartSuggestedPlanCardProps) {
  const id = useId();
  const queryClient = useQueryClient();
  const startDay = formatStartDay(comingMonday(today ?? new Date()));

  const [values, setValues] = useState<Record<TmKey | 'startWeightKg', string>>({
    benchTm: initialValues?.benchTm?.toString() ?? '',
    squatTm: initialValues?.squatTm?.toString() ?? '',
    ohpTm: initialValues?.ohpTm?.toString() ?? '',
    deadliftTm: initialValues?.deadliftTm?.toString() ?? '',
    startWeightKg: (initialValues?.startWeightKg ?? DEFAULT_START_WEIGHT_KG).toString(),
  });
  const [errors, setErrors] = useState<FieldErrors>({});

  const start = useMutation({
    mutationFn: suggestedPlansApi.startCutTo85,
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: ['plans'] });
      await queryClient.invalidateQueries({ queryKey: ['workouts'] });
      await queryClient.invalidateQueries({ queryKey: ['habits'] });
      onStarted?.(result);
    },
  });

  const setField = (key: TmKey | 'startWeightKg') => (event: React.ChangeEvent<HTMLInputElement>) => {
    const next = event.target.value;
    setValues((current) => ({ ...current, [key]: next }));
    if (errors[key]) {
      setErrors((current) => ({ ...current, [key]: undefined }));
    }
  };

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const nextErrors = validate(values);
    setErrors(nextErrors);
    if (Object.values(nextErrors).some(Boolean)) {
      return;
    }
    start.mutate({
      benchTm: parseKg(values.benchTm)!,
      squatTm: parseKg(values.squatTm)!,
      ohpTm: parseKg(values.ohpTm)!,
      deadliftTm: parseKg(values.deadliftTm)!,
      startWeightKg: parseKg(values.startWeightKg)!,
    });
  };

  const fieldId = (key: string) => `${id}-${key}`;
  const errorId = (key: string) => `${id}-${key}-error`;

  return (
    <Card data-testid="start-suggested-plan-card">
      <CardHeader>
        <CardTitle>Start this plan</CardTitle>
        <CardDescription>
          Cut to 85, Nov 2026. A2S2 on the four main lifts, cardio twice a week, Mobility A as every
          warm-up, five daily habits. Starts {startDay}.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-4">
          <p className="text-sm text-muted-foreground">
            Enter your current training maxes. A TM is roughly 90% of your one-rep max.
          </p>

          <div className="grid grid-cols-2 gap-4">
            {TM_FIELDS.map(({ key, label }) => (
              <div key={key} className="flex flex-col gap-2">
                <Label htmlFor={fieldId(key)}>{label}</Label>
                <div className="relative">
                  <Input
                    id={fieldId(key)}
                    name={key}
                    inputMode="decimal"
                    autoComplete="off"
                    placeholder="kg"
                    value={values[key]}
                    onChange={setField(key)}
                    aria-invalid={Boolean(errors[key])}
                    aria-describedby={errors[key] ? errorId(key) : undefined}
                    className="pr-10"
                  />
                  <span aria-hidden className="pointer-events-none absolute inset-y-0 right-4 flex items-center text-sm text-muted-foreground">
                    kg
                  </span>
                </div>
                {errors[key] && (
                  <p id={errorId(key)} role="alert" className="text-sm text-destructive">
                    {errors[key]}
                  </p>
                )}
              </div>
            ))}
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor={fieldId('startWeightKg')}>Start weight</Label>
            <div className="relative">
              <Input
                id={fieldId('startWeightKg')}
                name="startWeightKg"
                inputMode="decimal"
                autoComplete="off"
                value={values.startWeightKg}
                onChange={setField('startWeightKg')}
                aria-invalid={Boolean(errors.startWeightKg)}
                aria-describedby={errors.startWeightKg ? errorId('startWeightKg') : undefined}
                className="pr-10"
              />
              <span aria-hidden className="pointer-events-none absolute inset-y-0 right-4 flex items-center text-sm text-muted-foreground">
                kg
              </span>
            </div>
            {errors.startWeightKg && (
              <p id={errorId('startWeightKg')} role="alert" className="text-sm text-destructive">
                {errors.startWeightKg}
              </p>
            )}
          </div>

          {start.isError && (
            <p role="alert" className="text-sm text-destructive">
              {serverMessage(start.error)}
            </p>
          )}

          <Button type="submit" size="lg" className="w-full" disabled={start.isPending}>
            {start.isPending ? 'Starting…' : `Start this plan — starts ${startDay}`}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

export default StartSuggestedPlanCard;
