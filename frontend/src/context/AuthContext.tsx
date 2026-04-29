import {
  createContext, useContext, useState, useEffect, useCallback,
  type ReactNode,
} from 'react'
import {
  setTokens, clearTokens, getStoredRefreshToken, registerLogoutCallback,
} from '../api/client'
import { login as apiLogin, register as apiRegister, revokeToken } from '../api/auth'

interface AuthState {
  isAuthenticated: boolean
  isRestoring: boolean
}

interface AuthContextValue extends AuthState {
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [isAuthenticated, setIsAuthenticated] = useState(false)
  const [isRestoring, setIsRestoring] = useState(true)

  const logout = useCallback(async () => {
    const rt = getStoredRefreshToken()
    if (rt) await revokeToken(rt).catch(() => null)
    clearTokens()
    setIsAuthenticated(false)
  }, [])

  // Register the logout callback so authFetch can trigger it on failed refresh
  useEffect(() => {
    registerLogoutCallback(() => {
      clearTokens()
      setIsAuthenticated(false)
    })
  }, [])

  // On mount: if a refresh token exists in localStorage, try to restore the session
  useEffect(() => {
    async function restoreSession() {
      const rt = getStoredRefreshToken()
      if (!rt) { setIsRestoring(false); return }

      try {
        const res = await fetch('/api/auth/refresh', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ token: rt }),
        })
        if (res.ok) {
          const data = await res.json()
          setTokens(data.accessToken, data.refreshToken)
          setIsAuthenticated(true)
        } else {
          clearTokens()
        }
      } catch {
        clearTokens()
      } finally {
        setIsRestoring(false)
      }
    }
    restoreSession()
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const tokens = await apiLogin(email, password)
    setTokens(tokens.accessToken, tokens.refreshToken)
    setIsAuthenticated(true)
  }, [])

  const register = useCallback(async (email: string, password: string) => {
    const tokens = await apiRegister(email, password)
    setTokens(tokens.accessToken, tokens.refreshToken)
    setIsAuthenticated(true)
  }, [])

  return (
    <AuthContext.Provider value={{ isAuthenticated, isRestoring, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}
