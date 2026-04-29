export interface AuthTokensDto {
  accessToken: string
  refreshToken: string
  expiresInSeconds: number
}

export async function register(email: string, password: string): Promise<AuthTokensDto> {
  const res = await fetch('/api/auth/register', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })
  if (!res.ok) {
    const err = await res.json().catch(() => null)
    throw new Error(err?.detail ?? 'Registration failed')
  }
  return res.json()
}

export async function login(email: string, password: string): Promise<AuthTokensDto> {
  const res = await fetch('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })
  if (!res.ok) {
    const err = await res.json().catch(() => null)
    throw new Error(err?.detail ?? 'Invalid email or password')
  }
  return res.json()
}

export async function revokeToken(token: string): Promise<void> {
  await fetch('/api/auth/revoke', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ token }),
  })
}
