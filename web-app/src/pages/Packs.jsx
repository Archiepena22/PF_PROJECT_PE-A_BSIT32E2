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
    <div className="card">
      <h2>Packs</h2>
      {error && <p className="error">{error}</p>}
      {!error && packs.length === 0 && <p>No packs yet.</p>}
      <div className="list">
        {packs.map((pack) => (
          <div key={pack.id} className="list-item">
            <div>
              <strong>{pack.name}</strong>
              <p>{pack.description}</p>
            </div>
            <Link to={`/play/${pack.id}`}>Play</Link>
          </div>
        ))}
      </div>
    </div>
  )
}
