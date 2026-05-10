import { authFetch } from './client'

export interface FileMetadataDto {
  id: string
  filename: string
  fileSize: number
  mimeType: string
  uploadedAt: string
  folderId: string | null
}

export interface FileVersionDto {
  id: string
  versionNumber: number
  fileSize: number
  uploadedBy: string
  createdAt: string
}

const BASE = '/api/files'

async function expectOk(res: Response): Promise<Response> {
  if (!res.ok) {
    const err = await res.json().catch(() => null)
    throw new Error(err?.detail ?? `Request failed (${res.status})`)
  }
  return res
}

export async function listFiles(): Promise<FileMetadataDto[]> {
  const res = await expectOk(await authFetch(BASE))
  return res.json()
}

export async function uploadFile(file: File, folderId: string | null = null): Promise<FileMetadataDto> {
  const form = new FormData()
  form.append('file', file)
  const url = folderId ? `${BASE}?folderId=${folderId}` : BASE
  const res = await expectOk(await authFetch(url, { method: 'POST', body: form }))
  return res.json()
}

export async function downloadFile(id: string, filename: string): Promise<void> {
  const res = await expectOk(await authFetch(`${BASE}/${id}`))
  const blob = await res.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
  URL.revokeObjectURL(url)
}

export async function deleteFile(id: string): Promise<void> {
  await expectOk(await authFetch(`${BASE}/${id}`, { method: 'DELETE' }))
}

export async function listVersions(fileId: string): Promise<FileVersionDto[]> {
  const res = await expectOk(await authFetch(`${BASE}/${fileId}/versions`))
  return res.json()
}

export async function restoreVersion(fileId: string, versionId: string): Promise<void> {
  await expectOk(await authFetch(`${BASE}/${fileId}/versions/${versionId}/restore`, { method: 'POST' }))
}

export async function deleteVersion(fileId: string, versionId: string): Promise<void> {
  await expectOk(await authFetch(`${BASE}/${fileId}/versions/${versionId}`, { method: 'DELETE' }))
}
