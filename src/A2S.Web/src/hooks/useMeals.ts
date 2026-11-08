import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { mealsApi } from '../api/meals';
import type { SaveMealRequest, ShoppingListDto } from '../types/meals';

export const mealKeys = {
  all: ['meals'] as const,
  list: () => [...mealKeys.all, 'list'] as const,
  week: (week: string) => [...mealKeys.all, 'week', week] as const,
  shoppingList: (week: string) => ['shoppingList', week] as const,
};

export function useMeals() {
  return useQuery({
    queryKey: mealKeys.list(),
    queryFn: () => mealsApi.getMeals(),
    staleTime: 1000 * 60 * 5,
  });
}

export function useWeekMeals(week: string) {
  return useQuery({
    queryKey: mealKeys.week(week),
    queryFn: () => mealsApi.getWeekMeals(week),
  });
}

export function useShoppingList(week: string) {
  return useQuery({
    queryKey: mealKeys.shoppingList(week),
    queryFn: () => mealsApi.getShoppingList(week),
  });
}

export function useSaveMeal() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id?: string; request: SaveMealRequest }) =>
      id ? mealsApi.updateMeal(id, request) : mealsApi.createMeal(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: mealKeys.list() }),
  });
}

export function useDeleteMeal() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => mealsApi.deleteMeal(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: mealKeys.all }),
  });
}

/** Sets the week's menu. The cached menu is replaced straight away; the list is refetched. */
export function useSetWeekMeals(week: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (mealIds: string[]) => mealsApi.setWeekMeals(week, mealIds),
    onSuccess: (data) => {
      queryClient.setQueryData(mealKeys.week(week), data);
      queryClient.invalidateQueries({ queryKey: mealKeys.shoppingList(week) });
    },
  });
}

export function useGenerateShoppingList(week: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => mealsApi.generateShoppingList(week),
    onSuccess: (data) => {
      queryClient.setQueryData(mealKeys.shoppingList(week), data);
      queryClient.invalidateQueries({ queryKey: mealKeys.week(week) });
    },
  });
}

/** Ticks a line optimistically so the row flips under the thumb, then settles on the server's answer. */
export function useToggleShoppingItem(week: string) {
  const queryClient = useQueryClient();
  const key = mealKeys.shoppingList(week);
  return useMutation({
    mutationFn: ({ listId, name, unit }: { listId: string; name: string; unit: string | null }) =>
      mealsApi.toggleShoppingItem(listId, name, unit),
    onMutate: async ({ name, unit }) => {
      await queryClient.cancelQueries({ queryKey: key });
      const previous = queryClient.getQueryData<ShoppingListDto>(key);
      if (previous) {
        const items = previous.items.map((i) =>
          i.name === name && i.unit === unit ? { ...i, checked: !i.checked } : i
        );
        queryClient.setQueryData<ShoppingListDto>(key, {
          ...previous,
          items,
          checkedCount: items.filter((i) => i.checked).length,
        });
      }
      return { previous };
    },
    onError: (_error, _vars, context) => {
      if (context?.previous) queryClient.setQueryData(key, context.previous);
    },
    onSuccess: (data) => queryClient.setQueryData(key, data),
  });
}
