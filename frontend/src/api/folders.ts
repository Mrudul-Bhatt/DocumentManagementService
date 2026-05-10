import { authFetch } from './client'

export interface FolderDto {
  id: string
  name: string
  parentFolderId: string | null
  createdAt: string
}

export interface FolderContentsDto {
  folders: FolderDto[]
  files: import('./files').FileMetadataDto[]
}

const BASE = '/api/folders'

async function expectOk(res: Response): Promise<Response> {
  if (!res.ok) {
    const err = await res.json().catch(() => null)
    throw new Error(err?.detail ?? `Request failed (${res.status})`)
  }
  return res
}

export async function getFolderContents(folderId: string | null): Promise<FolderContentsDto> {
  const url = folderId === null ? `${BASE}/root` : `${BASE}/${folderId}`
  const res = await expectOk(await authFetch(url))
  return res.json()
}

export async function createFolder(name: string, parentFolderId: string | null): Promise<FolderDto> {
  const res = await expectOk(await authFetch(BASE, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name, parentFolderId }),
  }))
  return res.json()
}

export async function renameFolder(id: string, newName: string): Promise<void> {
  await expectOk(await authFetch(`${BASE}/${id}/name`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ newName }),
  }))
}

export async function deleteFolder(id: string): Promise<void> {
  await expectOk(await authFetch(`${BASE}/${id}`, { method: 'DELETE' }))
}

export async function restoreFolder(id: string): Promise<void> {
  await expectOk(await authFetch(`${BASE}/${id}/restore`, { method: 'POST' }))
}
