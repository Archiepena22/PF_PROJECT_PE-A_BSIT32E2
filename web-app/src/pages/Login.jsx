import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext.jsx'

export default function Login() {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const { login } = useAuth()
  const navigate = useNavigate()

  const onSubmit = async (e) => {
    e.preventDefault()
    setError('')
    try {
      await login({ email, password })
      navigate('/packs')
    } catch (err) {
      setError(err.message)
    }
  }

  return (
    <div className="auth-layout">
      <div className="auth-card">
        <h2>Welcome Back</h2>
        <p className="muted">Log in to continue your puzzle streak.</p>
        <form onSubmit={onSubmit} className="form">
          <label>
            Email
            <input value={email} onChange={(e) => setEmail(e.target.value)} />
          </label>
          <label>
            Password
            <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
          </label>
          {error && <p className="error">{error}</p>}
          <button type="submit">Sign in</button>
        </form>
      </div>
      <div className="auth-side">
        <h3>Daily Brain Teasers</h3>
        <p>Each pack is shuffled, each puzzle is fresh. Can you solve them all?</p>
        <div className="badge-row">
          <span className="badge">Random Packs</span>
          <span className="badge">Smart Hints</span>
          <span className="badge">Admin CMS</span>
        </div>
      </div>
    </div>
  )
}
