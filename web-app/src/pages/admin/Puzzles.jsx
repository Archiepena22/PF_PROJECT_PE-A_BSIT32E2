import { useEffect, useMemo, useState } from 'react'
import { useAuth } from '../../auth/AuthContext.jsx'

const RESOURCE_BASE_URL = 'http://localhost:5076'

export default function AdminPuzzles() {
  const { token } = useAuth()
  const [puzzles, setPuzzles] = useState([])
  const [images, setImages] = useState([])
  const [tags, setTags] = useState([])
  const [selectedTag, setSelectedTag] = useState('')
  const [answer, setAnswer] = useState('')
  const [hint, setHint] = useState('')
  const [difficulty, setDifficulty] = useState('easy')
  const [acceptable, setAcceptable] = useState('')
  const [selectedImages, setSelectedImages] = useState([])
  const [error, setError] = useState('')

  const loadAll = async () => {
    const [puzzlesRes, imagesRes, tagsRes] = await Promise.all([
      fetch(`${RESOURCE_BASE_URL}/cms/puzzles`, { headers: { Authorization: `Bearer ${token}` } }),
      fetch(`${RESOURCE_BASE_URL}/cms/images`, { headers: { Authorization: `Bearer ${token}` } }),
      fetch(`${RESOURCE_BASE_URL}/cms/tags`, { headers: { Authorization: `Bearer ${token}` } })
    ])

    if (puzzlesRes.ok) setPuzzles(await puzzlesRes.json())
    if (imagesRes.ok) setImages(await imagesRes.json())
    if (tagsRes.ok) setTags(Array.from(await tagsRes.json()).sort())
  }

  useEffect(() => {
    loadAll()
  }, [token])

  const filteredImages = useMemo(() => {
    if (!selectedTag) return images
    return images.filter((img) => img.tags?.includes(selectedTag))
  }, [images, selectedTag])

  const toggleImage = (id) => {
    setSelectedImages((prev) => {
      if (prev.includes(id)) return prev.filter((x) => x !== id)
      if (prev.length >= 4) return prev
      return [...prev, id]
    })
  }

  const createPuzzle = async (e) => {
    e.preventDefault()
    setError('')

    const res = await fetch(`${RESOURCE_BASE_URL}/cms/puzzles`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`
      },
      body: JSON.stringify({
        answer,
        hint,
        difficulty,
        imageIds: selectedImages,
        acceptableAnswers: acceptable
          .split(',')
          .map((x) => x.trim())
          .filter(Boolean)
      })
    })

    if (!res.ok) {
      setError('Failed to create puzzle')
      return
    }

    setAnswer('')
    setHint('')
    setDifficulty('easy')
    setAcceptable('')
    setSelectedImages([])
    await loadAll()
  }

  const deletePuzzle = async (id) => {
    const res = await fetch(`${RESOURCE_BASE_URL}/cms/puzzles/${id}`, {
      method: 'DELETE',
      headers: { Authorization: `Bearer ${token}` }
    })

    if (res.ok) await loadAll()
  }

  return (
    <div className="panel">
      <div className="panel-header">
        <div>
          <h2>Puzzle Builder</h2>
          <p className="muted">Choose four images that point to one word.</p>
        </div>
      </div>
      {error && <p className="error">{error}</p>}

      <div className="split">
        <form onSubmit={createPuzzle} className="form">
          <label>
            Answer
            <input value={answer} onChange={(e) => setAnswer(e.target.value)} />
          </label>
          <label>
            Hint
            <input value={hint} onChange={(e) => setHint(e.target.value)} />
          </label>
          <label>
            Difficulty
            <select value={difficulty} onChange={(e) => setDifficulty(e.target.value)}>
              <option value="easy">easy</option>
              <option value="medium">medium</option>
              <option value="hard">hard</option>
            </select>
          </label>
          <label>
            Acceptable answers (comma-separated)
            <input value={acceptable} onChange={(e) => setAcceptable(e.target.value)} />
          </label>

          <label>
            Filter images by tag
            <select value={selectedTag} onChange={(e) => setSelectedTag(e.target.value)}>
              <option value="">All</option>
              {tags.map((tag) => (
                <option key={tag} value={tag}>
                  {tag}
                </option>
              ))}
            </select>
          </label>

          <div className="image-picker">
            {filteredImages.map((img) => (
              <button
                type="button"
                key={img.id}
                className={selectedImages.includes(img.id) ? 'selected' : ''}
                onClick={() => toggleImage(img.id)}
              >
                <img src={img.url} alt="thumb" />
              </button>
            ))}
          </div>
          <p>Selected: {selectedImages.length} / 4</p>
          <button type="submit">Create puzzle</button>
        </form>

        <div>
          <h3>Existing puzzles</h3>
          <div className="list">
            {puzzles.map((puzzle) => (
              <div key={puzzle.id} className="list-item">
                <div>
                  <strong>{puzzle.answer}</strong>
                  <p>{puzzle.difficulty}</p>
                </div>
                <button type="button" onClick={() => deletePuzzle(puzzle.id)}>
                  Delete
                </button>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}
