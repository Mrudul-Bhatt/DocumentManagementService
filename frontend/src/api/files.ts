import { authFetch } from './client'

export interface FileMetadataDto {
  id: string
  filename: string
  fileSize: number
  mimeType: string
  uploadedAt: string
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

export async function uploadFile(file: File): Promise<FileMetadataDto> {
  const form = new FormData()
  form.append('file', file)
  const res = await expectOk(await authFetch(BASE, { method: 'POST', body: form }))
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
