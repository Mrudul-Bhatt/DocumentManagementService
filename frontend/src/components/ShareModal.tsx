import { useState, useEffect } from 'react'
import { X, Trash2, Link, Plus, Copy, Check, Loader2, Globe } from 'lucide-react'
import type { ShareResourceType, ShareRole, ShareDto } from '../api/shares'
import type { PublicLinkDto } from '../api/publicLinks'
import { createShare, revokeShare, listShares } from '../api/shares'
import { createPublicLink, revokePublicLink, listPublicLinks } from '../api/publicLinks'
import { useToast } from './Toast'
import { formatDate } from '../lib/utils'

interface Props {
  resourceId: string
  resourceType: ShareResourceType
  resourceName: string
  onClose: () => void
}

type Tab = 'people' | 'links'

const ROLES: ShareRole[] = ['Viewer', 'Commenter', 'Editor']

const roleColors: Record<ShareRole, { bg: string; color: string }> = {
  Viewer:    { bg: '#f3f4f6', color: '#374151' },
  Commenter: { bg: '#eff6ff', color: '#1d4ed8' },
  Editor:    { bg: '#f0fdf4', color: '#15803d' },
}

const overlayStyle: React.CSSProperties = {
  position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)',
  display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 50,
}

const modalStyle: React.CSSProperties = {
  background: 'white', borderRadius: '0.75rem', width: '100%', maxWidth: '520px',
  maxHeight: '85vh', display: 'flex', flexDirection: 'column',
  boxShadow: '0 20px 40px rgba(0,0,0,0.15)', overflow: 'hidden',
}

export function ShareModal({ resourceId, resourceType, resourceName, onClose }: Props) {
  const [tab, setTab] = useState<Tab>('people')
  const [shares, setShares] = useState<ShareDto[]>([])
  const [links, setLinks] = useState<PublicLinkDto[]>([])
  const [loadingShares, setLoadingShares] = useState(true)
  const [loadingLinks, setLoadingLinks] = useState(true)

  // Invite form state
  const [inviteEmail, setInviteEmail] = useState('')
  const [inviteRole, setInviteRole] = useState<ShareRole>('Viewer')
  const [inviting, setInviting] = useState(false)

  // Public link form state
  const [linkRole, setLinkRole] = useState<ShareRole>('Viewer')
  const [linkExpiry, setLinkExpiry] = useState('')
  const [linkPassword, setLinkPassword] = useState('')
  const [creatingLink, setCreatingLink] = useState(false)

  const [copiedToken, setCopiedToken] = useState<string | null>(null)
  const [revokingId, setRevokingId] = useState<string | null>(null)

  const { toast } = useToast()

  useEffect(() => {
    loadShares()
    loadLinks()
  }, [resourceId])

  async function loadShares() {
    setLoadingShares(true)
    try {
      setShares(await listShares(resourceId, resourceType))
    } catch {
      // non-fatal — owner-only, shared user will get 403
    } finally {
      setLoadingShares(false)
    }
  }

  async function loadLinks() {
    setLoadingLinks(true)
    try {
      setLinks(await listPublicLinks(resourceId, resourceType))
    } catch {
      // non-fatal
    } finally {
      setLoadingLinks(false)
    }
  }

  async function handleInvite() {
    if (!inviteEmail.trim()) return
    setInviting(true)
    try {
      const share = await createShare(resourceId, resourceType, inviteEmail.trim(), inviteRole)
      setShares(prev => [...prev, share])
      setInviteEmail('')
      toast(`Invited ${inviteEmail.trim()} as ${inviteRole}`)
    } catch (err: unknown) {
      toast(err instanceof Error ? err.message : 'Failed to share', 'error')
    } finally {
      setInviting(false)
    }
  }

  async function handleRevokeShare(shareId: string) {
    setRevokingId(shareId)
    try {
      await revokeShare(shareId)
      setShares(prev => prev.filter(s => s.id !== shareId))
      toast('Access revoked')
    } catch (err: unknown) {
      toast(err instanceof Error ? err.message : 'Failed to revoke', 'error')
    } finally {
      setRevokingId(null)
    }
  }

  async function handleCreateLink() {
    setCreatingLink(true)
    try {
      const link = await createPublicLink(
        resourceId,
        resourceType,
        linkRole,
        linkExpiry || null,
        linkPassword || null,
      )
      setLinks(prev => [...prev, link])
      setLinkExpiry('')
      setLinkPassword('')
      toast('Public link created')
    } catch (err: unknown) {
      toast(err instanceof Error ? err.message : 'Failed to create link', 'error')
    } finally {
      setCreatingLink(false)
    }
  }

  async function handleRevokeLink(linkId: string) {
    setRevokingId(linkId)
    try {
      await revokePublicLink(linkId)
      setLinks(prev => prev.filter(l => l.id !== linkId))
      toast('Link revoked')
    } catch (err: unknown) {
      toast(err instanceof Error ? err.message : 'Failed to revoke link', 'error')
    } finally {
      setRevokingId(null)
    }
  }

  function copyLink(token: string) {
    const url = `${window.location.origin}/public/${token}`
    navigator.clipboard.writeText(url).then(() => {
      setCopiedToken(token)
      setTimeout(() => setCopiedToken(null), 2000)
    })
  }

  const tabBtn = (t: Tab): React.CSSProperties => ({
    flex: 1, padding: '0.5rem', border: 'none', cursor: 'pointer', fontSize: '0.8rem',
    fontWeight: tab === t ? 600 : 400,
    background: tab === t ? 'white' : 'transparent',
    color: tab === t ? '#2563eb' : '#6b7280',
    borderRadius: '0.375rem',
    boxShadow: tab === t ? '0 1px 3px rgba(0,0,0,0.1)' : 'none',
  })

  const inputStyle: React.CSSProperties = {
    padding: '0.5rem 0.75rem', border: '1px solid #d1d5db', borderRadius: '0.5rem',
    fontSize: '0.875rem', outline: 'none', background: 'white',
  }

  const selectStyle: React.CSSProperties = {
    ...inputStyle, cursor: 'pointer', flexShrink: 0,
  }

  return (
    <div style={overlayStyle} onClick={onClose}>
      <div style={modalStyle} onClick={e => e.stopPropagation()}>
        {/* Header */}
        <div style={{ padding: '1.25rem 1.5rem 0', borderBottom: '1px solid #f3f4f6' }}>
          <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: '0.875rem' }}>
            <div>
              <h2 style={{ margin: 0, fontSize: '1rem', fontWeight: 600, color: '#111827' }}>Share</h2>
              <p style={{ margin: '0.125rem 0 0', fontSize: '0.8rem', color: '#9ca3af', maxWidth: '340px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                {resourceName}
              </p>
            </div>
            <button
              onClick={onClose}
              style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '0.25rem', color: '#9ca3af' }}
            >
              <X size={18} />
            </button>
          </div>

          {/* Tabs */}
          <div style={{ display: 'flex', gap: '0.25rem', background: '#f3f4f6', borderRadius: '0.5rem', padding: '0.25rem', marginBottom: '1rem' }}>
            <button style={tabBtn('people')} onClick={() => setTab('people')}>
              People
            </button>
            <button style={tabBtn('links')} onClick={() => setTab('links')}>
              <span style={{ display: 'flex', alignItems: 'center', gap: '0.3rem', justifyContent: 'center' }}>
                <Link size={12} />
                Public Links {links.length > 0 && `(${links.length})`}
              </span>
            </button>
          </div>
        </div>

        {/* Body */}
        <div style={{ flex: 1, overflowY: 'auto', padding: '1rem 1.5rem 1.5rem' }}>
          {tab === 'people' && (
            <>
              {/* Invite form */}
              <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1.25rem' }}>
                <input
                  type="email"
                  placeholder="Email address"
                  value={inviteEmail}
                  onChange={e => setInviteEmail(e.target.value)}
                  onKeyDown={e => e.key === 'Enter' && handleInvite()}
                  style={{ ...inputStyle, flex: 1 }}
                />
                <select value={inviteRole} onChange={e => setInviteRole(e.target.value as ShareRole)} style={selectStyle}>
                  {ROLES.map(r => <option key={r} value={r}>{r}</option>)}
                </select>
                <button
                  onClick={handleInvite}
                  disabled={inviting || !inviteEmail.trim()}
                  style={{
                    display: 'flex', alignItems: 'center', gap: '0.3rem',
                    padding: '0.5rem 0.875rem', background: '#2563eb', color: 'white',
                    border: 'none', borderRadius: '0.5rem', fontSize: '0.8rem', cursor: 'pointer',
                    opacity: inviting || !inviteEmail.trim() ? 0.6 : 1, flexShrink: 0,
                  }}
                >
                  {inviting ? <Loader2 size={14} style={{ animation: 'spin 1s linear infinite' }} /> : <Plus size={14} />}
                  Invite
                </button>
              </div>

              {/* Share list */}
              {loadingShares ? (
                <div style={{ display: 'flex', justifyContent: 'center', padding: '2rem 0', color: '#9ca3af' }}>
                  <Loader2 size={18} style={{ animation: 'spin 1s linear infinite' }} />
                </div>
              ) : shares.length === 0 ? (
                <p style={{ margin: 0, fontSize: '0.8rem', color: '#9ca3af', textAlign: 'center', padding: '1.5rem 0' }}>
                  Not shared with anyone yet.
                </p>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                  {shares.map(share => (
                    <div key={share.id} style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', padding: '0.625rem 0.75rem', background: '#f9fafb', borderRadius: '0.5rem' }}>
                      <div style={{ width: 32, height: 32, borderRadius: '50%', background: '#e5e7eb', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '0.8rem', fontWeight: 600, color: '#6b7280', flexShrink: 0 }}>
                        {share.grantedToEmail[0].toUpperCase()}
                      </div>
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <p style={{ margin: 0, fontSize: '0.8rem', fontWeight: 500, color: '#111827', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                          {share.grantedToEmail}
                        </p>
                        <p style={{ margin: '0.1rem 0 0', fontSize: '0.7rem', color: '#9ca3af' }}>
                          Since {formatDate(share.createdAt)}
                        </p>
                      </div>
                      <span style={{ ...roleColors[share.role], padding: '0.15rem 0.5rem', borderRadius: '0.25rem', fontSize: '0.7rem', fontWeight: 500, flexShrink: 0 }}>
                        {share.role}
                      </span>
                      <button
                        onClick={() => handleRevokeShare(share.id)}
                        disabled={revokingId === share.id}
                        title="Revoke access"
                        style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '0.25rem', color: '#9ca3af', opacity: revokingId === share.id ? 0.5 : 1, flexShrink: 0 }}
                      >
                        {revokingId === share.id
                          ? <Loader2 size={14} style={{ animation: 'spin 1s linear infinite' }} />
                          : <Trash2 size={14} />}
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </>
          )}

          {tab === 'links' && (
            <>
              {/* Create link form */}
              <div style={{ background: '#f9fafb', border: '1px solid #e5e7eb', borderRadius: '0.5rem', padding: '1rem', marginBottom: '1.25rem' }}>
                <p style={{ margin: '0 0 0.75rem', fontSize: '0.8rem', fontWeight: 500, color: '#374151', display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                  <Globe size={13} />
                  Create public link
                </p>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                  <div style={{ display: 'flex', gap: '0.5rem' }}>
                    <div style={{ flex: 1 }}>
                      <label style={{ display: 'block', fontSize: '0.7rem', color: '#6b7280', marginBottom: '0.25rem' }}>Role</label>
                      <select value={linkRole} onChange={e => setLinkRole(e.target.value as ShareRole)} style={{ ...selectStyle, width: '100%' }}>
                        {ROLES.map(r => <option key={r} value={r}>{r}</option>)}
                      </select>
                    </div>
                    <div style={{ flex: 1 }}>
                      <label style={{ display: 'block', fontSize: '0.7rem', color: '#6b7280', marginBottom: '0.25rem' }}>Expires (optional)</label>
                      <input
                        type="datetime-local"
                        value={linkExpiry}
                        onChange={e => setLinkExpiry(e.target.value)}
                        style={{ ...inputStyle, width: '100%', boxSizing: 'border-box' }}
                      />
                    </div>
                  </div>
                  <div>
                    <label style={{ display: 'block', fontSize: '0.7rem', color: '#6b7280', marginBottom: '0.25rem' }}>Password (optional)</label>
                    <input
                      type="password"
                      placeholder="Leave blank for no password"
                      value={linkPassword}
                      onChange={e => setLinkPassword(e.target.value)}
                      style={{ ...inputStyle, width: '100%', boxSizing: 'border-box' }}
                    />
                  </div>
                  <button
                    onClick={handleCreateLink}
                    disabled={creatingLink}
                    style={{
                      display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '0.4rem',
                      padding: '0.5rem', background: '#2563eb', color: 'white',
                      border: 'none', borderRadius: '0.5rem', fontSize: '0.8rem', cursor: 'pointer',
                      opacity: creatingLink ? 0.6 : 1,
                    }}
                  >
                    {creatingLink ? <Loader2 size={14} style={{ animation: 'spin 1s linear infinite' }} /> : <Link size={14} />}
                    Generate Link
                  </button>
                </div>
              </div>

              {/* Links list */}
              {loadingLinks ? (
                <div style={{ display: 'flex', justifyContent: 'center', padding: '2rem 0', color: '#9ca3af' }}>
                  <Loader2 size={18} style={{ animation: 'spin 1s linear infinite' }} />
                </div>
              ) : links.length === 0 ? (
                <p style={{ margin: 0, fontSize: '0.8rem', color: '#9ca3af', textAlign: 'center', padding: '1rem 0' }}>
                  No public links yet.
                </p>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                  {links.map(link => (
                    <div key={link.id} style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', padding: '0.625rem 0.75rem', background: '#f9fafb', borderRadius: '0.5rem' }}>
                      <Link size={14} color="#9ca3af" style={{ flexShrink: 0 }} />
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <p style={{ margin: 0, fontSize: '0.75rem', fontFamily: 'monospace', color: '#374151', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                          {link.token.slice(0, 20)}…
                        </p>
                        <p style={{ margin: '0.1rem 0 0', fontSize: '0.7rem', color: '#9ca3af' }}>
                          {link.role}{link.hasPassword ? ' · 🔒 password' : ''}
                          {link.expiresAt ? ` · expires ${formatDate(link.expiresAt)}` : ' · no expiry'}
                        </p>
                      </div>
                      <button
                        onClick={() => copyLink(link.token)}
                        title="Copy link"
                        style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '0.25rem', color: copiedToken === link.token ? '#16a34a' : '#6b7280', flexShrink: 0 }}
                      >
                        {copiedToken === link.token ? <Check size={14} /> : <Copy size={14} />}
                      </button>
                      <button
                        onClick={() => handleRevokeLink(link.id)}
                        disabled={revokingId === link.id}
                        title="Revoke link"
                        style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '0.25rem', color: '#9ca3af', opacity: revokingId === link.id ? 0.5 : 1, flexShrink: 0 }}
                      >
                        {revokingId === link.id
                          ? <Loader2 size={14} style={{ animation: 'spin 1s linear infinite' }} />
                          : <Trash2 size={14} />}
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  )
}
