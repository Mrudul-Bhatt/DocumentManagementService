import { authFetch } from './client'
import type { ShareResourceType, ShareRole } from './shares'

export interface PublicLinkDto {
  id: string
  resourceId: string
  resourceType: ShareResourceType
  token: string
  role: ShareRole
  expiresAt: string | null
  hasPassword: boolean
  createdAt: string
}

const BASE = '/api/public'

async function expectOk(res: Response): Promise<Response> {
  if (!res.ok) {
    const err = await res.json().catch(() => null)
    throw new Error(err?.detail ?? `Request failed (${res.status})`)
  }
  return res
}

export async function createPublicLink(
  resourceId: string,
  resourceType: ShareResourceType,
  role: ShareRole,
  expiresAt: string | null,
  password: string | null,
): Promise<PublicLinkDto> {
  const res = await expectOk(await authFetch(BASE, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ resourceId, resourceType, role, expiresAt, password }),
  }))
  return res.json()
}

export async function revokePublicLink(linkId: string): Promise<void> {
  await expectOk(await authFetch(`${BASE}/${linkId}`, { method: 'DELETE' }))
}

export async function listPublicLinks(resourceId: string, resourceType: ShareResourceType): Promise<PublicLinkDto[]> {
  const res = await expectOk(await authFetch(`${BASE}?resourceId=${resourceId}&resourceType=${resourceType}`))
  return res.json()
}
