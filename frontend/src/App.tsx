import { Navigate, Outlet, Route, Routes, useLocation } from 'react-router-dom'
import { useAuth } from './auth'
import AppLayout from './layouts/AppLayout'
import AgentChatPage from './pages/AgentChatPage'
import AgentHubPage from './pages/AgentHubPage'
import CampusHubPage from './pages/CampusHubPage'
import HomePage from './pages/HomePage'
import LoginPage from './pages/LoginPage'
import PlatformHubPage from './pages/PlatformHubPage'
import ServicePage from './pages/ServicePage'

function RequireAuth() {
  const { isLoggedIn } = useAuth()
  const location = useLocation()
  if (!isLoggedIn) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }
  return <Outlet />
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route path="/" element={<HomePage />} />
          <Route path="/campus" element={<CampusHubPage />} />
          <Route path="/campus/:serviceId" element={<ServicePage />} />
          <Route path="/platform" element={<PlatformHubPage />} />
          <Route path="/platform/:serviceId" element={<ServicePage />} />
          <Route path="/agent" element={<AgentHubPage />} />
          <Route path="/agent/chat/:conversationId" element={<AgentChatPage />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
