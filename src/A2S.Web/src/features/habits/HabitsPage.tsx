import { Navbar } from '@/components/layout/Navbar';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

/**
 * Habits tab. The habit tracker (water, walks, weigh-in, mobility, creatine with streaks)
 * ships in the next release; this page keeps the tab honest until then.
 */
export function HabitsPage() {
  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <main className="container-page py-6 space-y-4">
        <h1 className="text-2xl font-bold text-foreground">Habits</h1>
        <Card>
          <CardHeader>
            <CardTitle>Coming next</CardTitle>
            <CardDescription>
              Daily counters for water and walks, your morning weigh-in, mobility and creatine ticks,
              and a streak that forgives one missed day.
            </CardDescription>
          </CardHeader>
          <CardContent className="text-sm text-muted-foreground">
            Your existing Terran CC habits and history will be imported so the streak starts honest.
          </CardContent>
        </Card>
      </main>
    </div>
  );
}
