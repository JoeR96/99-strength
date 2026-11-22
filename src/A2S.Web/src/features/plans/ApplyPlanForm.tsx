/**
 * Applying a plan template: a start date (snapped to the Monday on or after it, shown), the template's
 * inputs (blank optional training maxes stay pending) and the start weight. When another plan is
 * active the server answers 409 and the form asks explicitly before resending with confirmReplace.
 * Used by the apply dialog on the Plan templates page and by Today's first-run card.
 */

import { useId, useState, type FormEvent } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { planTemplatesApi, replaceConfirmation, type ApplyPlanTemplateRequest, type ApplyPlanTemplateResult } from '@/api/planTemplates';
import type { SuggestedPlanPreview } from '@/api/suggestedPlans';
import { buildApplyRequest, formatLongDate, snapToMonday, START_WEIGHT_KEY } from './planTemplateForm';

export interface ApplyPlanFormProps {
  template: Pick<SuggestedPlanPreview, 'key' | 'name' | 'inputs' | 'startDate'>;
  onApplied?: (result: ApplyPlanTemplateResult) => void;
  onCancel?: () => void;
  /** Called with the snapped Monday when the date changes (the preview reloads its dates). */
  onStartDateChange?: (monday: string) => void;
  /** Stories: open on the replace confirmation for this active plan. */
  initialReplaceName?: string | null;
}

function serverMessage(error: unknown): string {
  const data = (error as { response?: { data?: { detail?: string; title?: string } } })?.response?.data;
  return data?.detail ?? data?.title ?? 'Could not start the plan. Check your connection and try again.';
}

export function ApplyPlanForm({ template, onApplied, onCancel, onStartDateChange, initialReplaceName = null }: ApplyPlanFormProps) {
  const id = useId();
  const queryClient = useQueryClient();
  const [date, setDate] = useState(template.startDate);
  const monday = snapToMonday(date) ?? template.startDate;
  const [values, setValues] = useState<Record<string, string>>(() =>
    Object.fromEntries(template.inputs.map((i) => [i.key, i.key === START_WEIGHT_KEY && i.default !== null ? String(i.default) : '']))
  );
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [replaceName, setReplaceName] = useState<string | null>(initialReplaceName);

  const apply = useMutation({
    mutationFn: (request: ApplyPlanTemplateRequest) => planTemplatesApi.apply(template.key, request),
    onSuccess: async (result) => {
      setReplaceName(null);
      await queryClient.invalidateQueries({ queryKey: ['plans'] });
      await queryClient.invalidateQueries({ queryKey: ['workouts'] });
      await queryClient.invalidateQueries({ queryKey: ['habits'] });
      onApplied?.(result);
    },
    onError: (error) => {
      const confirm = replaceConfirmation(error);
      if (confirm) setReplaceName(confirm.activePlanName);
    },
  });

  const send = (confirmReplace: boolean) => {
    const { request, errors: next } = buildApplyRequest(template.inputs, values, monday, confirmReplace);
    setErrors(next);
    if (request) apply.mutate(request);
  };

  const onSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    send(false);
  };

  const fieldId = (key: string) => `${id}-${key}`;
  const errorId = (key: string) => `${id}-${key}-error`;
  const showServerError = apply.isError && !replaceConfirmation(apply.error);

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4" data-testid="apply-plan-form">
      <div className="flex flex-col gap-2">
        <Label htmlFor={fieldId('start')}>Start date</Label>
        <Input
          id={fieldId('start')}
          type="date"
          value={date}
          onChange={(e) => {
            setDate(e.target.value);
            const snapped = snapToMonday(e.target.value);
            if (snapped) onStartDateChange?.(snapped);
          }}
        />
        <p className="text-sm text-muted-foreground" data-testid="snapped-start">
          Starts {formatLongDate(monday)}
          {monday !== date && ' (plans start on a Monday)'}
        </p>
      </div>

      <p className="text-sm text-muted-foreground">A TM is roughly 90 % of your one-rep max. Leave a TM blank to set it before that lift’s first session.</p>

      <div className="grid grid-cols-2 gap-4">
        {template.inputs.map((input) => (
          <div key={input.key} className="flex flex-col gap-2">
            <Label htmlFor={fieldId(input.key)}>
              {input.label}
              {input.optional && <span className="font-normal text-muted-foreground"> (optional)</span>}
            </Label>
            <Input
              id={fieldId(input.key)}
              name={input.key}
              inputMode="decimal"
              autoComplete="off"
              placeholder={input.unit}
              value={values[input.key] ?? ''}
              onChange={(e) => {
                const next = e.target.value;
                setValues((v) => ({ ...v, [input.key]: next }));
              }}
              aria-invalid={Boolean(errors[input.key])}
              aria-describedby={errors[input.key] ? errorId(input.key) : undefined}
            />
            {errors[input.key] && (
              <p id={errorId(input.key)} role="alert" className="text-sm text-destructive">
                {errors[input.key]}
              </p>
            )}
          </div>
        ))}
      </div>

      {replaceName && (
        <div className="rounded-md border border-warning/50 bg-warning/10 p-4" role="alertdialog" aria-labelledby={`${id}-replace`} data-testid="replace-confirm">
          <p id={`${id}-replace`} className="font-medium text-foreground">
            Replace {replaceName}? Your old plan and its history are kept.
          </p>
          <div className="mt-3 grid grid-cols-2 gap-2">
            <Button type="button" variant="outline" className="h-12" onClick={() => setReplaceName(null)}>
              Keep it
            </Button>
            <Button type="button" className="h-12" disabled={apply.isPending} onClick={() => send(true)} data-testid="replace-confirm-button">
              Replace
            </Button>
          </div>
        </div>
      )}

      {showServerError && (
        <p role="alert" className="text-sm text-destructive">
          {serverMessage(apply.error)}
        </p>
      )}

      {!replaceName && (
        <div className="flex flex-col gap-2">
          <Button type="submit" size="lg" className="w-full" disabled={apply.isPending}>
            {apply.isPending ? 'Starting…' : `Start ${template.name}`}
          </Button>
          {onCancel && (
            <Button type="button" variant="outline" className="h-12 w-full" onClick={onCancel}>
              Cancel
            </Button>
          )}
        </div>
      )}
    </form>
  );
}
