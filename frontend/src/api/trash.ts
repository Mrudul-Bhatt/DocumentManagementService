import { authFetch } from './client'

export type TrashedItemType = 'File' | 'Folder'

export interface TrashedItemDto {
  id: string
  name: string
  type: TrashedItemType
  deletedAt: string
}

const BASE = '/api/trash'

async function expectOk(res: Response): Promise<Response> {
  if (!res.ok) {
    const err = await res.json().catch(() => null)
    throw new Error(err?.detail ?? `Request failed (${res.status})`)
  }
  return res
}

export async function listTrash(): Promise<TrashedItemDto[]> {
  const res = await expectOk(await authFetch(BASE))
  return res.json()
}

export async function restoreFromTrash(id: string, itemType: TrashedItemType): Promise<void> {
  await expectOk(await authFetch(`${BASE}/${id}/restore`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ itemType }),
  }))
}

export async function emptyTrash(): Promise<void> {
  await expectOk(await authFetch(BASE, { method: 'DELETE' }))
}
