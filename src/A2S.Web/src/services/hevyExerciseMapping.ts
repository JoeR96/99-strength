/**
 * Hevy Exercise Mapping Service
 * Maps A2S exercise names to Hevy exercise template IDs
 */

import { hevyApi } from './hevyApi';
import { HEVY_EXERCISE_MAPPING } from '@/data/hevyExercises';
import type { HevyExerciseTemplate, HevyEquipmentCategory, HevyMuscleGroup } from '@/types/hevy';
import { EquipmentType } from '@/types/workout';

// Storage key for the exercise mapping
const MAPPING_STORAGE_KEY = 'hevy-exercise-mapping';
const HEVY_EXERCISES_CACHE_KEY = 'hevy-exercises-cache';
const CACHE_TTL_MS = 24 * 60 * 60 * 1000; // 24 hours

// Exercise mapping type
export interface ExerciseMapping {
  a2sName: string;
  hevyTemplateId: string;
  hevyTitle: string;
  lastUpdated: string;
}

// Cached Hevy exercises
interface CachedHevyExercises {
  exercises: HevyExerciseTemplate[];
  timestamp: number;
}

// Equipment type mapping from A2S to Hevy
const equipmentTypeToHevy: Record<number, HevyEquipmentCategory> = {
  [EquipmentType.Barbell]: 'barbell',
  [EquipmentType.Dumbbell]: 'dumbbell',
  [EquipmentType.Cable]: 'machine',
  [EquipmentType.Machine]: 'machine',
  [EquipmentType.Bodyweight]: 'none',
  [EquipmentType.SmithMachine]: 'machine',
};

// Exercise name to muscle group mapping
const exerciseToMuscleGroup: Record<string, HevyMuscleGroup> = {
  // Back exercises
  'lat pulldown': 'lats',
  'cable low row': 'lats',
  'assisted pullups': 'lats',
  'pullups': 'lats',
  'pull ups': 'lats',
  'rowing': 'lats',
  'row': 'lats',

  // Shoulder exercises
  'overhead press': 'shoulders',
  'ohp': 'shoulders',
  'lateral raise': 'shoulders',
  'rear delt': 'shoulders',
  'shoulder press': 'shoulders',
  'face pull': 'shoulders',

  // Chest exercises
  'bench press': 'chest',
  'chest flye': 'chest',
  'pec deck': 'chest',
  'dips': 'chest',
  'push up': 'chest',
  'pushup': 'chest',

  // Biceps
  'bicep curl': 'biceps',
  'curl': 'biceps',
  'ez curl': 'biceps',
  'concentration curl': 'biceps',
  'hammer curl': 'biceps',
  'preacher curl': 'biceps',

  // Triceps
  'tricep pushdown': 'triceps',
  'tricep extension': 'triceps',
  'skull crusher': 'triceps',
  'close grip': 'triceps',

  // Quadriceps
  'squat': 'quadriceps',
  'front squat': 'quadriceps',
  'leg extension': 'quadriceps',
  'leg press': 'quadriceps',
  'lunge': 'quadriceps',
  'hack squat': 'quadriceps',

  // Hamstrings
  'leg curl': 'hamstrings',
  'romanian deadlift': 'hamstrings',
  'rdl': 'hamstrings',
  'deadlift': 'hamstrings',
  'stiff leg': 'hamstrings',

  // Glutes
  'hip thrust': 'glutes',
  'glute bridge': 'glutes',
  'booty builder': 'glutes',
  'kickback': 'glutes',

  // Adductors/Abductors
  'hip adduction': 'adductors',
  'hip abduction': 'abductors',
  'inner thigh': 'adductors',
  'outer thigh': 'abductors',

  // Calves
  'calf raise': 'calves',
  'calf press': 'calves',

  // Abs
  'crunch': 'abdominals',
  'plank': 'abdominals',
  'sit up': 'abdominals',
  'leg raise': 'abdominals',
  'ab': 'abdominals',
};

class HevyExerciseMappingService {
  private mappings: Map<string, ExerciseMapping> = new Map();
  private cachedHevyExercises: HevyExerciseTemplate[] = [];
  private cacheTimestamp: number = 0;

  constructor() {
    this.loadMappings();
    this.loadCachedExercises();
  }

  /**
   * Load mappings from localStorage
   */
  private loadMappings(): void {
    try {
      const stored = localStorage.getItem(MAPPING_STORAGE_KEY);
      if (stored) {
        const mappings: ExerciseMapping[] = JSON.parse(stored);
        mappings.forEach(m => this.mappings.set(m.a2sName.toLowerCase(), m));
      }
    } catch {
      console.error('Failed to load exercise mappings');
    }
  }

  /**
   * Save mappings to localStorage
   */
  private saveMappings(): void {
    try {
      const mappings = Array.from(this.mappings.values());
      localStorage.setItem(MAPPING_STORAGE_KEY, JSON.stringify(mappings));
    } catch {
      console.error('Failed to save exercise mappings');
    }
  }

  /**
   * Load cached Hevy exercises from localStorage
   */
  private loadCachedExercises(): void {
    try {
      const stored = localStorage.getItem(HEVY_EXERCISES_CACHE_KEY);
      if (stored) {
        const cached: CachedHevyExercises = JSON.parse(stored);
        if (Date.now() - cached.timestamp < CACHE_TTL_MS) {
          this.cachedHevyExercises = cached.exercises;
          this.cacheTimestamp = cached.timestamp;
        }
      }
    } catch {
      console.error('Failed to load cached Hevy exercises');
    }
  }

  /**
   * Save cached Hevy exercises to localStorage
   */
  private saveCachedExercises(): void {
    try {
      const cached: CachedHevyExercises = {
        exercises: this.cachedHevyExercises,
        timestamp: this.cacheTimestamp,
      };
      localStorage.setItem(HEVY_EXERCISES_CACHE_KEY, JSON.stringify(cached));
    } catch {
      console.error('Failed to save cached Hevy exercises');
    }
  }

  /**
   * Fetch all Hevy exercises (with caching)
   */
  async fetchHevyExercises(forceRefresh = false): Promise<HevyExerciseTemplate[]> {
    if (!forceRefresh && this.cachedHevyExercises.length > 0 &&
        Date.now() - this.cacheTimestamp < CACHE_TTL_MS) {
      return this.cachedHevyExercises;
    }

    try {
      const exercises = await hevyApi.getAllExerciseTemplates();
      this.cachedHevyExercises = exercises;
      this.cacheTimestamp = Date.now();
      this.saveCachedExercises();
      return exercises;
    } catch (error) {
      console.error('Failed to fetch Hevy exercises:', error);
      return this.cachedHevyExercises;
    }
  }

  /**
   * Get the mapping for an A2S exercise name
   */
  getMapping(a2sExerciseName: string): ExerciseMapping | undefined {
    return this.mappings.get(a2sExerciseName.toLowerCase());
  }

  /**
   * Set a mapping for an A2S exercise
   */
  setMapping(a2sName: string, hevyTemplateId: string, hevyTitle: string): void {
    const mapping: ExerciseMapping = {
      a2sName,
      hevyTemplateId,
      hevyTitle,
      lastUpdated: new Date().toISOString(),
    };
    this.mappings.set(a2sName.toLowerCase(), mapping);
    this.saveMappings();
  }

  /**
   * Remove a mapping
   */
  removeMapping(a2sName: string): void {
    this.mappings.delete(a2sName.toLowerCase());
    this.saveMappings();
  }

  /**
   * Get all current mappings
   */
  getAllMappings(): ExerciseMapping[] {
    return Array.from(this.mappings.values());
  }

  /**
   * Find the best matching Hevy exercise for an A2S exercise name
   */
  findBestMatch(a2sExerciseName: string, hevyExercises: HevyExerciseTemplate[]): HevyExerciseTemplate | null {
    const normalizedName = a2sExerciseName.toLowerCase().trim();

    // First, try exact match
    const exactMatch = hevyExercises.find(
      ex => ex.title.toLowerCase().trim() === normalizedName
    );
    if (exactMatch) return exactMatch;

    // Try matching without common suffixes/prefixes
    const cleanName = normalizedName
      .replace(/\s*(machine|cable|barbell|dumbbell|smith|ez)\s*/gi, ' ')
      .replace(/\s+/g, ' ')
      .trim();

    const cleanMatch = hevyExercises.find(
      ex => ex.title.toLowerCase().replace(/\s*(machine|cable|barbell|dumbbell|smith|ez)\s*/gi, ' ').replace(/\s+/g, ' ').trim() === cleanName
    );
    if (cleanMatch) return cleanMatch;

    // Try partial match - check if any Hevy exercise contains the A2S name or vice versa
    const partialMatch = hevyExercises.find(
      ex => ex.title.toLowerCase().includes(normalizedName) ||
            normalizedName.includes(ex.title.toLowerCase())
    );
    if (partialMatch) return partialMatch;

    // Try matching key words
    const words = normalizedName.split(/\s+/).filter(w => w.length > 2);
    const wordMatch = hevyExercises.find(ex => {
      const hevyWords = ex.title.toLowerCase().split(/\s+/);
      return words.some(word => hevyWords.some(hw => hw.includes(word) || word.includes(hw)));
    });
    if (wordMatch) return wordMatch;

    return null;
  }

  /**
   * Get the muscle group for an exercise name
   */
  getMuscleGroup(exerciseName: string): HevyMuscleGroup {
    const lowerName = exerciseName.toLowerCase();

    for (const [keyword, muscleGroup] of Object.entries(exerciseToMuscleGroup)) {
      if (lowerName.includes(keyword)) {
        return muscleGroup;
      }
    }

    return 'other';
  }

  /**
   * Get Hevy equipment category from A2S equipment type
   */
  getEquipmentCategory(equipmentType: number): HevyEquipmentCategory {
    return equipmentTypeToHevy[equipmentType] || 'other';
  }

  /**
   * Auto-map all A2S exercises to Hevy exercises
   * Returns a summary of what was mapped
   */
  async autoMapExercises(a2sExerciseNames: string[]): Promise<{
    mapped: { a2sName: string; hevyName: string; hevyId: string }[];
    unmapped: string[];
  }> {
    const hevyExercises = await this.fetchHevyExercises();
    const mapped: { a2sName: string; hevyName: string; hevyId: string }[] = [];
    const unmapped: string[] = [];

    for (const a2sName of a2sExerciseNames) {
      // Skip if already mapped
      if (this.getMapping(a2sName)) {
        const existing = this.getMapping(a2sName)!;
        mapped.push({
          a2sName,
          hevyName: existing.hevyTitle,
          hevyId: existing.hevyTemplateId,
        });
        continue;
      }

      const match = this.findBestMatch(a2sName, hevyExercises);
      if (match) {
        this.setMapping(a2sName, match.id, match.title);
        mapped.push({
          a2sName,
          hevyName: match.title,
          hevyId: match.id,
        });
      } else {
        unmapped.push(a2sName);
      }
    }

    return { mapped, unmapped };
  }

  /**
   * Find a match in the pre-built Hevy exercise mapping
   */
  findInPrebuiltMapping(exerciseName: string): { id: string; title: string } | null {
    const normalizedName = exerciseName.toLowerCase().trim();

    // Try exact match first
    if (HEVY_EXERCISE_MAPPING[normalizedName]) {
      const ex = HEVY_EXERCISE_MAPPING[normalizedName];
      return { id: ex.id, title: ex.title };
    }

    // Try partial matches
    for (const [key, value] of Object.entries(HEVY_EXERCISE_MAPPING)) {
      // Check if the key contains the exercise name or vice versa
      if (key.includes(normalizedName) || normalizedName.includes(key)) {
        return { id: value.id, title: value.title };
      }
    }

    // Try matching by removing common words
    const cleanName = normalizedName
      .replace(/\s*(smith\s*machine|cable|barbell|dumbbell|machine|ez)\s*/gi, ' ')
      .replace(/\s+/g, ' ')
      .trim();

    for (const [key, value] of Object.entries(HEVY_EXERCISE_MAPPING)) {
      const cleanKey = key
        .replace(/\s*\([^)]+\)\s*/g, ' ') // Remove parentheses
        .replace(/\s+/g, ' ')
        .trim();
      if (cleanKey.includes(cleanName) || cleanName.includes(cleanKey)) {
        return { id: value.id, title: value.title };
      }
    }

    return null;
  }

  /**
   * Get Hevy template ID for an A2S exercise, creating it if necessary
   */
  async getOrCreateHevyTemplateId(
    a2sExerciseName: string,
    equipmentType: number
  ): Promise<string> {
    // Check existing mapping first
    const mapping = this.getMapping(a2sExerciseName);
    if (mapping) {
      return mapping.hevyTemplateId;
    }

    // Try the pre-built mapping (445 exercises from your Hevy account)
    const prebuiltMatch = this.findInPrebuiltMapping(a2sExerciseName);
    if (prebuiltMatch) {
      this.setMapping(a2sExerciseName, prebuiltMatch.id, prebuiltMatch.title);
      return prebuiltMatch.id;
    }

    // Try to find a match via API
    const hevyExercises = await this.fetchHevyExercises();
    const match = this.findBestMatch(a2sExerciseName, hevyExercises);

    if (match) {
      this.setMapping(a2sExerciseName, match.id, match.title);
      return match.id;
    }

    // Create new exercise in Hevy
    const muscleGroup = this.getMuscleGroup(a2sExerciseName);
    const equipmentCategory = this.getEquipmentCategory(equipmentType);

    const newExercise = await hevyApi.createExerciseTemplate({
      exercise: {
        title: a2sExerciseName,
        exercise_type: 'weight_reps',
        equipment_category: equipmentCategory,
        muscle_group: muscleGroup,
      },
    });

    // Update cache and mapping
    this.cachedHevyExercises.push(newExercise);
    this.saveCachedExercises();
    this.setMapping(a2sExerciseName, newExercise.id, newExercise.title);

    return newExercise.id;
  }

  /**
   * Clear all mappings and cache
   */
  clearAll(): void {
    this.mappings.clear();
    this.cachedHevyExercises = [];
    this.cacheTimestamp = 0;
    localStorage.removeItem(MAPPING_STORAGE_KEY);
    localStorage.removeItem(HEVY_EXERCISES_CACHE_KEY);
  }
}

// Export singleton instance
export const hevyExerciseMapping = new HevyExerciseMappingService();
