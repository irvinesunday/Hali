import client from './client';
import type {
  SignalPreviewRequest,
  SignalPreviewResponse,
  SignalSubmitRequest,
  SignalSubmitResponse,
} from '../types/api';

export async function previewSignal(
  body: SignalPreviewRequest,
): Promise<SignalPreviewResponse> {
  const { data } = await client.post<SignalPreviewResponse>(
    '/v1/signals/preview',
    body,
  );
  return data;
}

export async function submitSignal(
  body: SignalSubmitRequest,
): Promise<SignalSubmitResponse> {
  const { data } = await client.post<SignalSubmitResponse>(
    '/v1/signals/submit',
    body,
  );
  return data;
}
