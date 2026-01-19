import { DndContext, closestCorners, DragOverlay, type DragEndEvent, type DragStartEvent, type DragOverEvent } from "@dnd-kit/core";
import { SortableContext, verticalListSortingStrategy, useSortable } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { useState } from "react";
import type { SelectedExercise, DayNumber, ProgramVariant } from "@/types/workout";
import { SelectedExerciseCard } from "./SelectedExerciseCard";

interface DayColumnsViewProps {
  exercises: SelectedExercise[];
  programVariant: ProgramVariant;
  onReorderAcrossDays: (exerciseId: string, newDay: DayNumber, newOrder: number) => void;
  onEdit: (exercise: SelectedExercise) => void;
  onRemove: (id: string) => void;
}

/**
 * Sortable wrapper for SelectedExerciseCard
 */
function SortableExerciseCard({
  exercise,
  showOrder,
  onEdit,
  onRemove,
}: {
  exercise: SelectedExercise;
  showOrder: boolean;
  onEdit: (exercise: SelectedExercise) => void;
  onRemove: (id: string) => void;
}) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: exercise.id,
    data: {
      exercise,
    },
  });

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
  };

  return (
    <div ref={setNodeRef} style={style} {...attributes} {...listeners}>
      <SelectedExerciseCard
        exercise={exercise}
        onEdit={onEdit}
        onRemove={onRemove}
        isDragging={isDragging}
        showOrder={showOrder}
      />
    </div>
  );
}

/**
 * Droppable day column
 */
function DayColumn({
  day,
  exercises,
  onEdit,
  onRemove,
}: {
  day: DayNumber;
  exercises: SelectedExercise[];
  onEdit: (exercise: SelectedExercise) => void;
  onRemove: (id: string) => void;
}) {
  const sortedExercises = [...exercises].sort((a, b) => a.orderInDay - b.orderInDay);

  return (
    <div className="flex flex-col h-full">
      {/* Day header */}
      <div className="mb-3 pb-3 border-b border-border">
        <div className="flex items-center gap-2">
          <span className="inline-flex items-center justify-center w-7 h-7 rounded-full bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-400 text-sm font-bold">
            {day}
          </span>
          <h3 className="text-base font-semibold text-foreground">Day {day}</h3>
        </div>
        <p className="text-xs text-muted-foreground mt-1">
          {exercises.length} {exercises.length === 1 ? "exercise" : "exercises"}
        </p>
      </div>

      {/* Droppable area */}
      <SortableContext
        id={`day-${day}`}
        items={sortedExercises.map((ex) => ex.id)}
        strategy={verticalListSortingStrategy}
      >
        <div className="flex-1 space-y-2 min-h-[200px]">
          {sortedExercises.length === 0 ? (
            <div className="h-full flex items-center justify-center border-2 border-dashed border-border rounded-lg p-6">
              <p className="text-sm text-muted-foreground text-center">
                Drop exercises here
              </p>
            </div>
          ) : (
            sortedExercises.map((exercise, index) => (
              <SortableExerciseCard
                key={exercise.id}
                exercise={exercise}
                showOrder={true}
                onEdit={onEdit}
                onRemove={onRemove}
              />
            ))
          )}
        </div>
      </SortableContext>
    </div>
  );
}

/**
 * Day columns view with drag-and-drop between days
 */
export function DayColumnsView({
  exercises,
  programVariant,
  onReorderAcrossDays,
  onEdit,
  onRemove,
}: DayColumnsViewProps) {
  const [activeId, setActiveId] = useState<string | null>(null);
  const [overId, setOverId] = useState<string | null>(null);

  // Group exercises by day
  const exercisesByDay: Record<DayNumber, SelectedExercise[]> = {
    1: [],
    2: [],
    3: [],
    4: [],
    5: [],
    6: [],
  };

  exercises.forEach((exercise) => {
    exercisesByDay[exercise.assignedDay].push(exercise);
  });

  // Get available days based on program variant
  const availableDays: DayNumber[] = [1, 2, 3, 4];
  if (programVariant >= 5) availableDays.push(5);
  if (programVariant >= 6) availableDays.push(6);

  const handleDragStart = (event: DragStartEvent) => {
    setActiveId(event.active.id as string);
  };

  const handleDragOver = (event: DragOverEvent) => {
    setOverId(event.over?.id as string | null);
  };

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event;

    if (!over) {
      setActiveId(null);
      setOverId(null);
      return;
    }

    const activeExercise = exercises.find((ex) => ex.id === active.id);
    if (!activeExercise) {
      setActiveId(null);
      setOverId(null);
      return;
    }

    // Determine target day
    let targetDay: DayNumber = activeExercise.assignedDay;
    let targetOrder: number = activeExercise.orderInDay;

    // Check if we're dropping on another exercise
    const overExercise = exercises.find((ex) => ex.id === over.id);
    if (overExercise) {
      targetDay = overExercise.assignedDay;
      const exercisesInTargetDay = exercisesByDay[targetDay].sort((a, b) => a.orderInDay - b.orderInDay);
      const overIndex = exercisesInTargetDay.findIndex((ex) => ex.id === over.id);

      // If dragging within same day
      if (targetDay === activeExercise.assignedDay) {
        const activeIndex = exercisesInTargetDay.findIndex((ex) => ex.id === active.id);
        if (activeIndex < overIndex) {
          // Moving down, insert after
          targetOrder = overExercise.orderInDay;
        } else {
          // Moving up, insert before
          targetOrder = overExercise.orderInDay;
        }
      } else {
        // Moving to different day, insert at the over position
        targetOrder = overIndex + 1;
      }
    } else if (over.id.toString().startsWith('day-')) {
      // Dropping on empty day column
      const dayMatch = over.id.toString().match(/day-(\d+)/);
      if (dayMatch) {
        targetDay = parseInt(dayMatch[1]) as DayNumber;
        targetOrder = exercisesByDay[targetDay].length + 1;
      }
    }

    // Only update if something changed
    if (targetDay !== activeExercise.assignedDay || targetOrder !== activeExercise.orderInDay) {
      onReorderAcrossDays(activeExercise.id, targetDay, targetOrder);
    }

    setActiveId(null);
    setOverId(null);
  };

  const handleDragCancel = () => {
    setActiveId(null);
    setOverId(null);
  };

  const activeExercise = exercises.find((ex) => ex.id === activeId);

  if (exercises.length === 0) {
    return (
      <div className="text-center py-12 text-muted-foreground">
        <svg
          className="w-12 h-12 mx-auto mb-3 opacity-50"
          fill="none"
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth="2"
          viewBox="0 0 24 24"
          stroke="currentColor"
        >
          <path d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
        </svg>
        <p className="font-medium">No exercises selected yet</p>
        <p className="text-sm mt-1">Add exercises from the library to get started</p>
      </div>
    );
  }

  return (
    <DndContext
      collisionDetection={closestCorners}
      onDragStart={handleDragStart}
      onDragOver={handleDragOver}
      onDragEnd={handleDragEnd}
      onDragCancel={handleDragCancel}
    >
      {/* Day columns grid */}
      <div className="grid grid-cols-2 lg:grid-cols-3 gap-4">
        {availableDays.map((day) => (
          <div key={day} className="bg-muted/30 rounded-lg p-4 border border-border">
            <DayColumn
              day={day}
              exercises={exercisesByDay[day]}
              onEdit={onEdit}
              onRemove={onRemove}
            />
          </div>
        ))}
      </div>

      {/* Drag overlay */}
      <DragOverlay>
        {activeExercise ? (
          <div className="opacity-90">
            <SelectedExerciseCard
              exercise={activeExercise}
              onEdit={onEdit}
              onRemove={onRemove}
              isDragging
              showOrder={true}
            />
          </div>
        ) : null}
      </DragOverlay>
    </DndContext>
  );
}
