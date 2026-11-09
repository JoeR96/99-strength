/**
 * Meals and shopping-list DTOs, mirroring A2S.Application/DTOs/MealDtos.cs.
 * Enums arrive as strings; dates are `yyyy-MM-dd` local (Europe/London) dates.
 */

export type MealType = 'Breakfast' | 'Lunch' | 'Dinner' | 'Snack';

export const MEAL_TYPES: readonly MealType[] = ['Breakfast', 'Lunch', 'Dinner', 'Snack'];

/** The closed unit set; quantities are only ever summed within one unit. */
export const INGREDIENT_UNITS = [
  'g',
  'kg',
  'ml',
  'l',
  'tbsp',
  'tsp',
  'piece',
  'clove',
  'slice',
  'handful',
  'pinch',
] as const;

export type IngredientUnit = (typeof INGREDIENT_UNITS)[number];

export interface IngredientDto {
  name: string;
  quantity: number | null;
  unit: string | null;
}

export interface MealDto {
  id: string;
  title: string;
  type: MealType;
  description: string;
  ingredients: IngredientDto[];
  steps: string[];
  tags: string[];
  cutFriendly: boolean;
  externalKey: string | null;
}

export interface SaveMealRequest {
  title: string;
  type: MealType;
  description?: string | null;
  ingredients: IngredientDto[];
  steps?: string[];
  tags?: string[];
  cutFriendly: boolean;
}

export interface WeekMealsDto {
  weekStart: string;
  mealIds: string[];
}

export interface ShoppingListItemDto {
  name: string;
  quantity: number | null;
  unit: string | null;
  checked: boolean;
  fromMealIds: string[];
}

export interface ShoppingListDto {
  /** Null when the week has no list yet. */
  id: string | null;
  weekStart: string;
  sourceMealIds: string[];
  items: ShoppingListItemDto[];
  checkedCount: number;
  totalCount: number;
}

export type ShoppingListExportFormat = 'text' | 'csv';
