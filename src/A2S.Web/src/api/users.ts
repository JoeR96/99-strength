import apiClient from './apiClient';

export interface UserDto {
  id: string;
  email: string;
  name: string;
  createdAt: string;
}

export interface UserPreferencesDto {
  pushRoutinesToHevy: boolean;
}

export const usersApi = {
  getPreferences: async (): Promise<UserPreferencesDto> => (await apiClient.get<UserPreferencesDto>('/users/me/preferences')).data,

  setPreferences: async (preferences: UserPreferencesDto): Promise<UserPreferencesDto> =>
    (await apiClient.put<UserPreferencesDto>('/users/me/preferences', preferences)).data,

  /**
   * Get user by ID
   */
  getById: async (id: string): Promise<UserDto> => {
    const response = await apiClient.get<UserDto>(`/users/${id}`);
    return response.data;
  },

  /**
   * Get current user (authenticated user)
   */
  getCurrentUser: async (): Promise<UserDto> => {
    const response = await apiClient.get<UserDto>('/users/me');
    return response.data;
  },
};

export default usersApi;
