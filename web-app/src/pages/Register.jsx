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
    <div className="card">
      <h2>Register</h2>
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
  )
}
