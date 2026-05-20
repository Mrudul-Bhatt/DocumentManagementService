import { useState, useEffect } from 'react'
import { Loader2, FileText, Folder, RefreshCw } from 'lucide-react'
import { listSharedWithMe, type SharedResourceDto } from '../api/shares'
import { useToast } from './Toast'
import { formatDate } from '../lib/utils'

const roleColors: Record<string, { bg: string; color: string }> = {
  Viewer:    { bg: '#f3f4f6', color: '#374151' },
  Commenter: { bg: '#eff6ff', color: '#1d4ed8' },
  Editor:    { bg: '#f0fdf4', color: '#15803d' },
}

const tdStyle: React.CSSProperties = {
  padding: '0.75rem 1rem 0.75rem 0', fontSize: '0.875rem',
  borderBottom: '1px solid #f3f4f6', color: '#374151',
}

const thStyle: React.CSSProperties = {
  padding: '0.75rem 1rem 0.75rem 0', fontWeight: 500, fontSize: '0.7rem',
  color: '#6b7280', textTransform: 'uppercase', letterSpacing: '0.05em',
  borderBottom: '1px solid #e5e7eb', textAlign: 'left',
}

export function SharedWithMeView() {
  const [items, setItems] = useState<SharedResourceDto[]>([])
  const [loading, setLoading] = useState(true)
  const { toast } = useToast()

  async function load() {
    setLoading(true)
    try {
      setItems(await listSharedWithMe())
    } catch (err: unknown) {
      toast(err instanceof Error ? err.message : 'Failed to load shared items', 'error')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { load() }, [])

  return (
    <div style={{ background: 'white', borderRadius: '0.75rem', border: '1px solid #e5e7eb', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '1rem 1.5rem', borderBottom: '1px solid #f3f4f6' }}>
        <div>
          <h2 style={{ margin: 0, fontSize: '0.9rem', fontWeight: 600, color: '#111827' }}>Shared with me</h2>
          <p style={{ margin: '0.125rem 0 0', fontSize: '0.75rem', color: '#9ca3af' }}>
            Files and folders others have shared with you
          </p>
        </div>
        <button
          onClick={load}
          title="Refresh"
          style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '0.5rem', color: '#6b7280' }}
        >
          <RefreshCw size={15} />
        </button>
      </div>

      <div style={{ padding: '0.5rem 1.5rem 1rem' }}>
        {loading ? (
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '4rem 0', color: '#9ca3af' }}>
            <Loader2 size={20} style={{ marginRight: '0.5rem', animation: 'spin 1s linear infinite' }} />
            <span style={{ fontSize: '0.875rem' }}>Loading…</span>
          </div>
        ) : items.length === 0 ? (
          <div style={{ textAlign: 'center', padding: '4rem 0', color: '#9ca3af' }}>
            <FileText style={{ margin: '0 auto 0.75rem', display: 'block' }} size={40} />
            <p style={{ margin: 0, fontSize: '0.875rem' }}>Nothing has been shared with you yet.</p>
          </div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr>
                  <th style={thStyle}>Name</th>
                  <th style={thStyle}>Type</th>
                  <th style={thStyle}>Your Role</th>
                  <th style={thStyle}>Shared</th>
                </tr>
              </thead>
              <tbody>
                {items.map(item => (
                  <tr key={item.id}>
                    <td style={{ ...tdStyle, fontWeight: 500, color: '#111827', maxWidth: '20rem', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      <span style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                        {item.resourceType === 'Folder'
                          ? <Folder size={14} color="#f59e0b" style={{ flexShrink: 0 }} />
                          : <FileText size={14} color="#6b7280" style={{ flexShrink: 0 }} />}
                        {item.name}
                      </span>
                    </td>
                    <td style={tdStyle}>
                      <span style={{
                        background: item.resourceType === 'Folder' ? '#fef3c7' : '#f3f4f6',
                        color: item.resourceType === 'Folder' ? '#92400e' : '#6b7280',
                        padding: '0.125rem 0.5rem', borderRadius: '0.25rem', fontSize: '0.75rem',
                      }}>
                        {item.resourceType.toLowerCase()}
                      </span>
                    </td>
                    <td style={tdStyle}>
                      <span style={{ ...roleColors[item.role], padding: '0.15rem 0.5rem', borderRadius: '0.25rem', fontSize: '0.75rem', fontWeight: 500 }}>
                        {item.role}
                      </span>
                    </td>
                    <td style={{ ...tdStyle, color: '#6b7280' }}>
                      {formatDate(item.sharedAt)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  )
}
