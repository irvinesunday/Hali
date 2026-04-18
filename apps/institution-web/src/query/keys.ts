// Central registry for TanStack Query cache keys. Keeping them in one
// file prevents stale keys drifting across screens — when a mutation
// in #203 (post update) needs to invalidate the signals list or a
// cluster detail, the key is imported from here, not re-spelled.
//
// Nested arrays let us invalidate broader scopes with
// `queryClient.invalidateQueries({ queryKey: institutionKeys.all })`.

export const institutionKeys = {
  all: ["institution"] as const,
  overview: () => [...institutionKeys.all, "overview"] as const,
  signalsAll: () => [...institutionKeys.all, "signals"] as const,
  signalsList: (params: { areaId?: string; state?: string; cursor?: string }) =>
    [...institutionKeys.signalsAll(), "list", params] as const,
  signalDetail: (clusterId: string) =>
    [...institutionKeys.signalsAll(), "detail", clusterId] as const,
  areas: () => [...institutionKeys.all, "areas"] as const,
};
