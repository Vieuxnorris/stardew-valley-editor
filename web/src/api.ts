// Client for the mod's JSON API. Every call carries the per-session token from the URL the
// mod printed (`?token=...`), kept in sessionStorage so reloads keep working.

const TOKEN_KEY = 'valley-editor-token';

function readToken(): string | null {
  const params = new URLSearchParams(location.search);
  const fromUrl = params.get('token');
  if (fromUrl) {
    try {
      sessionStorage.setItem(TOKEN_KEY, fromUrl);
    } catch {
      // storage blocked: the token still works for this page view
    }
    params.delete('token');
    const query = params.toString();
    history.replaceState(null, '', location.pathname + (query ? `?${query}` : '') + location.hash);
    return fromUrl;
  }
  try {
    return sessionStorage.getItem(TOKEN_KEY);
  } catch {
    return null;
  }
}

const token = readToken();

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

export async function api<T>(method: string, path: string, body?: unknown): Promise<T> {
  let response: Response;
  try {
    response = await fetch(path, {
      method,
      headers: {
        'X-Editor-Token': token ?? '',
        ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiError(0, 'offline');
  }

  const data = await response.json().catch(() => null);
  if (!response.ok) throw new ApiError(response.status, data?.error ?? response.statusText);
  return data as T;
}

export const hasToken = token !== null;

export interface Status {
  worldReady: boolean;
  saveName: string | null;
  farmName: string | null;
  unsavedChanges: boolean;
  gameVersion: string;
  modVersion: string;
}

export interface Player {
  name: string;
  farmName: string;
  money: number;
}
