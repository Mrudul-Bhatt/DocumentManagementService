import { useState, useEffect, useCallback } from 'react'
import { Upload, RefreshCw, Loader2, LogOut, FolderPlus, Trash2, Home, ChevronRight, Users } from 'lucide-react'
import { getFolderContents, type FolderDto, type FolderContentsDto } from './api/folders'
import { FileTable } from './components/FileTable'
import { UploadDialog } from './components/UploadDialog'
import { CreateFolderDialog } from './components/CreateFolderDialog'
import { TrashView } from './components/TrashView'
import { SharedWithMeView } from './components/SharedWithMeView'
import { ToastProvider, useToast } from './components/Toast'
import { AuthProvider, useAuth } from './context/AuthContext'
import { AuthPage } from './pages/AuthPage'

type View = 'files' | 'trash' | 'shared'

interface BreadcrumbEntry {
  id: string
  name: string
}

function MainApp() {
  const [view, setView] = useState<View>('files')
  const [contents, setContents] = useState<FolderContentsDto>({ folders: [], files: [] })
  const [loading, setLoading] = useState(false)
  const [showUpload, setShowUpload] = useState(false)
  const [showCreateFolder, setShowCreateFolder] = useState(false)
  // breadcrumb: stack of {id, name} for folders above the current one
  const [breadcrumb, setBreadcrumb] = useState<BreadcrumbEntry[]>([])
  const currentFolderId = breadcrumb.length > 0 ? breadcrumb[breadcrumb.length - 1].id : null
  const { logout } = useAuth()
  const { toast } = useToast()

  const refresh = useCallback(async () => {
    setLoading(true)
    try {
      setContents(await getFolderContents(currentFolderId))
    } catch (err: unknown) {
      toast(err instanceof Error ? err.message : 'Failed to load folder', 'error')
    } finally {
      setLoading(false)
    }
  }, [currentFolderId, toast])

  useEffect(() => { if (view === 'files') refresh() }, [refresh, view])

  const handleFolderOpen = (folder: FolderDto) => {
    setBreadcrumb(prev => [...prev, { id: folder.id, name: folder.name }])
  }

  const handleBreadcrumbNav = (index: number) => {
    // -1 = go to root, 0..n = go to that level
    setBreadcrumb(prev => prev.slice(0, index + 1))
  }

  const handleLogout = async () => {
    await logout()
    toast('Signed out')
  }

  const tabStyle = (active: boolean): React.CSSProperties => ({
    display: 'flex', alignItems: 'center', gap: '0.4rem',
    padding: '0.4rem 0.9rem', borderRadius: '0.4rem', border: 'none',
    cursor: 'pointer', fontSize: '0.875rem', fontWeight: active ? 600 : 400,
    background: active ? '#eff6ff' : 'transparent',
    color: active ? '#2563eb' : '#6b7280',
  })

  const totalItems = contents.folders.length + contents.files.length

  return (
    <div style={{ minHeight: '100vh', background: '#f9fafb' }}>
      <header style={{ background: 'white', borderBottom: '1px solid #e5e7eb', padding: '1rem 1.5rem' }}>
        <div style={{ maxWidth: '64rem', margin: '0 auto', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div>
            <h1 style={{ margin: 0, fontSize: '1.25rem', fontWeight: 700, color: '#111827' }}>Document Management</h1>
            <p style={{ margin: '0.125rem 0 0', fontSize: '0.75rem', color: '#9ca3af' }}>Level 3 — Sharing + Permissions + Public Links</p>
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

      <main style={{ maxWidth: '64rem', margin: '0 auto', padding: '1.5rem' }}>
        {/* View tabs */}
        <div style={{ display: 'flex', gap: '0.25rem', marginBottom: '1.25rem', background: 'white', borderRadius: '0.5rem', border: '1px solid #e5e7eb', padding: '0.25rem', width: 'fit-content' }}>
          <button style={tabStyle(view === 'files')} onClick={() => setView('files')}>
            <Home size={14} />
            Files
          </button>
          <button style={tabStyle(view === 'shared')} onClick={() => setView('shared')}>
            <Users size={14} />
            Shared
          </button>
          <button style={tabStyle(view === 'trash')} onClick={() => setView('trash')}>
            <Trash2 size={14} />
            Trash
          </button>
        </div>

        {view === 'trash' ? (
          <TrashView />
        ) : view === 'shared' ? (
          <SharedWithMeView />
        ) : (
          <div style={{ background: 'white', borderRadius: '0.75rem', border: '1px solid #e5e7eb', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
            {/* Toolbar */}
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '1rem 1.5rem', borderBottom: '1px solid #f3f4f6', flexWrap: 'wrap', gap: '0.5rem' }}>
              {/* Breadcrumb */}
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.25rem', flexWrap: 'wrap' }}>
                <button
                  onClick={() => setBreadcrumb([])}
                  style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '0.25rem 0.5rem', borderRadius: '0.375rem', color: breadcrumb.length === 0 ? '#2563eb' : '#6b7280', fontSize: '0.875rem', fontWeight: breadcrumb.length === 0 ? 600 : 400, display: 'flex', alignItems: 'center', gap: '0.25rem' }}
                >
                  <Home size={13} />
                  Root
                </button>
                {breadcrumb.map((entry, idx) => (
                  <span key={entry.id} style={{ display: 'flex', alignItems: 'center', gap: '0.25rem' }}>
                    <ChevronRight size={12} color="#d1d5db" />
                    <button
                      onClick={() => handleBreadcrumbNav(idx)}
                      style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '0.25rem 0.5rem', borderRadius: '0.375rem', color: idx === breadcrumb.length - 1 ? '#2563eb' : '#6b7280', fontSize: '0.875rem', fontWeight: idx === breadcrumb.length - 1 ? 600 : 400 }}
                    >
                      {entry.name}
                    </button>
                  </span>
                ))}
              </div>

              {/* Actions */}
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <span style={{ fontSize: '0.75rem', color: '#9ca3af' }}>
                  {totalItems} item{totalItems !== 1 ? 's' : ''}
                </span>
                <button
                  onClick={() => setShowCreateFolder(true)}
                  style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', padding: '0.4rem 0.8rem', background: 'white', color: '#374151', border: '1px solid #d1d5db', borderRadius: '0.5rem', fontSize: '0.8rem', cursor: 'pointer' }}
                >
                  <FolderPlus size={14} />
                  New Folder
                </button>
                <button
                  onClick={() => setShowUpload(true)}
                  style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', padding: '0.4rem 0.9rem', background: '#2563eb', color: 'white', border: 'none', borderRadius: '0.5rem', fontSize: '0.8rem', cursor: 'pointer' }}
                >
                  <Upload size={14} />
                  Upload
                </button>
              </div>
            </div>

            {/* Content */}
            <div style={{ padding: '0.5rem 1.5rem 1rem' }}>
              {loading ? (
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '4rem 0', color: '#9ca3af' }}>
                  <Loader2 size={20} style={{ marginRight: '0.5rem', animation: 'spin 1s linear infinite' }} />
                  <span style={{ fontSize: '0.875rem' }}>Loading…</span>
                </div>
              ) : (
                <FileTable
                  folders={contents.folders}
                  files={contents.files}
                  onFolderOpen={handleFolderOpen}
                  onRefresh={refresh}
                />
              )}
            </div>
          </div>
        )}
      </main>

      {showUpload && (
        <UploadDialog
          folderId={currentFolderId}
          onUploaded={refresh}
          onClose={() => setShowUpload(false)}
        />
      )}

      {showCreateFolder && (
        <CreateFolderDialog
          parentFolderId={currentFolderId}
          onCreated={refresh}
          onClose={() => setShowCreateFolder(false)}
        />
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
