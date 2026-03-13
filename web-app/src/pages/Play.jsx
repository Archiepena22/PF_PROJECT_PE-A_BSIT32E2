import { useEffect, useMemo, useState } from 'react'
import { useParams } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext.jsx'

const RESOURCE_BASE_URL = 'http://localhost:5076'

const normalize = (value) => value.replace(/\s+/g, '').toLowerCase()

export default function Play() {
  const { packId } = useParams()
  const { token } = useAuth()
  const [puzzle, setPuzzle] = useState(null)
  const [guess, setGuess] = useState('')
  const [feedback, setFeedback] = useState('')
  const [status, setStatus] = useState('idle')

  const loadNext = async () => {
    setStatus('loading')
    const res = await fetch(`${RESOURCE_BASE_URL}/puzzles/next?packId=${packId}`, {
      headers: { Authorization: `Bearer ${token}` }
    })

    if (!res.ok) {
      setFeedback('Failed to load puzzle')
      setStatus('error')
      return
    }

    const data = await res.json()
    if (data.completed) {
      setPuzzle(null)
      setFeedback('Pack completed!')
      setStatus('completed')
      return
    }

    setPuzzle(data)
    setGuess('')
    setFeedback('')
    setStatus('ready')
  }

  useEffect(() => {
    loadNext()
  }, [packId])

  const submitGuess = async (e) => {
    e.preventDefault()
    if (!guess.trim()) return
    setStatus('submitting')

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
      setStatus('error')
      return
    }

    const data = await res.json()
    setFeedback(data.correct ? 'Correct! Next puzzle loading...' : 'Try again')

    if (data.correct && data.nextAvailable) {
      await loadNext()
    } else {
      setStatus('ready')
    }
  }

  const letters = useMemo(() => {
    const clean = normalize(guess)
    return clean.split('').slice(0, 12)
  }, [guess])

  return (
    <div className="game-wrap">
      <div className="game-header">
        <div>
          <h2>4 Pics 1 Word</h2>
          <p className="muted">Find the single word that connects all four images.</p>
        </div>
        <button type="button" className="ghost" onClick={loadNext}>
          Skip
        </button>
      </div>

      {feedback && (
        <div className={`banner ${feedback.includes('Correct') ? 'success' : 'warn'}`}>
          {feedback}
        </div>
      )}

      {status === 'loading' && <p>Loading...</p>}
      {status === 'completed' && <p>Pack completed. Choose another pack.</p>}

      {puzzle && (
        <>
          <div className="game-grid">
            {puzzle.images.map((url) => (
              <div key={url} className="game-tile">
                <img src={url} alt="puzzle" />
              </div>
            ))}
          </div>

          <form onSubmit={submitGuess} className="answer-panel">
            <label className="answer-label">Your Answer</label>
            <div className="answer-boxes">
              {Array.from({ length: 10 }).map((_, idx) => (
                <div key={idx} className="answer-slot">
                  {letters[idx] ? letters[idx].toUpperCase() : ''}
                </div>
              ))}
            </div>
            <input
              className="answer-input"
              placeholder="Type your answer here"
              value={guess}
              onChange={(e) => setGuess(e.target.value)}
            />
            <button type="submit" disabled={status === 'submitting'}>
              {status === 'submitting' ? 'Checking...' : 'Submit'}
            </button>
          </form>
        </>
      )}
    </div>
  )
}
