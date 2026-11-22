import apiClient from './apiClient';
import type { CreateMobilityRoutineRequest, MobilityDrillDto, MobilityRoutineDto, UpdateMobilityRoutineRequest } from '@/types/mobility';

/** `/api/v1/mobility-routines`: the user's own mobility, stretch and yoga routines. */
export const mobilityRoutinesApi = {
  list: async (): Promise<MobilityRoutineDto[]> => {
    const response = await apiClient.get<MobilityRoutineDto[]>('/mobility-routines');
    return response.data;
  },

  create: async (request: CreateMobilityRoutineRequest): Promise<MobilityRoutineDto> => {
    const response = await apiClient.post<MobilityRoutineDto>('/mobility-routines', request);
    return response.data;
  },

  /** Renames the routine and changes its target minutes; the drills are left as they are. */
  update: async (id: string, request: UpdateMobilityRoutineRequest): Promise<MobilityRoutineDto> => {
    const response = await apiClient.put<MobilityRoutineDto>(`/mobility-routines/${id}`, request);
    return response.data;
  },

  /** Deletes the routine. 409 while a plan's week still schedules it. */
  remove: async (id: string): Promise<void> => {
    await apiClient.delete(`/mobility-routines/${id}`);
  },

  /** Replaces the routine's drills in order. */
  replaceDrills: async (id: string, drills: MobilityDrillDto[]): Promise<MobilityRoutineDto> => {
    const response = await apiClient.put<MobilityRoutineDto>(`/mobility-routines/${id}/drills`, { drills });
    return response.data;
  },
};
