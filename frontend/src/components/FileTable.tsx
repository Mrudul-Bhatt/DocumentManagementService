import { useState } from 'react'
import { Download, Trash2, FileText, Loader2 } from 'lucide-react'
import type { FileMetadataDto } from '../api/files'
import { downloadFile, deleteFile } from '../api/files'
import { useToast } from './Toast'
import { formatBytes, formatDate } from '../lib/utils'

interface Props {
  files: FileMetadataDto[]
  onDeleted: () => void
}

const thStyle: React.CSSProperties = {
  padding: '0.75rem 1rem 0.75rem 0', fontWeight: 500, fontSize: '0.7rem',
  color: '#6b7280', textTransform: 'uppercase', letterSpacing: '0.05em',
  borderBottom: '1px solid #e5e7eb', textAlign: 'left',
}
const tdStyle: React.CSSProperties = {
  padding: '0.75rem 1rem 0.75rem 0', fontSize: '0.875rem',
  borderBottom: '1px solid #f3f4f6', color: '#374151',
}

export function FileTable({ files, onDeleted }: Props) {
  const [deletingId, setDeletingId] = useState<string | null>(null)
  const [downloadingId, setDownloadingId] = useState<string | null>(null)
  const { toast } = useToast()

  const handleDownload = async (file: FileMetadataDto) => {
    setDownloadingId(file.id)
    try {
      await downloadFile(file.id, file.filename)
    } catch (err: unknown) {
      toast(err instanceof Error ? err.message : 'Download failed', 'error')
    } finally {
      setDownloadingId(null)
    }
  }

  const handleDelete = async (file: FileMetadataDto) => {
    if (!confirm(`Delete "${file.filename}"?`)) return
    setDeletingId(file.id)
    try {
      await deleteFile(file.id)
      toast(`"${file.filename}" deleted`)
      onDeleted()
    } catch (err: unknown) {
      toast(err instanceof Error ? err.message : 'Delete failed', 'error')
    } finally {
      setDeletingId(null)
    }
  }

  if (files.length === 0) {
    return (
      <div style={{ textAlign: 'center', padding: '4rem 0', color: '#9ca3af' }}>
        <FileText style={{ margin: '0 auto 0.75rem', display: 'block' }} size={40} />
        <p style={{ margin: 0, fontSize: '0.875rem' }}>No files yet. Upload your first file.</p>
      </div>
    )
  }

  return (
    <div style={{ overflowX: 'auto' }}>
      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.875rem' }}>
        <thead>
          <tr>
            <th style={thStyle}>Name</th>
            <th style={thStyle}>Size</th>
            <th style={thStyle}>Type</th>
            <th style={thStyle}>Uploaded</th>
            <th style={{ ...thStyle, textAlign: 'right' }}>Actions</th>
          </tr>
        </thead>
        <tbody>
          {files.map(file => (
            <tr key={file.id}>
              <td style={{ ...tdStyle, fontWeight: 500, color: '#111827', maxWidth: '16rem', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{file.filename}</td>
              <td style={{ ...tdStyle, color: '#6b7280' }}>{formatBytes(file.fileSize)}</td>
              <td style={tdStyle}>
                <span style={{ background: '#f3f4f6', color: '#6b7280', padding: '0.125rem 0.5rem', borderRadius: '0.25rem', fontSize: '0.75rem' }}>
                  {file.mimeType.split('/')[1] ?? file.mimeType}
                </span>
              </td>
              <td style={{ ...tdStyle, color: '#6b7280' }}>{formatDate(file.uploadedAt)}</td>
              <td style={{ ...tdStyle, textAlign: 'right' }}>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: '0.25rem' }}>
                  <button
                    onClick={() => handleDownload(file)}
                    disabled={downloadingId === file.id}
                    title="Download"
                    style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '0.375rem', borderRadius: '0.25rem', color: '#6b7280', opacity: downloadingId === file.id ? 0.5 : 1 }}
                  >
                    {downloadingId === file.id ? <Loader2 size={16} style={{ animation: 'spin 1s linear infinite' }} /> : <Download size={16} />}
                  </button>
                  <button
                    onClick={() => handleDelete(file)}
                    disabled={deletingId === file.id}
                    title="Delete"
                    style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '0.375rem', borderRadius: '0.25rem', color: '#6b7280', opacity: deletingId === file.id ? 0.5 : 1 }}
                  >
                    {deletingId === file.id ? <Loader2 size={16} /> : <Trash2 size={16} />}
                  </button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
