import { createContext, useContext, useMemo, useState } from 'react'

const AuthContext = createContext(null)

const AUTH_BASE_URL = 'http://localhost:5139'

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [token, setToken] = useState(null)

  const login = async ({ email, password }) => {
    const res = await fetch(`${AUTH_BASE_URL}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password })
    })

    if (!res.ok) {
      throw new Error('Invalid credentials')
    }

    const data = await res.json()
    setUser(data.user)
    setToken(data.token)
  }

  const register = async ({ email, password, role }) => {
    const res = await fetch(`${AUTH_BASE_URL}/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password, role })
    })

    if (!res.ok) {
      throw new Error('Registration failed')
    }
  }

  const logout = () => {
    setUser(null)
    setToken(null)
  }

  const value = useMemo(() => ({ user, token, login, register, logout }), [user, token])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}
