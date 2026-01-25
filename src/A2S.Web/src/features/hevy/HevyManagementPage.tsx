/**
 * Hevy Management Page
 * Manages Hevy API configuration and synced routines
 */

import { useState, useEffect } from 'react';
import { Navbar } from '@/components/layout/Navbar';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { HevySettings } from '@/components/hevy/HevySettings';
import { useHevy } from '@/contexts/HevyContext';
import { hevyApi } from '@/services/hevyApi';
import { apiClient } from '@/api/apiClient';
import type { HevyRoutine } from '@/types/hevy';
import type { WorkoutSummaryDto } from '@/types/workout';

interface RoutinesByProgram {
  [programName: string]: HevyRoutine[];
}

export function HevyManagementPage() {
  const { isConfigured, isValid } = useHevy();
  const [routines, setRoutines] = useState<HevyRoutine[]>([]);
  const [programs, setPrograms] = useState<WorkoutSummaryDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [deletingRoutine, setDeletingRoutine] = useState<string | null>(null);

  // Fetch routines and programs when API is configured and valid
  useEffect(() => {
    if (isConfigured && isValid) {
      fetchData();
    }
  }, [isConfigured, isValid]);

  const fetchData = async () => {
    setLoading(true);
    setError(null);
    try {
      const [routinesData, programsData] = await Promise.all([
        hevyApi.getAllRoutines(),
        apiClient.get<WorkoutSummaryDto[]>('/workouts').then(res => res.data),
      ]);
      setRoutines(routinesData);
      setPrograms(programsData);
    } catch (err) {
      console.error('Failed to fetch data:', err);
      setError(err instanceof Error ? err.message : 'Failed to fetch data');
    } finally {
      setLoading(false);
    }
  };

  const handleDeleteRoutine = async (routineId: string) => {
    if (!confirm('Are you sure you want to delete this routine from Hevy?')) {
      return;
    }

    setDeletingRoutine(routineId);
    try {
      await hevyApi.deleteRoutine(routineId);
      setRoutines(prev => prev.filter(r => r.id !== routineId));
    } catch (err) {
      console.error('Failed to delete routine:', err);
      setError(err instanceof Error ? err.message : 'Failed to delete routine');
    } finally {
      setDeletingRoutine(null);
    }
  };

  // Group routines by program name
  const routinesByProgram = routines.reduce<RoutinesByProgram>((acc, routine) => {
    // Extract program name from routine title (format: "Program Name - Week X Day Y")
    const match = routine.title.match(/^(.+?)\s*-\s*Week\s+\d+\s+Day\s+\d+$/i);
    const programName = match ? match[1].trim() : 'Other';

    if (!acc[programName]) {
      acc[programName] = [];
    }
    acc[programName].push(routine);
    return acc;
  }, {});

  // Get program names that exist in our system
  const knownProgramNames = new Set(programs.map(p => p.name));

  // Sort routines within each program by week/day
  Object.keys(routinesByProgram).forEach(programName => {
    routinesByProgram[programName].sort((a, b) => {
      const aMatch = a.title.match(/Week\s+(\d+)\s+Day\s+(\d+)/i);
      const bMatch = b.title.match(/Week\s+(\d+)\s+Day\s+(\d+)/i);
      if (aMatch && bMatch) {
        const aWeek = parseInt(aMatch[1]);
        const bWeek = parseInt(bMatch[1]);
        if (aWeek !== bWeek) return aWeek - bWeek;
        return parseInt(aMatch[2]) - parseInt(bMatch[2]);
      }
      return a.title.localeCompare(b.title);
    });
  });

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <div className="container mx-auto px-4 py-6 max-w-4xl">
        <div className="mb-6">
          <h1 className="text-2xl font-bold">Hevy Integration</h1>
          <p className="text-muted-foreground">
            Manage your Hevy API connection and synced routines
          </p>
        </div>

        <div className="space-y-6">
          {/* API Key Configuration */}
          <HevySettings />

          {/* Routine Browser */}
          {isConfigured && isValid && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center justify-between">
                  <span>Synced Routines</span>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={fetchData}
                    disabled={loading}
                  >
                    {loading ? (
                      <svg className="h-4 w-4 animate-spin" fill="none" viewBox="0 0 24 24">
                        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                      </svg>
                    ) : (
                      <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                      </svg>
                    )}
                    <span className="ml-2">Refresh</span>
                  </Button>
                </CardTitle>
                <CardDescription>
                  View and manage routines synced to your Hevy account
                </CardDescription>
              </CardHeader>
              <CardContent>
                {error && (
                  <div className="mb-4 p-3 rounded-lg bg-destructive/10 text-destructive text-sm">
                    {error}
                  </div>
                )}

                {loading && routines.length === 0 ? (
                  <div className="flex items-center justify-center py-8">
                    <svg className="h-6 w-6 animate-spin text-muted-foreground" fill="none" viewBox="0 0 24 24">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                    </svg>
                    <span className="ml-2 text-muted-foreground">Loading routines...</span>
                  </div>
                ) : routines.length === 0 ? (
                  <div className="text-center py-8 text-muted-foreground">
                    No routines found. Sync a workout to create routines in Hevy.
                  </div>
                ) : (
                  <div className="space-y-6">
                    {Object.entries(routinesByProgram).map(([programName, programRoutines]) => (
                      <div key={programName} className="space-y-3">
                        <div className="flex items-center gap-2">
                          <h3 className="font-semibold text-lg">{programName}</h3>
                          {knownProgramNames.has(programName) && (
                            <span className="text-xs px-2 py-0.5 rounded-full bg-primary/10 text-primary">
                              Active Program
                            </span>
                          )}
                          <span className="text-sm text-muted-foreground">
                            ({programRoutines.length} routine{programRoutines.length !== 1 ? 's' : ''})
                          </span>
                        </div>
                        <div className="space-y-2">
                          {programRoutines.map(routine => (
                            <div
                              key={routine.id}
                              className="flex items-center justify-between p-3 rounded-lg border bg-card hover:bg-muted/50 transition-colors"
                            >
                              <div className="flex-1">
                                <p className="font-medium">{routine.title}</p>
                                <p className="text-sm text-muted-foreground">
                                  {routine.exercises?.length || 0} exercises
                                  {routine.folder_id && ` • Folder: ${routine.folder_id}`}
                                </p>
                              </div>
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => handleDeleteRoutine(routine.id)}
                                disabled={deletingRoutine === routine.id}
                                className="text-destructive hover:text-destructive hover:bg-destructive/10"
                              >
                                {deletingRoutine === routine.id ? (
                                  <svg className="h-4 w-4 animate-spin" fill="none" viewBox="0 0 24 24">
                                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                                  </svg>
                                ) : (
                                  <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                  </svg>
                                )}
                              </Button>
                            </div>
                          ))}
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>
          )}

          {/* Not Configured State */}
          {!isConfigured && (
            <Card>
              <CardContent className="py-8">
                <div className="text-center text-muted-foreground">
                  <svg className="h-12 w-12 mx-auto mb-4 opacity-50" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1" />
                  </svg>
                  <p className="mb-2">Connect your Hevy account to view synced routines</p>
                  <p className="text-sm">Enter your API key above to get started</p>
                </div>
              </CardContent>
            </Card>
          )}
        </div>
      </div>
    </div>
  );
}
