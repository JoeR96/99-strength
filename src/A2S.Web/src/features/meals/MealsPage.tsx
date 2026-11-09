import { useMemo } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import { Navbar } from '@/components/layout/Navbar';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { useGenerateShoppingList, useMeals, useSetWeekMeals, useWeekMeals } from '@/hooks/useMeals';
import { MEAL_TYPES, type MealDto, type MealType } from '@/types/meals';
import { WeekPicker } from './WeekPicker';
import { resolveWeek } from './week';

/**
 * Pick this week's meals from the recipe set and generate the shopping list. Ticking a meal saves
 * the week's menu straight away; "Generate list" builds the list and opens it.
 */
export function MealsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const week = resolveWeek(searchParams.get('week'));

  const meals = useMeals();
  const weekMeals = useWeekMeals(week);
  const setWeekMeals = useSetWeekMeals(week);
  const generate = useGenerateShoppingList(week);

  const selected = useMemo(() => new Set(weekMeals.data?.mealIds ?? []), [weekMeals.data]);
  const grouped = useMemo(() => groupByType(meals.data ?? []), [meals.data]);

  const changeWeek = (next: string) => setSearchParams({ week: next });

  const toggleMeal = async (meal: MealDto) => {
    const next = new Set(selected);
    if (next.has(meal.id)) next.delete(meal.id);
    else next.add(meal.id);
    try {
      await setWeekMeals.mutateAsync([...next]);
    } catch {
      toast.error('Could not save this week’s meals');
    }
  };

  const generateList = async () => {
    try {
      const list = await generate.mutateAsync();
      toast.success(`Shopping list ready: ${list.totalCount} items`);
      navigate(`/shopping-list?week=${week}`);
    } catch {
      toast.error('Could not generate the shopping list');
    }
  };

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <main className="container-page py-8 space-y-6">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <h1 className="text-hero">Meals</h1>
            <p className="text-caption mt-2">Pick what you’re cooking this week, then generate one shopping list.</p>
          </div>
          <Button asChild variant="outline">
            <Link to={`/shopping-list?week=${week}`}>View shopping list</Link>
          </Button>
        </div>

        <WeekPicker week={week} onChange={changeWeek} />

        {meals.isLoading && <p className="text-caption text-center py-12">Loading meals...</p>}
        {meals.isError && (
          <Card className="p-6 text-center">
            <p className="text-destructive">Failed to load meals</p>
            <Button className="mt-4" onClick={() => meals.refetch()}>Retry</Button>
          </Card>
        )}

        {meals.data && (
          <>
            {MEAL_TYPES.filter((type) => grouped[type].length > 0).map((type) => (
              <section key={type} className="space-y-2">
                <h2 className="text-xl font-semibold text-foreground">{type}</h2>
                <ul className="space-y-2">
                  {grouped[type].map((meal) => (
                    <MealRow
                      key={meal.id}
                      meal={meal}
                      selected={selected.has(meal.id)}
                      disabled={setWeekMeals.isPending || weekMeals.isLoading}
                      onToggle={() => toggleMeal(meal)}
                    />
                  ))}
                </ul>
              </section>
            ))}

            <div className="sticky bottom-4 flex items-center justify-between gap-4 rounded-lg border border-border bg-card p-4">
              <span className="text-body">
                {selected.size === 0 ? 'No meals picked yet' : `${selected.size} meal${selected.size === 1 ? '' : 's'} this week`}
              </span>
              <Button size="lg" disabled={selected.size === 0 || generate.isPending} onClick={generateList}>
                {generate.isPending ? 'Generating...' : 'Generate list'}
              </Button>
            </div>
          </>
        )}
      </main>
    </div>
  );
}

interface MealRowProps {
  meal: MealDto;
  selected: boolean;
  disabled: boolean;
  onToggle: () => void;
}

function MealRow({ meal, selected, disabled, onToggle }: MealRowProps) {
  return (
    <li>
      <label
        className={cn(
          'flex min-h-14 cursor-pointer items-center gap-4 rounded-lg border bg-card px-4 py-3 transition-colors',
          selected ? 'border-primary' : 'border-border hover:border-foreground/40',
          disabled && 'cursor-wait opacity-70'
        )}
      >
        <input
          type="checkbox"
          className="h-6 w-6 shrink-0 accent-primary"
          checked={selected}
          disabled={disabled}
          onChange={onToggle}
          aria-label={`Add ${meal.title} to this week`}
        />
        <div className="min-w-0 flex-1">
          <div className="font-semibold text-foreground">{meal.title}</div>
          <div className="text-caption truncate">
            {meal.ingredients.length} ingredients
            {meal.tags.length > 0 && ` · ${meal.tags.join(', ')}`}
          </div>
        </div>
      </label>
    </li>
  );
}

function groupByType(meals: MealDto[]): Record<MealType, MealDto[]> {
  const groups: Record<MealType, MealDto[]> = { Breakfast: [], Lunch: [], Dinner: [], Snack: [] };
  for (const meal of meals) {
    (groups[meal.type] ?? groups.Snack).push(meal);
  }
  return groups;
}
