import { useState, useEffect, useCallback } from 'react'
import { Upload, RefreshCw, Loader2 } from 'lucide-react'
import { listFiles, type FileMetadataDto } from './api/files'
import { FileTable } from './components/FileTable'
import { UploadDialog } from './components/UploadDialog'
import { ToastProvider, useToast } from './components/Toast'

function AppContent() {
  const [userId, setUserId] = useState('alice')
  const [files, setFiles] = useState<FileMetadataDto[]>([])
  const [loading, setLoading] = useState(false)
  const [showUpload, setShowUpload] = useState(false)
  const { toast } = useToast()

  const refresh = useCallback(async () => {
    if (!userId.trim()) return
    setLoading(true)
    try {
      const data = await listFiles(userId.trim())
      setFiles(data)
    } catch (err: unknown) {
      toast(err instanceof Error ? err.message : 'Failed to load files', 'error')
    } finally {
      setLoading(false)
    }
  }, [userId, toast])

  useEffect(() => { refresh() }, [refresh])

  return (
    <div style={{ minHeight: '100vh', background: '#f9fafb' }}>
      <header style={{ background: 'white', borderBottom: '1px solid #e5e7eb', padding: '1rem 1.5rem' }}>
        <div style={{ maxWidth: '60rem', margin: '0 auto', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div>
            <h1 style={{ margin: 0, fontSize: '1.25rem', fontWeight: 700, color: '#111827' }}>Document Management</h1>
            <p style={{ margin: '0.125rem 0 0', fontSize: '0.75rem', color: '#9ca3af' }}>Level 0 — Flat File Storage</p>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', background: '#f3f4f6', borderRadius: '0.5rem', padding: '0.375rem 0.75rem' }}>
              <span style={{ fontSize: '0.75rem', color: '#6b7280', fontWeight: 500 }}>User ID:</span>
              <input
                type="text"
                value={userId}
                onChange={e => setUserId(e.target.value)}
                onBlur={refresh}
                style={{ background: 'transparent', border: 'none', outline: 'none', fontSize: '0.875rem', color: '#111827', width: '7rem' }}
                placeholder="Enter user ID"
              />
            </div>
            <button
              onClick={refresh}
              title="Refresh"
              style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '0.5rem', borderRadius: '0.5rem', color: '#6b7280' }}
            >
              <RefreshCw size={16} />
            </button>
          </div>
        </div>
      </header>

      <main style={{ maxWidth: '60rem', margin: '0 auto', padding: '2rem 1.5rem' }}>
        <div style={{ background: 'white', borderRadius: '0.75rem', border: '1px solid #e5e7eb', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '1rem 1.5rem', borderBottom: '1px solid #f3f4f6' }}>
            <div>
              <h2 style={{ margin: 0, fontWeight: 600, color: '#111827' }}>Files</h2>
              <p style={{ margin: '0.125rem 0 0', fontSize: '0.75rem', color: '#9ca3af' }}>
                {files.length} file{files.length !== 1 ? 's' : ''}
              </p>
            </div>
            <button
              onClick={() => setShowUpload(true)}
              disabled={!userId.trim()}
              style={{
                display: 'flex', alignItems: 'center', gap: '0.5rem',
                padding: '0.5rem 1rem', background: !userId.trim() ? '#93c5fd' : '#2563eb',
                color: 'white', border: 'none', borderRadius: '0.5rem',
                fontSize: '0.875rem', cursor: !userId.trim() ? 'not-allowed' : 'pointer',
              }}
            >
              <Upload size={15} />
              Upload
            </button>
          </div>

          <div style={{ padding: '0.5rem 1.5rem 1rem' }}>
            {loading ? (
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '4rem 0', color: '#9ca3af' }}>
                <Loader2 size={20} style={{ marginRight: '0.5rem' }} />
                <span style={{ fontSize: '0.875rem' }}>Loading files…</span>
              </div>
            ) : (
              <FileTable files={files} userId={userId} onDeleted={refresh} />
            )}
          </div>
        </div>
      </main>

      {showUpload && (
        <UploadDialog userId={userId} onUploaded={refresh} onClose={() => setShowUpload(false)} />
      )}
    </div>
  )
}

export default function App() {
  return (
    <ToastProvider>
      <AppContent />
    </ToastProvider>
  )
}
