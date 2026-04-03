import client from './client';
import type {
  FollowedLocalitiesResponse,
  SetFollowedLocalitiesBody,
} from '../types/api';

export async function getFollowedLocalities(): Promise<FollowedLocalitiesResponse> {
  const { data } =
    await client.get<FollowedLocalitiesResponse>('/v1/localities/followed');
  return data;
}

export async function setFollowedLocalities(
  body: SetFollowedLocalitiesBody,
): Promise<void> {
  await client.put('/v1/localities/followed', body);
}
