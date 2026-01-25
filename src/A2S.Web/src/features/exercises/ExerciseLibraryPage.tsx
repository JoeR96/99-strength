import { useState, useMemo } from 'react';
import { Navbar } from '@/components/layout/Navbar';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { HEVY_EXERCISE_MAPPING } from '@/data/hevyExercises';

/**
 * Exercise data structure
 */
interface Exercise {
  id: string;
  title: string;
  muscle_group: string;
  equipment: string;
  is_custom: boolean;
}

/**
 * Muscle group display configuration
 */
const MUSCLE_GROUP_CONFIG: Record<string, { label: string; icon: string; color: string }> = {
  abdominals: { label: 'Abdominals', icon: '🎯', color: 'bg-orange-500/10 border-orange-500/30 text-orange-600 dark:text-orange-400' },
  adductors: { label: 'Adductors', icon: '🦵', color: 'bg-pink-500/10 border-pink-500/30 text-pink-600 dark:text-pink-400' },
  back: { label: 'Back', icon: '🔙', color: 'bg-blue-500/10 border-blue-500/30 text-blue-600 dark:text-blue-400' },
  biceps: { label: 'Biceps', icon: '💪', color: 'bg-red-500/10 border-red-500/30 text-red-600 dark:text-red-400' },
  calves: { label: 'Calves', icon: '🦶', color: 'bg-emerald-500/10 border-emerald-500/30 text-emerald-600 dark:text-emerald-400' },
  cardio: { label: 'Cardio', icon: '❤️', color: 'bg-rose-500/10 border-rose-500/30 text-rose-600 dark:text-rose-400' },
  chest: { label: 'Chest', icon: '🫁', color: 'bg-purple-500/10 border-purple-500/30 text-purple-600 dark:text-purple-400' },
  forearms: { label: 'Forearms', icon: '🤜', color: 'bg-amber-500/10 border-amber-500/30 text-amber-600 dark:text-amber-400' },
  full_body: { label: 'Full Body', icon: '🏋️', color: 'bg-indigo-500/10 border-indigo-500/30 text-indigo-600 dark:text-indigo-400' },
  glutes: { label: 'Glutes', icon: '🍑', color: 'bg-pink-500/10 border-pink-500/30 text-pink-600 dark:text-pink-400' },
  hamstrings: { label: 'Hamstrings', icon: '🦵', color: 'bg-cyan-500/10 border-cyan-500/30 text-cyan-600 dark:text-cyan-400' },
  lats: { label: 'Lats', icon: '🦅', color: 'bg-sky-500/10 border-sky-500/30 text-sky-600 dark:text-sky-400' },
  lower_back: { label: 'Lower Back', icon: '⬇️', color: 'bg-teal-500/10 border-teal-500/30 text-teal-600 dark:text-teal-400' },
  neck: { label: 'Neck', icon: '🦒', color: 'bg-lime-500/10 border-lime-500/30 text-lime-600 dark:text-lime-400' },
  obliques: { label: 'Obliques', icon: '↔️', color: 'bg-orange-500/10 border-orange-500/30 text-orange-600 dark:text-orange-400' },
  other: { label: 'Other', icon: '📦', color: 'bg-gray-500/10 border-gray-500/30 text-gray-600 dark:text-gray-400' },
  quadriceps: { label: 'Quadriceps', icon: '🦵', color: 'bg-green-500/10 border-green-500/30 text-green-600 dark:text-green-400' },
  shoulders: { label: 'Shoulders', icon: '🔝', color: 'bg-violet-500/10 border-violet-500/30 text-violet-600 dark:text-violet-400' },
  traps: { label: 'Traps', icon: '⬆️', color: 'bg-fuchsia-500/10 border-fuchsia-500/30 text-fuchsia-600 dark:text-fuchsia-400' },
  triceps: { label: 'Triceps', icon: '💪', color: 'bg-red-500/10 border-red-500/30 text-red-600 dark:text-red-400' },
};

/**
 * Equipment display configuration
 */
const EQUIPMENT_CONFIG: Record<string, { label: string; icon: string }> = {
  barbell: { label: 'Barbell', icon: '🏋️' },
  bodyweight: { label: 'Bodyweight', icon: '🤸' },
  cable: { label: 'Cable', icon: '🔗' },
  dumbbell: { label: 'Dumbbell', icon: '🏋️' },
  ez_bar: { label: 'EZ Bar', icon: '🔄' },
  kettlebell: { label: 'Kettlebell', icon: '🔔' },
  machine: { label: 'Machine', icon: '⚙️' },
  none: { label: 'No Equipment', icon: '👐' },
  other: { label: 'Other', icon: '📦' },
  plate: { label: 'Plate', icon: '⭕' },
  resistance_band: { label: 'Resistance Band', icon: '🎀' },
  smith_machine: { label: 'Smith Machine', icon: '🔩' },
  suspension: { label: 'Suspension', icon: '⛓️' },
  trap_bar: { label: 'Trap Bar', icon: '⬡' },
};

/**
 * Categorized muscle groups for better organization
 */
const MUSCLE_CATEGORIES = {
  'Upper Body - Push': ['chest', 'shoulders', 'triceps'],
  'Upper Body - Pull': ['back', 'lats', 'biceps', 'forearms', 'traps'],
  'Lower Body': ['quadriceps', 'hamstrings', 'glutes', 'calves', 'adductors'],
  'Core': ['abdominals', 'obliques', 'lower_back'],
  'Other': ['neck', 'cardio', 'full_body', 'other'],
};

export function ExerciseLibraryPage() {
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedMuscleGroups, setSelectedMuscleGroups] = useState<Set<string>>(new Set());
  const [selectedEquipment, setSelectedEquipment] = useState<Set<string>>(new Set());
  const [showCustomOnly, setShowCustomOnly] = useState(false);
  const [viewMode, setViewMode] = useState<'grid' | 'list' | 'grouped'>('grouped');

  // Convert HEVY_EXERCISE_MAPPING to array
  const allExercises = useMemo(() => {
    return Object.values(HEVY_EXERCISE_MAPPING) as Exercise[];
  }, []);

  // Get unique muscle groups and equipment
  const { muscleGroups, equipmentTypes } = useMemo(() => {
    const muscles = new Set<string>();
    const equipment = new Set<string>();

    allExercises.forEach(ex => {
      muscles.add(ex.muscle_group);
      equipment.add(ex.equipment);
    });

    return {
      muscleGroups: Array.from(muscles).sort(),
      equipmentTypes: Array.from(equipment).sort(),
    };
  }, [allExercises]);

  // Filter exercises
  const filteredExercises = useMemo(() => {
    return allExercises.filter(exercise => {
      // Search filter
      if (searchQuery) {
        const query = searchQuery.toLowerCase();
        if (!exercise.title.toLowerCase().includes(query)) {
          return false;
        }
      }

      // Muscle group filter
      if (selectedMuscleGroups.size > 0 && !selectedMuscleGroups.has(exercise.muscle_group)) {
        return false;
      }

      // Equipment filter
      if (selectedEquipment.size > 0 && !selectedEquipment.has(exercise.equipment)) {
        return false;
      }

      // Custom only filter
      if (showCustomOnly && !exercise.is_custom) {
        return false;
      }

      return true;
    });
  }, [allExercises, searchQuery, selectedMuscleGroups, selectedEquipment, showCustomOnly]);

  // Group exercises by muscle group
  const groupedExercises = useMemo(() => {
    const groups: Record<string, Exercise[]> = {};

    filteredExercises.forEach(exercise => {
      const group = exercise.muscle_group;
      if (!groups[group]) {
        groups[group] = [];
      }
      groups[group].push(exercise);
    });

    // Sort exercises within each group
    Object.keys(groups).forEach(key => {
      groups[key].sort((a, b) => a.title.localeCompare(b.title));
    });

    return groups;
  }, [filteredExercises]);

  // Toggle muscle group filter
  const toggleMuscleGroup = (group: string) => {
    setSelectedMuscleGroups(prev => {
      const next = new Set(prev);
      if (next.has(group)) {
        next.delete(group);
      } else {
        next.add(group);
      }
      return next;
    });
  };

  // Toggle equipment filter
  const toggleEquipment = (equipment: string) => {
    setSelectedEquipment(prev => {
      const next = new Set(prev);
      if (next.has(equipment)) {
        next.delete(equipment);
      } else {
        next.add(equipment);
      }
      return next;
    });
  };

  // Clear all filters
  const clearFilters = () => {
    setSearchQuery('');
    setSelectedMuscleGroups(new Set());
    setSelectedEquipment(new Set());
    setShowCustomOnly(false);
  };

  const hasActiveFilters = searchQuery || selectedMuscleGroups.size > 0 || selectedEquipment.size > 0 || showCustomOnly;

  // Stats
  const customCount = allExercises.filter(e => e.is_custom).length;
  const standardCount = allExercises.length - customCount;

  return (
    <div className="min-h-screen bg-background theme-transition">
      <Navbar />

      <main className="container-apple py-8">
        {/* Header */}
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-foreground mb-2">Exercise Library</h1>
          <p className="text-muted-foreground">
            Browse {allExercises.length} exercises ({standardCount} standard, {customCount} custom)
          </p>
        </div>

        {/* Search and View Toggle */}
        <div className="flex flex-col sm:flex-row gap-4 mb-6">
          <div className="flex-1">
            <Input
              type="text"
              placeholder="Search exercises..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full"
            />
          </div>
          <div className="flex gap-2">
            <button
              onClick={() => setViewMode('grouped')}
              className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                viewMode === 'grouped'
                  ? 'bg-primary text-primary-foreground'
                  : 'bg-muted text-muted-foreground hover:text-foreground'
              }`}
            >
              Grouped
            </button>
            <button
              onClick={() => setViewMode('grid')}
              className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                viewMode === 'grid'
                  ? 'bg-primary text-primary-foreground'
                  : 'bg-muted text-muted-foreground hover:text-foreground'
              }`}
            >
              Grid
            </button>
            <button
              onClick={() => setViewMode('list')}
              className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                viewMode === 'list'
                  ? 'bg-primary text-primary-foreground'
                  : 'bg-muted text-muted-foreground hover:text-foreground'
              }`}
            >
              List
            </button>
          </div>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
          {/* Filters Sidebar */}
          <div className="lg:col-span-1 space-y-6">
            {/* Filter Header */}
            <div className="flex items-center justify-between">
              <h2 className="font-semibold text-foreground">Filters</h2>
              {hasActiveFilters && (
                <button
                  onClick={clearFilters}
                  className="text-sm text-primary hover:text-primary/80 transition-colors"
                >
                  Clear all
                </button>
              )}
            </div>

            {/* Custom Exercises Toggle */}
            <div>
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={showCustomOnly}
                  onChange={(e) => setShowCustomOnly(e.target.checked)}
                  className="rounded border-border"
                />
                <span className="text-sm text-foreground">Custom exercises only</span>
              </label>
            </div>

            {/* Muscle Groups Filter */}
            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="text-sm">Muscle Groups</CardTitle>
                {selectedMuscleGroups.size > 0 && (
                  <CardDescription className="text-xs">
                    {selectedMuscleGroups.size} selected
                  </CardDescription>
                )}
              </CardHeader>
              <CardContent className="space-y-4">
                {Object.entries(MUSCLE_CATEGORIES).map(([category, muscles]) => {
                  const availableMuscles = muscles.filter(m => muscleGroups.includes(m));
                  if (availableMuscles.length === 0) return null;

                  return (
                    <div key={category}>
                      <h4 className="text-xs font-medium text-muted-foreground mb-2">{category}</h4>
                      <div className="flex flex-wrap gap-1.5">
                        {availableMuscles.map(muscle => {
                          const config = MUSCLE_GROUP_CONFIG[muscle] || { label: muscle, icon: '📦', color: 'bg-gray-500/10' };
                          const isSelected = selectedMuscleGroups.has(muscle);
                          const count = allExercises.filter(e => e.muscle_group === muscle).length;

                          return (
                            <button
                              key={muscle}
                              onClick={() => toggleMuscleGroup(muscle)}
                              className={`px-2 py-1 rounded text-xs font-medium transition-all ${
                                isSelected
                                  ? 'bg-primary text-primary-foreground'
                                  : `${config.color} border hover:opacity-80`
                              }`}
                            >
                              {config.icon} {config.label} ({count})
                            </button>
                          );
                        })}
                      </div>
                    </div>
                  );
                })}
              </CardContent>
            </Card>

            {/* Equipment Filter */}
            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="text-sm">Equipment</CardTitle>
                {selectedEquipment.size > 0 && (
                  <CardDescription className="text-xs">
                    {selectedEquipment.size} selected
                  </CardDescription>
                )}
              </CardHeader>
              <CardContent>
                <div className="flex flex-wrap gap-1.5">
                  {equipmentTypes.map(equipment => {
                    const config = EQUIPMENT_CONFIG[equipment] || { label: equipment, icon: '📦' };
                    const isSelected = selectedEquipment.has(equipment);
                    const count = allExercises.filter(e => e.equipment === equipment).length;

                    return (
                      <button
                        key={equipment}
                        onClick={() => toggleEquipment(equipment)}
                        className={`px-2 py-1 rounded text-xs font-medium transition-all ${
                          isSelected
                            ? 'bg-primary text-primary-foreground'
                            : 'bg-muted text-muted-foreground hover:text-foreground hover:bg-muted/80'
                        }`}
                      >
                        {config.icon} {config.label} ({count})
                      </button>
                    );
                  })}
                </div>
              </CardContent>
            </Card>
          </div>

          {/* Exercise List */}
          <div className="lg:col-span-3">
            {/* Results Count */}
            <div className="mb-4 text-sm text-muted-foreground">
              Showing {filteredExercises.length} of {allExercises.length} exercises
            </div>

            {viewMode === 'grouped' && (
              <div className="space-y-6">
                {Object.entries(MUSCLE_CATEGORIES).map(([category, muscles]) => {
                  const categoryExercises = muscles.flatMap(m => groupedExercises[m] || []);
                  if (categoryExercises.length === 0) return null;

                  return (
                    <div key={category}>
                      <h2 className="text-lg font-semibold text-foreground mb-4 pb-2 border-b border-border">
                        {category}
                        <span className="ml-2 text-sm font-normal text-muted-foreground">
                          ({categoryExercises.length} exercises)
                        </span>
                      </h2>

                      {muscles.map(muscle => {
                        const exercises = groupedExercises[muscle];
                        if (!exercises || exercises.length === 0) return null;
                        const config = MUSCLE_GROUP_CONFIG[muscle] || { label: muscle, icon: '📦', color: '' };

                        return (
                          <div key={muscle} className="mb-6">
                            <h3 className="text-sm font-medium text-muted-foreground mb-3 flex items-center gap-2">
                              <span>{config.icon}</span>
                              <span>{config.label}</span>
                              <span className="text-xs">({exercises.length})</span>
                            </h3>
                            <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-2">
                              {exercises.map(exercise => (
                                <ExerciseCard key={exercise.id} exercise={exercise} />
                              ))}
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  );
                })}
              </div>
            )}

            {viewMode === 'grid' && (
              <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-3">
                {filteredExercises
                  .sort((a, b) => a.title.localeCompare(b.title))
                  .map(exercise => (
                    <ExerciseCard key={exercise.id} exercise={exercise} />
                  ))}
              </div>
            )}

            {viewMode === 'list' && (
              <div className="space-y-1">
                {filteredExercises
                  .sort((a, b) => a.title.localeCompare(b.title))
                  .map(exercise => (
                    <ExerciseListItem key={exercise.id} exercise={exercise} />
                  ))}
              </div>
            )}

            {filteredExercises.length === 0 && (
              <div className="text-center py-12">
                <p className="text-muted-foreground">No exercises found matching your criteria</p>
                <button
                  onClick={clearFilters}
                  className="mt-2 text-primary hover:text-primary/80 text-sm"
                >
                  Clear filters
                </button>
              </div>
            )}
          </div>
        </div>
      </main>
    </div>
  );
}

/**
 * Exercise Card Component
 */
function ExerciseCard({ exercise }: { exercise: Exercise }) {
  const muscleConfig = MUSCLE_GROUP_CONFIG[exercise.muscle_group] || { label: exercise.muscle_group, icon: '📦', color: 'bg-gray-500/10' };
  const equipmentConfig = EQUIPMENT_CONFIG[exercise.equipment] || { label: exercise.equipment, icon: '📦' };

  return (
    <div className={`p-3 rounded-lg border ${exercise.is_custom ? 'border-primary/30 bg-primary/5' : 'border-border bg-card'} hover:border-primary/50 transition-colors`}>
      <div className="flex items-start justify-between gap-2">
        <h3 className="font-medium text-sm text-foreground leading-tight">{exercise.title}</h3>
        {exercise.is_custom && (
          <span className="text-[10px] px-1.5 py-0.5 rounded bg-primary/20 text-primary font-medium shrink-0">
            Custom
          </span>
        )}
      </div>
      <div className="mt-2 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
        <span className={`px-1.5 py-0.5 rounded ${muscleConfig.color}`}>
          {muscleConfig.icon} {muscleConfig.label}
        </span>
        <span className="px-1.5 py-0.5 rounded bg-muted">
          {equipmentConfig.icon} {equipmentConfig.label}
        </span>
      </div>
    </div>
  );
}

/**
 * Exercise List Item Component
 */
function ExerciseListItem({ exercise }: { exercise: Exercise }) {
  const muscleConfig = MUSCLE_GROUP_CONFIG[exercise.muscle_group] || { label: exercise.muscle_group, icon: '📦', color: 'bg-gray-500/10' };
  const equipmentConfig = EQUIPMENT_CONFIG[exercise.equipment] || { label: exercise.equipment, icon: '📦' };

  return (
    <div className={`px-3 py-2 rounded border ${exercise.is_custom ? 'border-primary/30 bg-primary/5' : 'border-border bg-card'} hover:border-primary/50 transition-colors flex items-center gap-4`}>
      <div className="flex-1 min-w-0">
        <h3 className="font-medium text-sm text-foreground truncate">{exercise.title}</h3>
      </div>
      <div className="flex items-center gap-2 shrink-0">
        <span className={`px-1.5 py-0.5 rounded text-xs ${muscleConfig.color}`}>
          {muscleConfig.label}
        </span>
        <span className="px-1.5 py-0.5 rounded bg-muted text-xs text-muted-foreground">
          {equipmentConfig.label}
        </span>
        {exercise.is_custom && (
          <span className="text-[10px] px-1.5 py-0.5 rounded bg-primary/20 text-primary font-medium">
            Custom
          </span>
        )}
      </div>
    </div>
  );
}
