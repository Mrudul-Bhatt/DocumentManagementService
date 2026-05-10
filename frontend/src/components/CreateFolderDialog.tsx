import { useState } from 'react'
import { FolderPlus, X } from 'lucide-react'
import { createFolder } from '../api/folders'
import { useToast } from './Toast'

interface Props {
  parentFolderId: string | null
  onCreated: () => void
  onClose: () => void
}

export function CreateFolderDialog({ parentFolderId, onCreated, onClose }: Props) {
  const [name, setName] = useState('')
  const [loading, setLoading] = useState(false)
  const { toast } = useToast()

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!name.trim()) return
    setLoading(true)
    try {
      await createFolder(name.trim(), parentFolderId)
      toast(`Folder "${name.trim()}" created`)
      onCreated()
      onClose()
    } catch (err: unknown) {
      toast(err instanceof Error ? err.message : 'Failed to create folder', 'error')
    } finally {
      setLoading(false)
    }
  }

  const overlay: React.CSSProperties = {
    position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.5)',
    display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 40,
  }
  const dialog: React.CSSProperties = {
    background: 'white', borderRadius: '0.75rem', boxShadow: '0 20px 60px rgba(0,0,0,0.2)',
    width: '100%', maxWidth: '22rem', padding: '1.5rem',
  }

  return (
    <div style={overlay} onClick={e => { if (e.target === e.currentTarget) onClose() }}>
      <div style={dialog}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1.25rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <FolderPlus size={18} color="#2563eb" />
            <h2 style={{ margin: 0, fontSize: '1rem', fontWeight: 600, color: '#111827' }}>New Folder</h2>
          </div>
          <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#9ca3af' }}>
            <X size={18} />
          </button>
        </div>

        <form onSubmit={handleSubmit}>
          <input
            autoFocus
            type="text"
            value={name}
            onChange={e => setName(e.target.value)}
            placeholder="Folder name"
            maxLength={255}
            style={{
              width: '100%', padding: '0.5rem 0.75rem', border: '1px solid #d1d5db',
              borderRadius: '0.5rem', fontSize: '0.875rem', boxSizing: 'border-box',
              outline: 'none',
            }}
          />
          <div style={{ display: 'flex', gap: '0.75rem', marginTop: '1rem' }}>
            <button
              type="button"
              onClick={onClose}
              style={{ flex: 1, padding: '0.5rem', border: '1px solid #d1d5db', borderRadius: '0.5rem', background: 'white', color: '#374151', cursor: 'pointer', fontSize: '0.875rem' }}
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={!name.trim() || loading}
              style={{
                flex: 1, padding: '0.5rem', borderRadius: '0.5rem', border: 'none',
                background: (!name.trim() || loading) ? '#93c5fd' : '#2563eb',
                color: 'white', cursor: (!name.trim() || loading) ? 'not-allowed' : 'pointer',
                fontSize: '0.875rem',
              }}
            >
              {loading ? 'Creating…' : 'Create'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
