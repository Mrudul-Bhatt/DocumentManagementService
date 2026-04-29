import { useRef, useState } from 'react'
import { Upload, X } from 'lucide-react'
import { uploadFile } from '../api/files'
import { useToast } from './Toast'
import { formatBytes } from '../lib/utils'

interface Props {
  onUploaded: () => void
  onClose: () => void
}

export function UploadDialog({ onUploaded, onClose }: Props) {
  const [file, setFile] = useState<File | null>(null)
  const [loading, setLoading] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)
  const { toast } = useToast()

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault()
    const dropped = e.dataTransfer.files[0]
    if (dropped) setFile(dropped)
  }

  const handleSubmit = async () => {
    if (!file) return
    setLoading(true)
    try {
      await uploadFile(file)
      toast(`"${file.name}" uploaded successfully`)
      onUploaded()
      onClose()
    } catch (err: unknown) {
      toast(err instanceof Error ? err.message : 'Upload failed', 'error')
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
    width: '100%', maxWidth: '28rem', padding: '1.5rem',
  }
  const dropzone: React.CSSProperties = {
    border: '2px dashed #d1d5db', borderRadius: '0.5rem', padding: '2rem',
    textAlign: 'center', cursor: 'pointer', transition: 'all 0.2s',
  }

  return (
    <div style={overlay}>
      <div style={dialog}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1.25rem' }}>
          <h2 style={{ margin: 0, fontSize: '1.125rem', fontWeight: 600, color: '#111827' }}>Upload File</h2>
          <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#9ca3af' }}>
            <X size={20} />
          </button>
        </div>

        <div style={dropzone} onClick={() => inputRef.current?.click()} onDrop={handleDrop} onDragOver={e => e.preventDefault()}>
          <Upload style={{ margin: '0 auto 0.5rem', color: '#9ca3af', display: 'block' }} size={32} />
          {file ? (
            <div>
              <p style={{ margin: '0 0 0.25rem', fontWeight: 500, color: '#1f2937' }}>{file.name}</p>
              <p style={{ margin: 0, fontSize: '0.875rem', color: '#6b7280' }}>{formatBytes(file.size)}</p>
            </div>
          ) : (
            <p style={{ margin: 0, color: '#6b7280', fontSize: '0.875rem' }}>Click to browse or drag & drop (max 25 MB)</p>
          )}
          <input ref={inputRef} type="file" style={{ display: 'none' }} onChange={e => setFile(e.target.files?.[0] ?? null)} />
        </div>

        <div style={{ display: 'flex', gap: '0.75rem', marginTop: '1.25rem' }}>
          <button
            onClick={onClose}
            style={{ flex: 1, padding: '0.5rem 1rem', border: '1px solid #d1d5db', borderRadius: '0.5rem', background: 'white', color: '#374151', cursor: 'pointer' }}
          >
            Cancel
          </button>
          <button
            onClick={handleSubmit}
            disabled={!file || loading}
            style={{
              flex: 1, padding: '0.5rem 1rem', borderRadius: '0.5rem', border: 'none',
              background: (!file || loading) ? '#93c5fd' : '#2563eb', color: 'white',
              cursor: (!file || loading) ? 'not-allowed' : 'pointer',
            }}
          >
            {loading ? 'Uploading…' : 'Upload'}
          </button>
        </div>
      </div>
    </div>
  )
}
