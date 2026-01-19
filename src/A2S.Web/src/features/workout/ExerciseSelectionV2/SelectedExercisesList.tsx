import { DndContext, closestCenter, DragOverlay, type DragEndEvent, type DragStartEvent } from "@dnd-kit/core";
import { SortableContext, verticalListSortingStrategy, useSortable } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { useState } from "react";
import type { SelectedExercise, DayNumber } from "@/types/workout";
import { SelectedExerciseCard } from "./SelectedExerciseCard";

interface SelectedExercisesListProps {
  exercises: SelectedExercise[];
  onReorder: (startIndex: number, endIndex: number) => void;
  onEdit: (exercise: SelectedExercise) => void;
  onRemove: (id: string) => void;
  groupByDay?: boolean;
}

/**
 * Sortable wrapper for SelectedExerciseCard
 */
function SortableExerciseCard({
  exercise,
  onEdit,
  onRemove,
}: {
  exercise: SelectedExercise;
  onEdit: (exercise: SelectedExercise) => void;
  onRemove: (id: string) => void;
}) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: exercise.id,
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
      />
    </div>
  );
}

/**
 * List of selected exercises with drag-and-drop reordering
 */
export function SelectedExercisesList({
  exercises,
  onReorder,
  onEdit,
  onRemove,
  groupByDay = false,
}: SelectedExercisesListProps) {
  const [activeId, setActiveId] = useState<string | null>(null);

  const handleDragStart = (event: DragStartEvent) => {
    setActiveId(event.active.id as string);
  };

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event;

    if (over && active.id !== over.id) {
      const oldIndex = exercises.findIndex((ex) => ex.id === active.id);
      const newIndex = exercises.findIndex((ex) => ex.id === over.id);

      if (oldIndex !== -1 && newIndex !== -1) {
        onReorder(oldIndex, newIndex);
      }
    }

    setActiveId(null);
  };

  const handleDragCancel = () => {
    setActiveId(null);
  };

  const activeExercise = exercises.find((ex) => ex.id === activeId);

  // Group exercises by day if requested
  if (groupByDay) {
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

    // Sort exercises within each day by orderInDay
    Object.keys(exercisesByDay).forEach((day) => {
      exercisesByDay[Number(day) as DayNumber].sort((a, b) => a.orderInDay - b.orderInDay);
    });

    // Filter out empty days
    const activeDays = (Object.keys(exercisesByDay) as unknown as DayNumber[]).filter(
      (day) => exercisesByDay[day].length > 0
    );

    return (
      <div className="space-y-6">
        {activeDays.length === 0 ? (
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
        ) : (
          activeDays.map((day) => (
            <div key={day}>
              <h4 className="text-sm font-semibold mb-3 flex items-center gap-2">
                <span className="inline-flex items-center justify-center w-6 h-6 rounded-full bg-primary/10 text-primary text-xs">
                  {day}
                </span>
                Day {day}
                <span className="text-xs font-normal text-muted-foreground">
                  ({exercisesByDay[day].length}{" "}
                  {exercisesByDay[day].length === 1 ? "exercise" : "exercises"})
                </span>
              </h4>
              <DndContext
                collisionDetection={closestCenter}
                onDragStart={handleDragStart}
                onDragEnd={handleDragEnd}
                onDragCancel={handleDragCancel}
              >
                <SortableContext
                  items={exercisesByDay[day].map((ex) => ex.id)}
                  strategy={verticalListSortingStrategy}
                >
                  <div className="space-y-2">
                    {exercisesByDay[day].map((exercise) => (
                      <SortableExerciseCard
                        key={exercise.id}
                        exercise={exercise}
                        onEdit={onEdit}
                        onRemove={onRemove}
                      />
                    ))}
                  </div>
                </SortableContext>
                <DragOverlay>
                  {activeExercise ? (
                    <SelectedExerciseCard
                      exercise={activeExercise}
                      onEdit={onEdit}
                      onRemove={onRemove}
                      isDragging
                    />
                  ) : null}
                </DragOverlay>
              </DndContext>
            </div>
          ))
        )}
      </div>
    );
  }

  // Flat list view (no grouping)
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
      collisionDetection={closestCenter}
      onDragStart={handleDragStart}
      onDragEnd={handleDragEnd}
      onDragCancel={handleDragCancel}
    >
      <SortableContext items={exercises.map((ex) => ex.id)} strategy={verticalListSortingStrategy}>
        <div className="space-y-2">
          {exercises.map((exercise) => (
            <SortableExerciseCard
              key={exercise.id}
              exercise={exercise}
              onEdit={onEdit}
              onRemove={onRemove}
            />
          ))}
        </div>
      </SortableContext>
      <DragOverlay>
        {activeExercise ? (
          <SelectedExerciseCard
            exercise={activeExercise}
            onEdit={onEdit}
            onRemove={onRemove}
            isDragging
          />
        ) : null}
      </DragOverlay>
    </DndContext>
  );
}
