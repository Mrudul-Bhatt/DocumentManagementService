import { authFetch } from './client'

export type ShareResourceType = 'File' | 'Folder'
export type ShareRole = 'Viewer' | 'Commenter' | 'Editor'

export interface ShareDto {
  id: string
  resourceId: string
  resourceType: ShareResourceType
  grantedToUserId: string
  grantedToEmail: string
  role: ShareRole
  createdAt: string
}

export interface SharedResourceDto {
  id: string
  name: string
  resourceType: ShareResourceType
  role: ShareRole
  sharedByUserId: string
  sharedAt: string
}

const BASE = '/api/shares'

async function expectOk(res: Response): Promise<Response> {
  if (!res.ok) {
    const err = await res.json().catch(() => null)
    throw new Error(err?.detail ?? `Request failed (${res.status})`)
  }
  return res
}

export async function createShare(
  resourceId: string,
  resourceType: ShareResourceType,
  grantedToEmail: string,
  role: ShareRole,
): Promise<ShareDto> {
  const res = await expectOk(await authFetch(BASE, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ resourceId, resourceType, grantedToEmail, role }),
  }))
  return res.json()
}

export async function revokeShare(shareId: string): Promise<void> {
  await expectOk(await authFetch(`${BASE}/${shareId}`, { method: 'DELETE' }))
}

export async function listShares(resourceId: string, resourceType: ShareResourceType): Promise<ShareDto[]> {
  const res = await expectOk(await authFetch(`${BASE}?resourceId=${resourceId}&resourceType=${resourceType}`))
  return res.json()
}

export async function listSharedWithMe(): Promise<SharedResourceDto[]> {
  const res = await expectOk(await authFetch(`${BASE}/me`))
  return res.json()
}
