import { useState, useEffect, useCallback } from 'react'
import { Upload, RefreshCw, Loader2, LogOut } from 'lucide-react'
import { listFiles, type FileMetadataDto } from './api/files'
import { FileTable } from './components/FileTable'
import { UploadDialog } from './components/UploadDialog'
import { ToastProvider, useToast } from './components/Toast'
import { AuthProvider, useAuth } from './context/AuthContext'
import { AuthPage } from './pages/AuthPage'

function MainApp() {
  const [files, setFiles] = useState<FileMetadataDto[]>([])
  const [loading, setLoading] = useState(false)
  const [showUpload, setShowUpload] = useState(false)
  const { logout } = useAuth()
  const { toast } = useToast()

  const refresh = useCallback(async () => {
    setLoading(true)
    try {
      const data = await listFiles()
      setFiles(data)
    } catch (err: unknown) {
      toast(err instanceof Error ? err.message : 'Failed to load files', 'error')
    } finally {
      setLoading(false)
    }
  }, [toast])

  useEffect(() => { refresh() }, [refresh])

  const handleLogout = async () => {
    await logout()
    toast('Signed out')
  }

  return (
    <div style={{ minHeight: '100vh', background: '#f9fafb' }}>
      <header style={{ background: 'white', borderBottom: '1px solid #e5e7eb', padding: '1rem 1.5rem' }}>
        <div style={{ maxWidth: '60rem', margin: '0 auto', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div>
            <h1 style={{ margin: 0, fontSize: '1.25rem', fontWeight: 700, color: '#111827' }}>Document Management</h1>
            <p style={{ margin: '0.125rem 0 0', fontSize: '0.75rem', color: '#9ca3af' }}>Level 1 — Auth + Ownership</p>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <button
              onClick={refresh}
              title="Refresh"
              style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '0.5rem', borderRadius: '0.5rem', color: '#6b7280' }}
            >
              <RefreshCw size={16} />
            </button>
            <button
              onClick={handleLogout}
              title="Sign out"
              style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', background: 'none', border: '1px solid #e5e7eb', cursor: 'pointer', padding: '0.375rem 0.75rem', borderRadius: '0.5rem', color: '#6b7280', fontSize: '0.8rem' }}
            >
              <LogOut size={14} />
              Sign out
            </button>
          </div>
        </div>
      </header>

      <main style={{ maxWidth: '60rem', margin: '0 auto', padding: '2rem 1.5rem' }}>
        <div style={{ background: 'white', borderRadius: '0.75rem', border: '1px solid #e5e7eb', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '1rem 1.5rem', borderBottom: '1px solid #f3f4f6' }}>
            <div>
              <h2 style={{ margin: 0, fontWeight: 600, color: '#111827' }}>My Files</h2>
              <p style={{ margin: '0.125rem 0 0', fontSize: '0.75rem', color: '#9ca3af' }}>
                {files.length} file{files.length !== 1 ? 's' : ''}
              </p>
            </div>
            <button
              onClick={() => setShowUpload(true)}
              style={{
                display: 'flex', alignItems: 'center', gap: '0.5rem',
                padding: '0.5rem 1rem', background: '#2563eb',
                color: 'white', border: 'none', borderRadius: '0.5rem',
                fontSize: '0.875rem', cursor: 'pointer',
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
              <FileTable files={files} onDeleted={refresh} />
            )}
          </div>
        </div>
      </main>

      {showUpload && (
        <UploadDialog onUploaded={refresh} onClose={() => setShowUpload(false)} />
      )}
    </div>
  )
}

function AppContent() {
  const { isAuthenticated, isRestoring } = useAuth()

  if (isRestoring) {
    return (
      <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', background: '#f9fafb' }}>
        <Loader2 size={24} color="#9ca3af" style={{ animation: 'spin 1s linear infinite' }} />
      </div>
    )
  }

  return isAuthenticated ? <MainApp /> : <AuthPage />
}

export default function App() {
  return (
    <ToastProvider>
      <AuthProvider>
        <AppContent />
      </AuthProvider>
    </ToastProvider>
  )
}
