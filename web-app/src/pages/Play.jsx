import { useEffect, useMemo, useState } from 'react'
import { useParams } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext.jsx'

const RESOURCE_BASE_URL = 'http://localhost:5076'

const LETTERS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'

const buildKeyboard = (seed = '') => {
  const base = seed.toUpperCase().replace(/[^A-Z]/g, '')
  const pool = new Set(base.split(''))
  while (pool.size < 12) {
    pool.add(LETTERS[Math.floor(Math.random() * LETTERS.length)])
  }
  return Array.from(pool).slice(0, 12)
}

export default function Play() {
  const { packId } = useParams()
  const { token } = useAuth()
  const [puzzle, setPuzzle] = useState(null)
  const [guess, setGuess] = useState('')
  const [feedback, setFeedback] = useState('')
  const [status, setStatus] = useState('idle')
  const [keyboard, setKeyboard] = useState([])
  const [coins, setCoins] = useState(250)
  const [level, setLevel] = useState(1)

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
    setKeyboard(buildKeyboard())
    setLevel((prev) => prev + 1)
    setStatus('ready')
  }

  useEffect(() => {
    loadNext()
  }, [packId])

  const submitGuess = async () => {
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
    setFeedback(data.correct ? 'Correct!' : 'Try again')

    if (data.correct) {
      setCoins((prev) => prev + 10)
      if (data.nextAvailable) {
        await loadNext()
      }
    }

    setStatus('ready')
  }

  const onKeyPress = (letter) => {
    if (guess.length >= 8) return
    setGuess((prev) => prev + letter)
  }

  const onBackspace = () => {
    setGuess((prev) => prev.slice(0, -1))
  }

  const slots = useMemo(() => {
    const chars = guess.split('')
    return Array.from({ length: 8 }).map((_, i) => chars[i] || '')
  }, [guess])

  return (
    <div className="game-screen">
      <div className="game-top">
        <button type="button" className="icon-btn" onClick={() => window.history.back()}>
          ? Back
        </button>
        <div className="coin-pill">
          <span>{coins}</span>
          <span className="coin-dot">?</span>
        </div>
      </div>

      <div className="level">Level {level}</div>

      {feedback && <div className="toast">{feedback}</div>}
      {status === 'loading' && <div className="toast">Loading...</div>}
      {status === 'completed' && <div className="toast">Pack completed!</div>}

      {puzzle && (
        <>
          <div className="photo-grid">
            {puzzle.images.map((url) => (
              <img key={url} src={url} alt="puzzle" />
            ))}
          </div>

          <div className="answer-slots">
            {slots.map((char, idx) => (
              <div key={idx} className="slot">
                {char}
              </div>
            ))}
          </div>

          <div className="keyboard">
            <div className="key-row">
              {keyboard.slice(0, 6).map((key) => (
                <button key={key} className="key" type="button" onClick={() => onKeyPress(key)}>
                  {key}
                </button>
              ))}
            </div>
            <div className="key-row">
              {keyboard.slice(6, 12).map((key) => (
                <button key={key} className="key" type="button" onClick={() => onKeyPress(key)}>
                  {key}
                </button>
              ))}
            </div>
            <div className="key-actions">
              <button type="button" className="key action" onClick={onBackspace}>
                ?
              </button>
              <button type="button" className="key action" onClick={submitGuess} disabled={status === 'submitting'}>
                ?
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  )
}
