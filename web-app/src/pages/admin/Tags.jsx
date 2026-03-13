import { useEffect, useState } from 'react'
import { useAuth } from '../../auth/AuthContext.jsx'

const RESOURCE_BASE_URL = 'http://localhost:5076'

export default function AdminTags() {
  const { token } = useAuth()
  const [tags, setTags] = useState([])
  const [newTag, setNewTag] = useState('')
  const [error, setError] = useState('')

  const loadTags = async () => {
    const res = await fetch(`${RESOURCE_BASE_URL}/cms/tags`, {
      headers: { Authorization: `Bearer ${token}` }
    })

    if (!res.ok) {
      setError('Failed to load tags')
      return
    }

    const data = await res.json()
    setTags(Array.from(data).sort())
  }

  useEffect(() => {
    loadTags()
  }, [token])

  const addTag = async (e) => {
    e.preventDefault()
    setError('')

    const res = await fetch(`${RESOURCE_BASE_URL}/cms/tags`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`
      },
      body: JSON.stringify({ tag: newTag })
    })

    if (!res.ok) {
      setError('Failed to add tag')
      return
    }

    setNewTag('')
    await loadTags()
  }

  const removeTag = async (tag) => {
    const res = await fetch(`${RESOURCE_BASE_URL}/cms/tags`, {
      method: 'DELETE',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`
      },
      body: JSON.stringify({ tag })
    })

    if (res.ok) {
      await loadTags()
    }
  }

  return (
    <div className="card">
      <h2>Admin Tags</h2>
      {error && <p className="error">{error}</p>}
      <form onSubmit={addTag} className="form">
        <label>
          New tag
          <input value={newTag} onChange={(e) => setNewTag(e.target.value)} />
        </label>
        <button type="submit">Add tag</button>
      </form>
      <div className="chip-row">
        {tags.map((tag) => (
          <span key={tag} className="chip">
            {tag}
            <button type="button" onClick={() => removeTag(tag)}>
              ?
            </button>
          </span>
        ))}
      </div>
    </div>
  )
}
