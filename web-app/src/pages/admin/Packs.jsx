import { useEffect, useState } from 'react'
import { useAuth } from '../../auth/AuthContext.jsx'

const RESOURCE_BASE_URL = 'http://localhost:5076'

export default function AdminPacks() {
  const { token } = useAuth()
  const [packs, setPacks] = useState([])
  const [puzzles, setPuzzles] = useState([])
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [published, setPublished] = useState(false)
  const [selectedPuzzles, setSelectedPuzzles] = useState([])
  const [error, setError] = useState('')

  const loadAll = async () => {
    const [packsRes, puzzlesRes] = await Promise.all([
      fetch(`${RESOURCE_BASE_URL}/cms/packs`, { headers: { Authorization: `Bearer ${token}` } }),
      fetch(`${RESOURCE_BASE_URL}/cms/puzzles`, { headers: { Authorization: `Bearer ${token}` } })
    ])

    if (packsRes.ok) setPacks(await packsRes.json())
    if (puzzlesRes.ok) setPuzzles(await puzzlesRes.json())
  }

  useEffect(() => {
    loadAll()
  }, [token])

  const togglePuzzle = (id) => {
    setSelectedPuzzles((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    )
  }

  const createPack = async (e) => {
    e.preventDefault()
    setError('')

    const res = await fetch(`${RESOURCE_BASE_URL}/cms/packs`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`
      },
      body: JSON.stringify({
        name,
        description,
        published,
        puzzleIds: selectedPuzzles
      })
    })

    if (!res.ok) {
      setError('Failed to create pack')
      return
    }

    setName('')
    setDescription('')
    setPublished(false)
    setSelectedPuzzles([])
    await loadAll()
  }

  const togglePublish = async (id) => {
    const res = await fetch(`${RESOURCE_BASE_URL}/cms/packs/${id}/publish`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${token}` }
    })

    if (res.ok) await loadAll()
  }

  const deletePack = async (id) => {
    const res = await fetch(`${RESOURCE_BASE_URL}/cms/packs/${id}`, {
      method: 'DELETE',
      headers: { Authorization: `Bearer ${token}` }
    })

    if (res.ok) await loadAll()
  }

  return (
    <div className="card">
      <h2>Admin Packs</h2>
      {error && <p className="error">{error}</p>}

      <div className="split">
        <form onSubmit={createPack} className="form">
          <label>
            Name
            <input value={name} onChange={(e) => setName(e.target.value)} />
          </label>
          <label>
            Description
            <input value={description} onChange={(e) => setDescription(e.target.value)} />
          </label>
          <label>
            Published
            <input type="checkbox" checked={published} onChange={(e) => setPublished(e.target.checked)} />
          </label>

          <div className="list">
            {puzzles.map((puzzle) => (
              <label key={puzzle.id} className="list-item">
                <input
                  type="checkbox"
                  checked={selectedPuzzles.includes(puzzle.id)}
                  onChange={() => togglePuzzle(puzzle.id)}
                />
                <span>{puzzle.answer}</span>
              </label>
            ))}
          </div>

          <button type="submit">Create pack</button>
        </form>

        <div>
          <h3>Existing packs</h3>
          <div className="list">
            {packs.map((pack) => (
              <div key={pack.id} className="list-item">
                <div>
                  <strong>{pack.name}</strong>
                  <p>{pack.description}</p>
                  <p>{pack.published ? 'Published' : 'Draft'}</p>
                </div>
                <div className="action-row">
                  <button type="button" onClick={() => togglePublish(pack.id)}>
                    Toggle
                  </button>
                  <button type="button" onClick={() => deletePack(pack.id)}>
                    Delete
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}
