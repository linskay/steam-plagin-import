export interface Game {
  appId: string;
  steamAppId?: string;
  name: string;
  installed: boolean;
  description: string;
  descriptionSource?: string;
  developer: string;
  publisher: string;
}
export interface Snapshot {
  schemaVersion: number;
  steamAccountId: string;
  syncedAt: string;
  games: Game[];
}
export function parseSnapshot(raw: unknown, accountId?: string): Snapshot {
  // Millennium's FFI may already deserialize JSON strings into an object.
  // Frontend-to-webview FFI in 3.6.3 returns a CDP string envelope instead.
  const envelope = raw as {type?: string; value?: unknown};
  if (envelope?.type === 'string' && typeof envelope.value === 'string') raw = envelope.value;
  const value = typeof raw === 'string' ? JSON.parse(raw) : raw as Snapshot;
  if (!value || typeof value !== 'object') throw new Error('Invalid EGS snapshot');
  if (value.schemaVersion !== 1 || !Array.isArray(value.games)) throw new Error('Unsupported EGS snapshot');
  if (accountId && value.steamAccountId !== accountId) throw new Error('Snapshot belongs to another Steam account');
  const games: Game[] = value.games.filter((g: Game) =>
    typeof g.appId === 'string' && /^\d+$/.test(g.appId) && typeof g.name === 'string' && typeof g.installed === 'boolean'
  ).map((g: Game) => ({...g, description: String(g.description || ''), developer: String(g.developer || ''), publisher: String(g.publisher || '')}));
  return {...value, games};
}
export function findGame(games: Game[], appId?: string, name?: string): Game | undefined {
  if (appId) return games.find(g => g.appId === appId);
  const matches = games.filter(g => g.name === name);
  return matches.length === 1 ? matches[0] : undefined;
}
