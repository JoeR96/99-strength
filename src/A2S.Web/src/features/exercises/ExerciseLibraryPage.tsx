import { useState, useMemo } from 'react';
import { Navbar } from '@/components/layout/Navbar';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { HEVY_EXERCISE_MAPPING } from '@/data/hevyExercises';
import {
  type Exercise,
  MUSCLE_GROUP_CONFIG,
  EQUIPMENT_CONFIG,
  MUSCLE_CATEGORIES,
  ExerciseCard,
  ExerciseListItem,
  ExerciseHistoryModal,
} from './ExerciseLibraryComponents';

export function ExerciseLibraryPage() {
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedMuscleGroups, setSelectedMuscleGroups] = useState<Set<string>>(new Set());
  const [selectedEquipment, setSelectedEquipment] = useState<Set<string>>(new Set());
  const [showCustomOnly, setShowCustomOnly] = useState(false);
  const [viewMode, setViewMode] = useState<'grid' | 'list' | 'grouped'>('grouped');
  const [selectedExerciseForHistory, setSelectedExerciseForHistory] = useState<Exercise | null>(null);

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
                                <ExerciseCard
                                  key={exercise.id}
                                  exercise={exercise}
                                  onClick={() => setSelectedExerciseForHistory(exercise)}
                                />
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
                    <ExerciseCard
                      key={exercise.id}
                      exercise={exercise}
                      onClick={() => setSelectedExerciseForHistory(exercise)}
                    />
                  ))}
              </div>
            )}

            {viewMode === 'list' && (
              <div className="space-y-1">
                {filteredExercises
                  .sort((a, b) => a.title.localeCompare(b.title))
                  .map(exercise => (
                    <ExerciseListItem
                      key={exercise.id}
                      exercise={exercise}
                      onClick={() => setSelectedExerciseForHistory(exercise)}
                    />
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

      {/* Exercise History Modal */}
      {selectedExerciseForHistory && (
        <ExerciseHistoryModal
          exercise={selectedExerciseForHistory}
          onClose={() => setSelectedExerciseForHistory(null)}
        />
      )}
    </div>
  );
}
