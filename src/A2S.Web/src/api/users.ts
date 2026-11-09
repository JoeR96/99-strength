import apiClient from './apiClient';

export interface UserDto {
  id: string;
  email: string;
  name: string;
  createdAt: string;
}

export interface CreateUserRequest {
  email: string;
  name: string;
}

export const usersApi = {
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

  /**
   * Create a new user
   */
  create: async (data: CreateUserRequest): Promise<UserDto> => {
    const response = await apiClient.post<UserDto>('/users', data);
    return response.data;
  },
};

export default usersApi;
