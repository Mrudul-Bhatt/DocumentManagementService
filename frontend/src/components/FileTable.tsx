import { useState } from 'react'
import { Download, Trash2, FileText, Loader2, ChevronRight, Folder, Clock, FolderX, Share2 } from 'lucide-react'
import type { FileMetadataDto } from '../api/files'
import type { FolderDto } from '../api/folders'
import { downloadFile, deleteFile } from '../api/files'
import { deleteFolder } from '../api/folders'
import { useToast } from './Toast'
import { VersionsModal } from './VersionsModal'
import { ShareModal } from './ShareModal'
import { formatBytes, formatDate } from '../lib/utils'

interface Props {
  folders: FolderDto[]
  files: FileMetadataDto[]
  onFolderOpen: (folder: FolderDto) => void
  onRefresh: () => void
}

interface ShareTarget {
  id: string
  name: string
  type: 'File' | 'Folder'
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

export function FileTable({ folders, files, onFolderOpen, onRefresh }: Props) {
  const [deletingId, setDeletingId] = useState<string | null>(null)
  const [downloadingId, setDownloadingId] = useState<string | null>(null)
  const [versionsFile, setVersionsFile] = useState<FileMetadataDto | null>(null)
  const [shareTarget, setShareTarget] = useState<ShareTarget | null>(null)
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

  const handleDeleteFile = async (file: FileMetadataDto) => {
    if (!confirm(`Move "${file.filename}" to trash?`)) return
    setDeletingId(file.id)
    try {
      await deleteFile(file.id)
      toast(`"${file.filename}" moved to trash`)
      onRefresh()
    } catch (err: unknown) {
      toast(err instanceof Error ? err.message : 'Delete failed', 'error')
    } finally {
      setDeletingId(null)
    }
  }

  const handleDeleteFolder = async (folder: FolderDto) => {
    if (!confirm(`Move folder "${folder.name}" and all its contents to trash?`)) return
    setDeletingId(folder.id)
    try {
      await deleteFolder(folder.id)
      toast(`"${folder.name}" moved to trash`)
      onRefresh()
    } catch (err: unknown) {
      toast(err instanceof Error ? err.message : 'Delete failed', 'error')
    } finally {
      setDeletingId(null)
    }
  }

  const isEmpty = folders.length === 0 && files.length === 0

  if (isEmpty) {
    return (
      <div style={{ textAlign: 'center', padding: '4rem 0', color: '#9ca3af' }}>
        <FileText style={{ margin: '0 auto 0.75rem', display: 'block' }} size={40} />
        <p style={{ margin: 0, fontSize: '0.875rem' }}>This folder is empty. Upload a file or create a subfolder.</p>
      </div>
    )
  }

  return (
    <>
      <div style={{ overflowX: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.875rem' }}>
          <thead>
            <tr>
              <th style={thStyle}>Name</th>
              <th style={thStyle}>Size</th>
              <th style={thStyle}>Type</th>
              <th style={thStyle}>Date</th>
              <th style={{ ...thStyle, textAlign: 'right' }}>Actions</th>
            </tr>
          </thead>
          <tbody>
            {/* Folders first */}
            {folders.map(folder => (
              <tr key={folder.id} style={{ cursor: 'pointer' }}>
                <td
                  style={{ ...tdStyle, fontWeight: 500, color: '#111827', maxWidth: '16rem', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
                  onClick={() => onFolderOpen(folder)}
                >
                  <span style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                    <Folder size={15} color="#f59e0b" style={{ flexShrink: 0 }} />
                    {folder.name}
                    <ChevronRight size={13} color="#9ca3af" style={{ flexShrink: 0 }} />
                  </span>
                </td>
                <td style={{ ...tdStyle, color: '#9ca3af' }}>—</td>
                <td style={tdStyle}>
                  <span style={{ background: '#fef3c7', color: '#92400e', padding: '0.125rem 0.5rem', borderRadius: '0.25rem', fontSize: '0.75rem' }}>
                    folder
                  </span>
                </td>
                <td style={{ ...tdStyle, color: '#6b7280' }}>{formatDate(folder.createdAt)}</td>
                <td style={{ ...tdStyle, textAlign: 'right' }}>
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: '0.25rem' }}>
                    <button
                      onClick={() => setShareTarget({ id: folder.id, name: folder.name, type: 'Folder' })}
                      title="Share"
                      style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '0.375rem', borderRadius: '0.25rem', color: '#6b7280' }}
                    >
                      <Share2 size={15} />
                    </button>
                    <button
                      onClick={() => handleDeleteFolder(folder)}
                      disabled={deletingId === folder.id}
                      title="Move to trash"
                      style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '0.375rem', borderRadius: '0.25rem', color: '#6b7280', opacity: deletingId === folder.id ? 0.5 : 1 }}
                    >
                      {deletingId === folder.id ? <Loader2 size={16} style={{ animation: 'spin 1s linear infinite' }} /> : <FolderX size={16} />}
                    </button>
                  </div>
                </td>
              </tr>
            ))}

            {/* Files */}
            {files.map(file => (
              <tr key={file.id}>
                <td style={{ ...tdStyle, fontWeight: 500, color: '#111827', maxWidth: '16rem', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  <span style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                    <FileText size={14} color="#6b7280" style={{ flexShrink: 0 }} />
                    {file.filename}
                  </span>
                </td>
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
                      onClick={() => setShareTarget({ id: file.id, name: file.filename, type: 'File' })}
                      title="Share"
                      style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '0.375rem', borderRadius: '0.25rem', color: '#6b7280' }}
                    >
                      <Share2 size={15} />
                    </button>
                    <button
                      onClick={() => setVersionsFile(file)}
                      title="Version history"
                      style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '0.375rem', borderRadius: '0.25rem', color: '#6b7280' }}
                    >
                      <Clock size={15} />
                    </button>
                    <button
                      onClick={() => handleDownload(file)}
                      disabled={downloadingId === file.id}
                      title="Download"
                      style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '0.375rem', borderRadius: '0.25rem', color: '#6b7280', opacity: downloadingId === file.id ? 0.5 : 1 }}
                    >
                      {downloadingId === file.id ? <Loader2 size={16} style={{ animation: 'spin 1s linear infinite' }} /> : <Download size={16} />}
                    </button>
                    <button
                      onClick={() => handleDeleteFile(file)}
                      disabled={deletingId === file.id}
                      title="Move to trash"
                      style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '0.375rem', borderRadius: '0.25rem', color: '#6b7280', opacity: deletingId === file.id ? 0.5 : 1 }}
                    >
                      {deletingId === file.id ? <Loader2 size={16} style={{ animation: 'spin 1s linear infinite' }} /> : <Trash2 size={16} />}
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {versionsFile && (
        <VersionsModal
          file={versionsFile}
          onClose={() => setVersionsFile(null)}
          onChanged={onRefresh}
        />
      )}

      {shareTarget && (
        <ShareModal
          resourceId={shareTarget.id}
          resourceType={shareTarget.type}
          resourceName={shareTarget.name}
          onClose={() => setShareTarget(null)}
        />
      )}
    </>
  )
}
