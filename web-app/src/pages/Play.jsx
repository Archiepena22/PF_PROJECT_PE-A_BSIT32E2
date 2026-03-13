import { useParams } from 'react-router-dom'

export default function Play() {
  const { packId } = useParams()
  return (
    <div className="card">
      <h2>Play Pack: {packId}</h2>
      <p>4 images + guess input will render here.</p>
    </div>
  )
}
