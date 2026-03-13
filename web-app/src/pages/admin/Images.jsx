import { useEffect, useState } from 'react'
import { useAuth } from '../../auth/AuthContext.jsx'

const RESOURCE_BASE_URL = 'http://localhost:5076'

export default function AdminImages() {
  const { token } = useAuth()
  const [images, setImages] = useState([])
  const [tags, setTags] = useState([])
  const [url, setUrl] = useState('')
  const [file, setFile] = useState(null)
  const [error, setError] = useState('')
  const [tagInputs, setTagInputs] = useState({})

  const loadImages = async () => {
    const res = await fetch(`${RESOURCE_BASE_URL}/cms/images`, {
      headers: { Authorization: `Bearer ${token}` }
    })
    if (!res.ok) {
      setError('Failed to load images')
      return
    }
    const data = await res.json()
    setImages(data)
  }

  const loadTags = async () => {
    const res = await fetch(`${RESOURCE_BASE_URL}/cms/tags`, {
      headers: { Authorization: `Bearer ${token}` }
    })
    if (res.ok) {
      const data = await res.json()
      setTags(Array.from(data).sort())
    }
  }

  useEffect(() => {
    loadImages()
    loadTags()
  }, [token])

  const submitUrl = async (e) => {
    e.preventDefault()
    setError('')
    const res = await fetch(`${RESOURCE_BASE_URL}/cms/images`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`
      },
      body: JSON.stringify({ url })
    })

    if (!res.ok) {
      setError('Upload failed')
      return
    }

    setUrl('')
    await loadImages()
  }

  const submitFile = async (e) => {
    e.preventDefault()
    if (!file) return
    setError('')

    const form = new FormData()
    form.append('file', file)

    const res = await fetch(`${RESOURCE_BASE_URL}/cms/images`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`
      },
      body: form
    })

    if (!res.ok) {
      setError('File upload failed')
      return
    }

    setFile(null)
    await loadImages()
  }

  const addTag = async (imageId, overrideTag) => {
    const tag = (overrideTag || tagInputs[imageId] || '').trim()
    if (!tag) return

    const res = await fetch(`${RESOURCE_BASE_URL}/cms/images/${imageId}/tags`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`
      },
      body: JSON.stringify({ tag })
    })

    if (res.ok) {
      setTagInputs((prev) => ({ ...prev, [imageId]: '' }))
      await loadImages()
      await loadTags()
    }
  }

  const removeTag = async (imageId, tag) => {
    const res = await fetch(`${RESOURCE_BASE_URL}/cms/images/${imageId}/tags/${tag}`, {
      method: 'DELETE',
      headers: {
        Authorization: `Bearer ${token}`
      }
    })

    if (res.ok) {
      await loadImages()
    }
  }

  return (
    <div className="panel">
      <div className="panel-header">
        <div>
          <h2>Image Library</h2>
          <p className="muted">Upload or link images to power your puzzles.</p>
        </div>
      </div>
      {error && <p className="error">{error}</p>}

      <div className="grid-two">
        <form onSubmit={submitUrl} className="form">
          <label>
            Image URL
            <input value={url} onChange={(e) => setUrl(e.target.value)} />
          </label>
          <button type="submit">Add URL</button>
        </form>

        <form onSubmit={submitFile} className="form">
          <label>
            Upload file
            <input type="file" accept="image/*" onChange={(e) => setFile(e.target.files[0])} />
          </label>
          <button type="submit">Upload file</button>
        </form>
      </div>

      <div className="image-grid">
        {images.map((img) => (
          <div key={img.id} className="image-card">
            <img src={img.url} alt="uploaded" />
            <div className="chip-row">
              {img.tags?.map((tag) => (
                <span key={tag} className="chip">
                  {tag}
                  <button type="button" onClick={() => removeTag(img.id, tag)}>
                    ?
                  </button>
                </span>
              ))}
            </div>
            <div className="tag-add">
              <input
                placeholder="Add tag"
                value={tagInputs[img.id] || ''}
                onChange={(e) =>
                  setTagInputs((prev) => ({ ...prev, [img.id]: e.target.value }))
                }
              />
              <button type="button" onClick={() => addTag(img.id)}>
                Add
              </button>
            </div>
            <div className="tag-suggestions">
              {tags.map((tag) => (
                <button key={tag} type="button" onClick={() => addTag(img.id, tag)}>
                  {tag}
                </button>
              ))}
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
