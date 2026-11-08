import apiClient from './apiClient';
import type {
  MealDto,
  SaveMealRequest,
  ShoppingListDto,
  ShoppingListExportFormat,
  WeekMealsDto,
} from '../types/meals';

/**
 * Typed client for /meals and /shopping-list. `week` is any `yyyy-MM-dd` date in the week;
 * the API snaps it to the Monday.
 */
export const mealsApi = {
  /** Lists the user's meals; the API seeds the starter recipes on a first visit. */
  getMeals: async (): Promise<MealDto[]> => {
    const response = await apiClient.get<MealDto[]>('/meals');
    return response.data;
  },

  createMeal: async (request: SaveMealRequest): Promise<MealDto> => {
    const response = await apiClient.post<MealDto>('/meals', request);
    return response.data;
  },

  updateMeal: async (id: string, request: SaveMealRequest): Promise<MealDto> => {
    const response = await apiClient.put<MealDto>(`/meals/${id}`, request);
    return response.data;
  },

  deleteMeal: async (id: string): Promise<void> => {
    await apiClient.delete(`/meals/${id}`);
  },

  getWeekMeals: async (week: string): Promise<WeekMealsDto> => {
    const response = await apiClient.get<WeekMealsDto>('/meals/week', { params: { week } });
    return response.data;
  },

  setWeekMeals: async (weekStart: string, mealIds: string[]): Promise<WeekMealsDto> => {
    const response = await apiClient.put<WeekMealsDto>('/meals/week', { weekStart, mealIds });
    return response.data;
  },

  getShoppingList: async (week: string): Promise<ShoppingListDto> => {
    const response = await apiClient.get<ShoppingListDto>('/shopping-list', { params: { week } });
    return response.data;
  },

  generateShoppingList: async (weekStart: string): Promise<ShoppingListDto> => {
    const response = await apiClient.post<ShoppingListDto>('/shopping-list/generate', { weekStart });
    return response.data;
  },

  toggleShoppingItem: async (listId: string, name: string, unit: string | null): Promise<ShoppingListDto> => {
    const response = await apiClient.post<ShoppingListDto>('/shopping-list/items/toggle', { listId, name, unit });
    return response.data;
  },

  /** The list as text (`- [x] 2 clove garlic`) or CSV (`name,quantity,unit,checked`), exactly as the server renders it. */
  exportShoppingList: async (week: string, format: ShoppingListExportFormat): Promise<string> => {
    const response = await apiClient.get<string>('/shopping-list/export', {
      params: { week, format },
      responseType: 'text',
      transformResponse: [(data) => data],
    });
    return response.data;
  },
};
