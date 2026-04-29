// Access token lives in memory only — never persisted (XSS protection)
// Refresh token lives in localStorage — acceptable tradeoff for a non-httpOnly-cookie setup
let _accessToken: string | null = null
let _refreshToken: string | null = localStorage.getItem('refreshToken')
let _onLogout: (() => void) | null = null

export function setTokens(access: string, refresh: string): void {
  _accessToken = access
  _refreshToken = refresh
  localStorage.setItem('refreshToken', refresh)
}

export function clearTokens(): void {
  _accessToken = null
  _refreshToken = null
  localStorage.removeItem('refreshToken')
}

export function getStoredRefreshToken(): string | null {
  return _refreshToken
}

export function registerLogoutCallback(cb: () => void): void {
  _onLogout = cb
}

export async function authFetch(url: string, options: RequestInit = {}): Promise<Response> {
  const res = await doFetch(url, options)

  if (res.status !== 401) return res

  // Access token expired — try a silent refresh
  const refreshed = await tryRefresh()
  if (!refreshed) {
    _onLogout?.()
    return res
  }

  // Retry the original request with the new access token
  return doFetch(url, options)
}

function doFetch(url: string, options: RequestInit): Promise<Response> {
  return fetch(url, {
    ...options,
    headers: {
      ...options.headers,
      Authorization: `Bearer ${_accessToken}`,
    },
  })
}

async function tryRefresh(): Promise<boolean> {
  if (!_refreshToken) return false

  try {
    const res = await fetch('/api/auth/refresh', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token: _refreshToken }),
    })
    if (!res.ok) return false
    const data = await res.json()
    setTokens(data.accessToken, data.refreshToken)
    return true
  } catch {
    return false
  }
}
