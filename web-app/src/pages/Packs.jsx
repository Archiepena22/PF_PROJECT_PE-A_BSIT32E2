import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext.jsx'

const RESOURCE_BASE_URL = 'http://localhost:5076'

export default function Packs() {
  const { token } = useAuth()
  const [packs, setPacks] = useState([])
  const [error, setError] = useState('')

  useEffect(() => {
    const load = async () => {
      setError('')
      const res = await fetch(`${RESOURCE_BASE_URL}/packs?random=true`, {
        headers: { Authorization: `Bearer ${token}` }
      })

      if (!res.ok) {
        setError('Failed to load packs')
        return
      }

      const data = await res.json()
      setPacks(data)
    }

    load()
  }, [token])

  return (
    <div className="panel">
      <div className="panel-header">
        <div>
          <h2>Choose a Pack</h2>
          <p className="muted">Packs are shuffled every time you visit.</p>
        </div>
      </div>
      {error && <p className="error">{error}</p>}
      {!error && packs.length === 0 && <p>No packs yet.</p>}
      <div className="pack-grid">
        {packs.map((pack) => (
          <div key={pack.id} className="pack-card">
            <div>
              <h3>{pack.name}</h3>
              <p>{pack.description}</p>
            </div>
            <Link to={`/play/${pack.id}`} className="cta">
              Play pack
            </Link>
          </div>
        ))}
      </div>
    </div>
  )
}
