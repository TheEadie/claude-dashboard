import { Navigate, Route, Routes } from 'react-router-dom'
import SessionListPage from './pages/SessionListPage'
import SessionPage from './pages/SessionPage'

function NotFoundRoute() {
  return <Navigate to="/" replace />
}

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<SessionListPage />} />
      <Route path="/session/:id" element={<SessionPage />} />
      <Route path="*" element={<NotFoundRoute />} />
    </Routes>
  )
}
