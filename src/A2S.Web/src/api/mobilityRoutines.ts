import apiClient from './apiClient';
import type { CreateMobilityRoutineRequest, MobilityDrillDto, MobilityRoutineDto } from '@/types/mobility';

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

  /** Replaces the routine's drills in order. Name and target minutes are set on create only. */
  replaceDrills: async (id: string, drills: MobilityDrillDto[]): Promise<MobilityRoutineDto> => {
    const response = await apiClient.put<MobilityRoutineDto>(`/mobility-routines/${id}/drills`, { drills });
    return response.data;
  },
};
