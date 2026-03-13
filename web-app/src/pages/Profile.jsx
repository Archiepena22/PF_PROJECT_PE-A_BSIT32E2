import { useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthContext.jsx'

const RESOURCE_BASE_URL = 'http://localhost:5076'

export default function Profile() {
  const { token, user } = useAuth()
  const [progress, setProgress] = useState(null)

  useEffect(() => {
    const load = async () => {
      const res = await fetch(`${RESOURCE_BASE_URL}/profile/progress`, {
        headers: { Authorization: `Bearer ${token}` }
      })

      if (!res.ok) return
      const data = await res.json()
      setProgress(data)
    }

    load()
  }, [token])

  return (
    <div className="panel">
      <div className="panel-header">
        <div>
          <h2>Your Progress</h2>
          <p className="muted">Track solved puzzles and score.</p>
        </div>
      </div>
      <div className="stat-grid">
        <div className="stat-card">
          <h3>{progress?.solved ?? '-'}</h3>
          <p>Solved</p>
        </div>
        <div className="stat-card">
          <h3>{progress?.attempts ?? '-'}</h3>
          <p>Attempts</p>
        </div>
        <div className="stat-card">
          <h3>{progress?.score ?? '-'}</h3>
          <p>Score</p>
        </div>
      </div>
      <p className="muted">Signed in as {user?.email}</p>
    </div>
  )
}
