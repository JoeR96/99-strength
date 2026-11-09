/** Push types mirroring `A2S.Application.DTOs.PushDtos` and `PushRequests`. Times are `HH:mm:ss`. */

export interface VapidPublicKeyDto {
  publicKey: string | null;
}

export interface PushSubscriptionDto {
  id: string;
  endpoint: string;
  userAgent: string | null;
  /** Local time the "time to train" nudge is wanted, `HH:mm:ss`. */
  sessionStartTime: string;
  createdAtUtc: string;
}

export interface SubscribePushRequest {
  endpoint: string;
  p256dh: string;
  auth: string;
  userAgent?: string | null;
  /** `HH:mm` or `HH:mm:ss`; omitted keeps the stored time (17:30 for a new subscription). */
  sessionStartTime?: string | null;
}
