import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext.jsx'

const RESOURCE_BASE_URL = 'http://localhost:5076'

export default function Play() {
  const { packId } = useParams()
  const { token } = useAuth()
  const [puzzle, setPuzzle] = useState(null)
  const [guess, setGuess] = useState('')
  const [feedback, setFeedback] = useState('')

  const loadNext = async () => {
    const res = await fetch(`${RESOURCE_BASE_URL}/puzzles/next?packId=${packId}`, {
      headers: { Authorization: `Bearer ${token}` }
    })

    if (!res.ok) {
      setFeedback('Failed to load puzzle')
      return
    }

    const data = await res.json()
    if (data.completed) {
      setPuzzle(null)
      setFeedback('Pack completed!')
      return
    }

    setPuzzle(data)
    setGuess('')
    setFeedback('')
  }

  useEffect(() => {
    loadNext()
  }, [packId])

  const submitGuess = async (e) => {
    e.preventDefault()
    setFeedback('')

    const res = await fetch(`${RESOURCE_BASE_URL}/game/submit`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`
      },
      body: JSON.stringify({ puzzleId: puzzle.puzzleId, guess })
    })

    if (!res.ok) {
      setFeedback('Submit failed')
      return
    }

    const data = await res.json()
    setFeedback(data.correct ? 'Correct! Next puzzle loading...' : 'Try again')

    if (data.correct && data.nextAvailable) {
      await loadNext()
    }
  }

  return (
    <div className="panel">
      <div className="panel-header">
        <div>
          <h2>Guess the Word</h2>
          <p className="muted">Every puzzle has one answer. No repeats.</p>
        </div>
      </div>
      {feedback && <p className="note">{feedback}</p>}
      {!puzzle && !feedback && <p>Loading...</p>}
      {puzzle && (
        <>
          <div className="image-grid-large">
            {puzzle.images.map((url) => (
              <img key={url} src={url} alt="puzzle" />
            ))}
          </div>
          <form onSubmit={submitGuess} className="guess-form">
            <input
              placeholder="Type your answer"
              value={guess}
              onChange={(e) => setGuess(e.target.value)}
            />
            <button type="submit">Submit</button>
          </form>
        </>
      )}
    </div>
  )
}
