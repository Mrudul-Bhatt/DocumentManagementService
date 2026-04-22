export interface FileMetadataDto {
  id: string
  filename: string
  fileSize: number
  mimeType: string
  uploadedAt: string
}

const BASE = '/api/files'

function headers(userId: string): HeadersInit {
  return { 'X-User-Id': userId }
}

export async function listFiles(userId: string): Promise<FileMetadataDto[]> {
  const res = await fetch(BASE, { headers: headers(userId) })
  if (!res.ok) throw new Error(await res.text())
  return res.json()
}

export async function uploadFile(userId: string, file: File): Promise<FileMetadataDto> {
  const form = new FormData()
  form.append('file', file)
  const res = await fetch(BASE, {
    method: 'POST',
    headers: headers(userId),
    body: form,
  })
  if (!res.ok) throw new Error(await res.text())
  return res.json()
}

export async function downloadFile(userId: string, id: string, filename: string): Promise<void> {
  const res = await fetch(`${BASE}/${id}`, { headers: headers(userId) })
  if (!res.ok) throw new Error(await res.text())
  const blob = await res.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
  URL.revokeObjectURL(url)
}

export async function deleteFile(userId: string, id: string): Promise<void> {
  const res = await fetch(`${BASE}/${id}`, {
    method: 'DELETE',
    headers: headers(userId),
  })
  if (!res.ok) throw new Error(await res.text())
}
