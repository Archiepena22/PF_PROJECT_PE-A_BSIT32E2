import { NavLink, Route, Routes } from 'react-router-dom'
import RequireAuth from './routes/RequireAuth.jsx'
import RequireAdmin from './routes/RequireAdmin.jsx'
import Login from './pages/Login.jsx'
import Register from './pages/Register.jsx'
import Packs from './pages/Packs.jsx'
import Play from './pages/Play.jsx'
import Profile from './pages/Profile.jsx'
import AdminImages from './pages/admin/Images.jsx'
import AdminTags from './pages/admin/Tags.jsx'
import AdminPuzzles from './pages/admin/Puzzles.jsx'
import AdminPacks from './pages/admin/Packs.jsx'
import { useAuth } from './auth/AuthContext.jsx'

export default function App() {
  const { user, logout } = useAuth()

  return (
    <div className="shell">
      <header className="topbar">
        <div className="brand">
          <div className="brand-icon">4P</div>
          <div>
            <h1>4 Pics 1 Word</h1>
            <p>Find the word that connects the four clues</p>
          </div>
        </div>
        <nav className="nav-links">
          <NavLink to="/packs">Packs</NavLink>
          <NavLink to="/profile">Profile</NavLink>
          {user?.role === 'admin' && (
            <>
              <NavLink to="/admin/images">Images</NavLink>
              <NavLink to="/admin/tags">Tags</NavLink>
              <NavLink to="/admin/puzzles">Puzzles</NavLink>
              <NavLink to="/admin/packs">Packs</NavLink>
            </>
          )}
          {!user && <NavLink to="/login">Login</NavLink>}
          {!user && <NavLink to="/register">Register</NavLink>}
          {user && (
            <button type="button" onClick={logout} className="ghost">
              Logout
            </button>
          )}
        </nav>
      </header>

      <main className="content">
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/register" element={<Register />} />
          <Route
            path="/packs"
            element={
              <RequireAuth>
                <Packs />
              </RequireAuth>
            }
          />
          <Route
            path="/play/:packId"
            element={
              <RequireAuth>
                <Play />
              </RequireAuth>
            }
          />
          <Route
            path="/profile"
            element={
              <RequireAuth>
                <Profile />
              </RequireAuth>
            }
          />
          <Route
            path="/admin/images"
            element={
              <RequireAuth>
                <RequireAdmin>
                  <AdminImages />
                </RequireAdmin>
              </RequireAuth>
            }
          />
          <Route
            path="/admin/tags"
            element={
              <RequireAuth>
                <RequireAdmin>
                  <AdminTags />
                </RequireAdmin>
              </RequireAuth>
            }
          />
          <Route
            path="/admin/puzzles"
            element={
              <RequireAuth>
                <RequireAdmin>
                  <AdminPuzzles />
                </RequireAdmin>
              </RequireAuth>
            }
          />
          <Route
            path="/admin/packs"
            element={
              <RequireAuth>
                <RequireAdmin>
                  <AdminPacks />
                </RequireAdmin>
              </RequireAuth>
            }
          />
          <Route path="*" element={<Login />} />
        </Routes>
      </main>
    </div>
  )
}
