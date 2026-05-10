import { useState, useEffect, useCallback } from 'react'
import { Trash2, RotateCcw, FileText, Folder, Loader2, AlertTriangle } from 'lucide-react'
import type { TrashedItemDto } from '../api/trash'
import { listTrash, restoreFromTrash, emptyTrash } from '../api/trash'
import { useToast } from './Toast'
import { formatDate } from '../lib/utils'

export function TrashView() {
  const [items, setItems] = useState<TrashedItemDto[]>([])
  const [loading, setLoading] = useState(false)
  const [actionId, setActionId] = useState<string | null>(null)
  const [emptyingTrash, setEmptyingTrash] = useState(false)
  const { toast } = useToast()

  const refresh = useCallback(async () => {
    setLoading(true)
    try {
      setItems(await listTrash())
    } catch (err: unknown) {
      toast(err instanceof Error ? err.message : 'Failed to load trash', 'error')
    } finally {
      setLoading(false)
    }
  }, [toast])

  useEffect(() => { refresh() }, [refresh])

  const handleRestore = async (item: TrashedItemDto) => {
    setActionId(item.id)
    try {
      await restoreFromTrash(item.id, item.type)
      toast(`"${item.name}" restored`)
      await refresh()
    } catch (err: unknown) {
      toast(err instanceof Error ? err.message : 'Restore failed', 'error')
    } finally {
      setActionId(null)
    }
  }

  const handleEmptyTrash = async () => {
    if (!confirm(`Permanently delete all ${items.length} item${items.length !== 1 ? 's' : ''} in trash? This cannot be undone.`)) return
    setEmptyingTrash(true)
    try {
      await emptyTrash()
      toast('Trash emptied')
      setItems([])
    } catch (err: unknown) {
      toast(err instanceof Error ? err.message : 'Failed to empty trash', 'error')
    } finally {
      setEmptyingTrash(false)
    }
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

  return (
    <div style={{ background: 'white', borderRadius: '0.75rem', border: '1px solid #e5e7eb', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '1rem 1.5rem', borderBottom: '1px solid #f3f4f6' }}>
        <div>
          <h2 style={{ margin: 0, fontWeight: 600, color: '#111827', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Trash2 size={16} />
            Trash
          </h2>
          <p style={{ margin: '0.125rem 0 0', fontSize: '0.75rem', color: '#9ca3af' }}>
            Items are permanently deleted after 30 days
          </p>
        </div>
        {items.length > 0 && (
          <button
            onClick={handleEmptyTrash}
            disabled={emptyingTrash}
            style={{
              display: 'flex', alignItems: 'center', gap: '0.5rem',
              padding: '0.5rem 1rem', background: 'white',
              color: '#dc2626', border: '1px solid #fca5a5', borderRadius: '0.5rem',
              fontSize: '0.875rem', cursor: emptyingTrash ? 'not-allowed' : 'pointer',
              opacity: emptyingTrash ? 0.6 : 1,
            }}
          >
            {emptyingTrash ? <Loader2 size={14} style={{ animation: 'spin 1s linear infinite' }} /> : <AlertTriangle size={14} />}
            Empty Trash
          </button>
        )}
      </div>

      <div style={{ padding: '0.5rem 1.5rem 1rem' }}>
        {loading ? (
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '4rem 0', color: '#9ca3af' }}>
            <Loader2 size={20} style={{ marginRight: '0.5rem', animation: 'spin 1s linear infinite' }} />
            <span style={{ fontSize: '0.875rem' }}>Loading…</span>
          </div>
        ) : items.length === 0 ? (
          <div style={{ textAlign: 'center', padding: '4rem 0', color: '#9ca3af' }}>
            <Trash2 style={{ margin: '0 auto 0.75rem', display: 'block' }} size={40} />
            <p style={{ margin: 0, fontSize: '0.875rem' }}>Trash is empty.</p>
          </div>
        ) : (
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr>
                <th style={thStyle}>Name</th>
                <th style={thStyle}>Type</th>
                <th style={thStyle}>Deleted</th>
                <th style={{ ...thStyle, textAlign: 'right' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {items.map(item => (
                <tr key={item.id}>
                  <td style={{ ...tdStyle, fontWeight: 500, color: '#111827', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                    {item.type === 'Folder'
                      ? <Folder size={14} color="#f59e0b" />
                      : <FileText size={14} color="#6b7280" />
                    }
                    <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', maxWidth: '20rem' }}>{item.name}</span>
                  </td>
                  <td style={tdStyle}>
                    <span style={{ background: '#f3f4f6', color: '#6b7280', padding: '0.125rem 0.5rem', borderRadius: '0.25rem', fontSize: '0.75rem' }}>
                      {item.type}
                    </span>
                  </td>
                  <td style={{ ...tdStyle, color: '#6b7280' }}>{formatDate(item.deletedAt)}</td>
                  <td style={{ ...tdStyle, textAlign: 'right' }}>
                    <button
                      onClick={() => handleRestore(item)}
                      disabled={actionId === item.id}
                      title="Restore"
                      style={{
                        display: 'inline-flex', alignItems: 'center', gap: '0.25rem',
                        background: 'none', border: '1px solid #d1d5db', cursor: actionId === item.id ? 'not-allowed' : 'pointer',
                        padding: '0.3rem 0.6rem', borderRadius: '0.375rem',
                        color: '#2563eb', fontSize: '0.75rem', opacity: actionId === item.id ? 0.5 : 1,
                      }}
                    >
                      {actionId === item.id
                        ? <Loader2 size={12} style={{ animation: 'spin 1s linear infinite' }} />
                        : <RotateCcw size={12} />
                      }
                      Restore
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  )
}
