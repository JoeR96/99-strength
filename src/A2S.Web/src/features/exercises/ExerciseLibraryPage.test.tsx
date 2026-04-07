import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ExerciseLibraryPage } from './ExerciseLibraryPage';

vi.mock('@/components/layout/Navbar', () => ({
  Navbar: () => <nav data-testid="navbar">Navbar</nav>,
}));

vi.mock('./ExerciseLibraryComponents', () => ({
  MUSCLE_GROUP_CONFIG: {
    chest: { label: 'Chest', icon: '💪', color: 'bg-red-500/10' },
    quadriceps: { label: 'Quadriceps', icon: '🦵', color: 'bg-blue-500/10' },
    lats: { label: 'Lats', icon: '🔙', color: 'bg-green-500/10' },
  },
  EQUIPMENT_CONFIG: {
    barbell: { label: 'Barbell', icon: '🏋️' },
    machine: { label: 'Machine', icon: '⚙️' },
  },
  MUSCLE_CATEGORIES: {
    'Upper Body': ['chest', 'lats'],
    'Lower Body': ['quadriceps'],
  },
  ExerciseCard: ({ exercise, onClick }: { exercise: { title: string }; onClick: () => void }) => (
    <div data-testid="exercise-card" onClick={onClick}>{exercise.title}</div>
  ),
  ExerciseListItem: ({ exercise, onClick }: { exercise: { title: string }; onClick: () => void }) => (
    <div data-testid="exercise-list-item" onClick={onClick}>{exercise.title}</div>
  ),
  ExerciseHistoryModal: () => null,
}));

vi.mock('@/data/hevyExercises', () => ({
  HEVY_EXERCISE_MAPPING: {
    'bench-press': {
      id: '1',
      title: 'Bench Press',
      muscle_group: 'chest',
      equipment: 'barbell',
      is_custom: false,
    },
    'squat': {
      id: '2',
      title: 'Squat',
      muscle_group: 'quadriceps',
      equipment: 'barbell',
      is_custom: false,
    },
    'lat-pulldown': {
      id: '3',
      title: 'Lat Pulldown',
      muscle_group: 'lats',
      equipment: 'machine',
      is_custom: false,
    },
  },
}));

describe('ExerciseLibraryPage', () => {
  it('renders the exercise library page', () => {
    render(<ExerciseLibraryPage />);
    expect(screen.getByText(/Exercise Library/i)).toBeInTheDocument();
  });

  it('displays exercises from mapping data', () => {
    render(<ExerciseLibraryPage />);
    expect(screen.getByText('Bench Press')).toBeInTheDocument();
    expect(screen.getByText('Squat')).toBeInTheDocument();
    expect(screen.getByText('Lat Pulldown')).toBeInTheDocument();
  });

  it('filters exercises by search query', () => {
    render(<ExerciseLibraryPage />);
    const searchInput = screen.getByPlaceholderText(/search/i);
    fireEvent.change(searchInput, { target: { value: 'bench' } });
    expect(screen.getByText('Bench Press')).toBeInTheDocument();
    expect(screen.queryByText('Squat')).not.toBeInTheDocument();
  });
});
