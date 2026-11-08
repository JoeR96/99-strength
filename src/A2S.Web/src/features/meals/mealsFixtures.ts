import type { MealDto, ShoppingListDto, WeekMealsDto } from '@/types/meals';

/** Shared fixtures for the meals stories and tests. Deterministic ids and dates. */

export const WEEK = '2026-11-09';

export const orzo: MealDto = {
  id: '11111111-1111-1111-1111-111111111111',
  title: 'Creamy Lemon Orzo with Skinless Chicken Thighs',
  type: 'Dinner',
  description: 'A light, lemony orzo dish.',
  ingredients: [
    { name: 'skinless boneless chicken thigh', quantity: 4, unit: 'piece' },
    { name: 'orzo', quantity: 200, unit: 'g' },
    { name: 'garlic', quantity: 2, unit: 'clove' },
    { name: 'salt', quantity: null, unit: null },
  ],
  steps: ['Sear.', 'Simmer.'],
  tags: ['chicken', 'pasta'],
  cutFriendly: true,
  externalKey: 'cooks:creamy-lemon-orzo-with-skinless-chicken-thighs',
};

export const barbacoa: MealDto = {
  id: '22222222-2222-2222-2222-222222222222',
  title: 'Slow-Cooked Beef Barbacoa',
  type: 'Dinner',
  description: 'Mexican-style shredded beef.',
  ingredients: [
    { name: 'beef chuck', quantity: 1.2, unit: 'kg' },
    { name: 'garlic', quantity: 4, unit: 'clove' },
  ],
  steps: ['Sear.', 'Slow cook.'],
  tags: ['beef', 'slow-cooker'],
  cutFriendly: true,
  externalKey: 'cooks:slow-cooked-beef-barbacoa',
};

export const oats: MealDto = {
  id: '33333333-3333-3333-3333-333333333333',
  title: 'Classic Overnight Oats',
  type: 'Breakfast',
  description: 'The base recipe.',
  ingredients: [
    { name: 'rolled oats', quantity: 80, unit: 'g' },
    { name: 'milk', quantity: 200, unit: 'ml' },
    { name: 'salt', quantity: 1, unit: 'pinch' },
  ],
  steps: ['Stir.', 'Chill.'],
  tags: ['breakfast', 'make-ahead'],
  cutFriendly: true,
  externalKey: 'cooks:classic-overnight-oats',
};

export const meals: MealDto[] = [oats, orzo, barbacoa];

export const weekWithOrzoAndBarbacoa: WeekMealsDto = { weekStart: WEEK, mealIds: [orzo.id, barbacoa.id] };

export const emptyWeek: WeekMealsDto = { weekStart: WEEK, mealIds: [] };

export const emptyList: ShoppingListDto = {
  id: null,
  weekStart: WEEK,
  sourceMealIds: [],
  items: [],
  checkedCount: 0,
  totalCount: 0,
};

export const generatedList: ShoppingListDto = {
  id: '44444444-4444-4444-4444-444444444444',
  weekStart: WEEK,
  sourceMealIds: [orzo.id, barbacoa.id],
  items: [
    { name: 'beef chuck', quantity: 1.2, unit: 'kg', checked: false, fromMealIds: [barbacoa.id] },
    { name: 'garlic', quantity: 6, unit: 'clove', checked: true, fromMealIds: [orzo.id, barbacoa.id] },
    { name: 'orzo', quantity: 200, unit: 'g', checked: false, fromMealIds: [orzo.id] },
    { name: 'salt', quantity: null, unit: null, checked: false, fromMealIds: [orzo.id] },
    { name: 'skinless boneless chicken thigh', quantity: 4, unit: 'piece', checked: false, fromMealIds: [orzo.id] },
  ],
  checkedCount: 1,
  totalCount: 5,
};

export const generatedListText = '- [ ] 1.2 kg beef chuck\n- [x] 6 clove garlic\n- [ ] 200 g orzo\n- [ ] salt\n- [ ] 4 piece skinless boneless chicken thigh';
