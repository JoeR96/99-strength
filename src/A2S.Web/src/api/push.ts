import apiClient from './apiClient';
import type { PushSubscriptionDto, SubscribePushRequest, VapidPublicKeyDto } from '@/types/push';

/** `/api/v1/push`: the server's VAPID key and this user's subscriptions (one per device). */
export const pushApi = {
  /** `null` when push is not configured on the server. */
  getVapidPublicKey: async (): Promise<string | null> => {
    const response = await apiClient.get<VapidPublicKeyDto>('/push/vapid-public-key');
    return response.data.publicKey;
  },

  listSubscriptions: async (): Promise<PushSubscriptionDto[]> => {
    const response = await apiClient.get<PushSubscriptionDto[]>('/push/subscriptions');
    return response.data;
  },

  /** Stores or refreshes a browser subscription; matched on endpoint. */
  subscribe: async (request: SubscribePushRequest): Promise<PushSubscriptionDto> => {
    const response = await apiClient.post<PushSubscriptionDto>('/push/subscribe', request);
    return response.data;
  },

  unsubscribe: async (endpoint: string): Promise<void> => {
    await apiClient.delete('/push/subscribe', { data: { endpoint } });
  },
};
