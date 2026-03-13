import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext.jsx'

export default function Register() {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] = useState('player')
  const [error, setError] = useState('')
  const { register } = useAuth()
  const navigate = useNavigate()

  const onSubmit = async (e) => {
    e.preventDefault()
    setError('')
    try {
      await register({ email, password, role })
      navigate('/login')
    } catch (err) {
      setError(err.message)
    }
  }

  return (
    <div className="auth-layout">
      <div className="auth-card">
        <h2>Create Account</h2>
        <p className="muted">Start collecting solved puzzles and streaks.</p>
        <form onSubmit={onSubmit} className="form">
          <label>
            Email
            <input value={email} onChange={(e) => setEmail(e.target.value)} />
          </label>
          <label>
            Password
            <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
          </label>
          <label>
            Role
            <select value={role} onChange={(e) => setRole(e.target.value)}>
              <option value="player">player</option>
              <option value="admin">admin</option>
            </select>
          </label>
          {error && <p className="error">{error}</p>}
          <button type="submit">Create account</button>
        </form>
      </div>
      <div className="auth-side">
        <h3>Make Your Own Packs</h3>
        <p>Admins can upload images, tag them, and assemble custom challenges.</p>
        <div className="badge-row">
          <span className="badge">Image Tags</span>
          <span className="badge">Publish Packs</span>
          <span className="badge">Role-based</span>
        </div>
      </div>
    </div>
  )
}
