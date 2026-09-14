import { useNavigate } from 'react-router-dom'
import { createConversation, fetchConversations } from '../api'
import { useAuth } from '../auth'
import { featuresForRole, type FeatureId } from '../features'
import './AgentHubPage.css'

export default function AgentHubPage() {
  const { session, setConversations } = useAuth()
  const navigate = useNavigate()
  const features = featuresForRole(session?.role)

  const hubTitle =
    session?.role === 'teacher'
      ? 'AI 功能 · 教師'
      : session?.role === 'admin'
        ? 'AI 功能 · 行政'
        : 'AI 功能 · 學生'

  async function openFeature(id: FeatureId) {
    const conv = await createConversation(id)
    setConversations(await fetchConversations())
    navigate(`/agent/chat/${conv.id}`)
  }

  return (
    <div className="hub">
      <header className="top">
        <h2>{hubTitle}</h2>
        <button type="button" className="ghost" onClick={() => navigate('/')}>
          回主畫面
        </button>
      </header>
      <p className="hub-sub">點選下方功能才會建立對話；「新對話」只會回到此頁。</p>
      <div className="hub-grid">
        {features.map(f => (
          <button key={f.id} type="button" className="hub-card" onClick={() => void openFeature(f.id)}>
            <strong>{f.label}</strong>
            <span>{f.desc}</span>
          </button>
        ))}
      </div>
    </div>
  )
}
