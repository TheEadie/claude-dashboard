import { Navigate, Route, Routes } from 'react-router-dom'
import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'
import SessionPage from './pages/SessionPage'

function Home() {
  return (
    <Box p={4}>
      <Typography variant="h5">Claude Dashboard</Typography>
      <Typography variant="body1">
        Open a session at <code>/session/&lt;session-id&gt;</code>.
      </Typography>
    </Box>
  )
}

function NotFoundRoute() {
  return <Navigate to="/" replace />
}

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<Home />} />
      <Route path="/session/:id" element={<SessionPage />} />
      <Route path="*" element={<NotFoundRoute />} />
    </Routes>
  )
}
