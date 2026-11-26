/**
 * Plan templates (`/plans/templates`, `/plans/templates/:key`): every ready-made plan, a full preview
 * of the chosen one (dates follow the start date picked in the apply dialog) and Apply.
 */

import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { ChevronRight } from 'lucide-react';
import { Navbar } from '@/components/layout/Navbar';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { planTemplatesApi, type PlanTemplatePreview } from '@/api/planTemplates';
import { ApplyPlanForm } from './ApplyPlanForm';
import { PlanTemplatePreviewView } from './PlanTemplatePreviewView';
import { formatShortDate } from './planTemplateForm';

export const planTemplateKeys = {
  list: () => ['plans', 'templates'] as const,
  one: (key: string, startDate: string | null) => ['plans', 'templates', key, startDate] as const,
};

const SHEET = 'sheet-surface bottom-0 top-auto w-full translate-y-0 overflow-y-auto rounded-b-none rounded-t-2xl p-6 pb-[calc(1.5rem+env(safe-area-inset-bottom))] sm:bottom-auto sm:top-1/2 sm:w-[calc(100%-2rem)] sm:-translate-y-1/2 sm:rounded-2xl sm:pb-6 max-h-[92dvh]';

export function ApplyPlanDialog({
  open,
  template,
  onClose,
  onStartDateChange,
  initialReplaceName,
}: {
  open: boolean;
  template: PlanTemplatePreview;
  onClose: () => void;
  onStartDateChange?: (monday: string) => void;
  initialReplaceName?: string | null;
}) {
  const navigate = useNavigate();
  return (
    <Dialog open={open} onOpenChange={(isOpen) => !isOpen && onClose()}>
      <DialogContent className={SHEET}>
        <DialogHeader>
          <DialogTitle>Start {template.name}</DialogTitle>
          <DialogDescription>Pick the start date and fill in what you know. Everything else can wait.</DialogDescription>
        </DialogHeader>
        <ApplyPlanForm
          template={template}
          onCancel={onClose}
          onStartDateChange={onStartDateChange}
          initialReplaceName={initialReplaceName}
          onApplied={(result) => {
            toast.success(result.alreadyStarted ? 'This plan is already running' : 'Plan started');
            onClose();
            navigate('/today');
          }}
        />
      </DialogContent>
    </Dialog>
  );
}

function TemplateList({ templates }: { templates: PlanTemplatePreview[] }) {
  return (
    <ul className="space-y-3" data-testid="plan-template-list">
      {templates.map((t) => (
        <li key={t.key}>
          <Link
            to={`/plans/templates/${t.key}`}
            className="flex min-h-14 items-center gap-3 rounded-lg border border-border bg-card p-4 transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <span className="min-w-0 flex-1">
              <span className="block font-semibold text-foreground">{t.name}</span>
              <span className="block text-sm text-muted-foreground">{t.description}</span>
              <span className="mt-1 block text-xs text-muted-foreground">
                {t.programWeeks} weeks of A2S2 · {t.sessions.length || t.liftingDays.length || 'Several'} lifting days · starts {formatShortDate(t.startDate)}
              </span>
            </span>
            <ChevronRight className="h-5 w-5 shrink-0 text-muted-foreground" aria-hidden="true" />
          </Link>
        </li>
      ))}
    </ul>
  );
}

export interface PlanTemplatesPageProps {
  /** Stories: open with the apply dialog (and optionally its replace confirmation) showing. */
  initialApplyOpen?: boolean;
  initialReplaceName?: string | null;
}

export function PlanTemplatesPage({ initialApplyOpen = false, initialReplaceName = null }: PlanTemplatesPageProps) {
  const { key } = useParams<{ key: string }>();
  const [startDate, setStartDate] = useState<string | null>(null);
  const [applyOpen, setApplyOpen] = useState(initialApplyOpen);

  const list = useQuery({ queryKey: planTemplateKeys.list(), queryFn: () => planTemplatesApi.list(), enabled: !key });
  const one = useQuery({
    queryKey: planTemplateKeys.one(key ?? '', startDate),
    queryFn: () => planTemplatesApi.get(key!, startDate ?? undefined),
    enabled: Boolean(key),
    placeholderData: (previous) => previous,
  });

  const template = one.data ?? null;

  return (
    <div className="min-h-dvh bg-background">
      <Navbar />
      <main className="container-page space-y-6 py-8">
        {!key ? (
          <>
            <header>
              <h1 className="text-hero">Plan templates</h1>
              <p className="text-muted-foreground">Ready-made plans. Open one to see everything it sets up before you start it.</p>
            </header>
            {list.isLoading && <p className="text-muted-foreground">Loading plan templates…</p>}
            {list.isError && (
              <p role="alert" className="text-destructive">
                Couldn't load the plan templates.
              </p>
            )}
            {list.data && <TemplateList templates={list.data} />}
          </>
        ) : (
          <>
            <Link to="/plans/templates" className="inline-flex min-h-11 items-center text-sm text-primary underline-offset-4 hover:underline">
              All plan templates
            </Link>
            {one.isLoading && <p className="text-muted-foreground">Loading the plan…</p>}
            {one.isError && !template && (
              <p role="alert" className="text-destructive">
                Couldn't load this plan template.
              </p>
            )}
            {template && (
              <>
                <header>
                  <h1 className="text-hero">{template.name}</h1>
                </header>
                <PlanTemplatePreviewView template={template} />
                <Button size="lg" className="w-full" onClick={() => setApplyOpen(true)} data-testid="apply-template">
                  {template.alreadyStarted ? 'Already running' : 'Start this plan'}
                </Button>
                <ApplyPlanDialog
                  open={applyOpen}
                  template={template}
                  onClose={() => setApplyOpen(false)}
                  onStartDateChange={setStartDate}
                  initialReplaceName={initialReplaceName}
                />
              </>
            )}
          </>
        )}
      </main>
    </div>
  );
}
