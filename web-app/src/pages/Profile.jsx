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
    <div className="card">
      <h2>Profile</h2>
      <p>Signed in as {user?.email}</p>
      {progress ? (
        <ul>
          <li>Solved: {progress.solved}</li>
          <li>Attempts: {progress.attempts}</li>
          <li>Score: {progress.score}</li>
        </ul>
      ) : (
        <p>Loading...</p>
      )}
    </div>
  )
}
