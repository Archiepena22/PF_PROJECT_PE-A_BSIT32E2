import { Navigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext.jsx'

export default function RequireAdmin({ children }) {
  const { user } = useAuth()

  if (!user || user.role !== 'admin') {
    return <Navigate to="/packs" replace />
  }

  return children
}
