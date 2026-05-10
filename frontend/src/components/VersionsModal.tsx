import { useState, useEffect } from 'react'
import { X, RotateCcw, Trash2, Loader2, Clock } from 'lucide-react'
import type { FileMetadataDto, FileVersionDto } from '../api/files'
import { listVersions, restoreVersion, deleteVersion } from '../api/files'
import { useToast } from './Toast'
import { formatBytes, formatDate } from '../lib/utils'

interface Props {
  file: FileMetadataDto
  onClose: () => void
  onChanged: () => void
}

export function VersionsModal({ file, onClose, onChanged }: Props) {
  const [versions, setVersions] = useState<FileVersionDto[]>([])
  const [loading, setLoading] = useState(true)
  const [actionId, setActionId] = useState<string | null>(null)
  const { toast } = useToast()

  const load = async () => {
    setLoading(true)
    try {
      setVersions(await listVersions(file.id))
    } catch (err: unknown) {
      toast(err instanceof Error ? err.message : 'Failed to load versions', 'error')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { load() }, [])

  const handleRestore = async (v: FileVersionDto) => {
    setActionId(v.id)
    try {
      await restoreVersion(file.id, v.id)
      toast(`Restored to version ${v.versionNumber}`)
      onChanged()
      onClose()
    } catch (err: unknown) {
      toast(err instanceof Error ? err.message : 'Restore failed', 'error')
    } finally {
      setActionId(null)
    }
  }

  const handleDelete = async (v: FileVersionDto) => {
    if (!confirm(`Delete version ${v.versionNumber}? This cannot be undone.`)) return
    setActionId(v.id)
    try {
      await deleteVersion(file.id, v.id)
      toast(`Version ${v.versionNumber} deleted`)
      await load()
    } catch (err: unknown) {
      toast(err instanceof Error ? err.message : 'Delete failed', 'error')
    } finally {
      setActionId(null)
    }
  }

  const overlay: React.CSSProperties = {
    position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.5)',
    display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 40,
  }
  const dialog: React.CSSProperties = {
    background: 'white', borderRadius: '0.75rem', boxShadow: '0 20px 60px rgba(0,0,0,0.2)',
    width: '100%', maxWidth: '32rem', maxHeight: '80vh', display: 'flex', flexDirection: 'column',
  }

  return (
    <div style={overlay} onClick={e => { if (e.target === e.currentTarget) onClose() }}>
      <div style={dialog}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '1.25rem 1.5rem', borderBottom: '1px solid #e5e7eb' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Clock size={16} color="#6b7280" />
            <div>
              <h2 style={{ margin: 0, fontSize: '1rem', fontWeight: 600, color: '#111827' }}>Version History</h2>
              <p style={{ margin: 0, fontSize: '0.75rem', color: '#9ca3af' }}>{file.filename}</p>
            </div>
          </div>
          <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#9ca3af' }}>
            <X size={18} />
          </button>
        </div>

        <div style={{ overflowY: 'auto', flex: 1, padding: '0.5rem 0' }}>
          {loading ? (
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '3rem', color: '#9ca3af' }}>
              <Loader2 size={20} style={{ animation: 'spin 1s linear infinite', marginRight: '0.5rem' }} />
              <span style={{ fontSize: '0.875rem' }}>Loading versions…</span>
            </div>
          ) : versions.length === 0 ? (
            <p style={{ textAlign: 'center', color: '#9ca3af', fontSize: '0.875rem', padding: '3rem' }}>No version history.</p>
          ) : (
            versions.map((v, idx) => (
              <div key={v.id} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '0.75rem 1.5rem', borderBottom: idx < versions.length - 1 ? '1px solid #f3f4f6' : 'none' }}>
                <div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                    <span style={{ fontWeight: 600, fontSize: '0.875rem', color: '#111827' }}>v{v.versionNumber}</span>
                    {idx === 0 && (
                      <span style={{ fontSize: '0.65rem', background: '#dbeafe', color: '#1d4ed8', padding: '0.1rem 0.4rem', borderRadius: '0.25rem', fontWeight: 500 }}>current</span>
                    )}
                  </div>
                  <p style={{ margin: '0.1rem 0 0', fontSize: '0.75rem', color: '#6b7280' }}>
                    {formatBytes(v.fileSize)} · {formatDate(v.createdAt)}
                  </p>
                </div>
                <div style={{ display: 'flex', gap: '0.25rem' }}>
                  {idx !== 0 && (
                    <button
                      onClick={() => handleRestore(v)}
                      disabled={actionId === v.id}
                      title="Restore this version"
                      style={{ background: 'none', border: '1px solid #d1d5db', cursor: 'pointer', padding: '0.3rem 0.6rem', borderRadius: '0.375rem', color: '#2563eb', fontSize: '0.75rem', display: 'flex', alignItems: 'center', gap: '0.25rem', opacity: actionId === v.id ? 0.5 : 1 }}
                    >
                      <RotateCcw size={12} /> Restore
                    </button>
                  )}
                  {idx !== 0 && (
                    <button
                      onClick={() => handleDelete(v)}
                      disabled={actionId === v.id}
                      title="Delete this version"
                      style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '0.375rem', borderRadius: '0.25rem', color: '#9ca3af', opacity: actionId === v.id ? 0.5 : 1 }}
                    >
                      {actionId === v.id ? <Loader2 size={14} style={{ animation: 'spin 1s linear infinite' }} /> : <Trash2 size={14} />}
                    </button>
                  )}
                </div>
              </div>
            ))
          )}
        </div>
      </div>
    </div>
  )
}
