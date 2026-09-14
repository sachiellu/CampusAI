import { useEffect, useState, type MouseEvent } from 'react'
import { NavLink, Outlet, useLocation, useNavigate, useParams } from 'react-router-dom'
import { deleteConversation, fetchConversations } from '../api'
import { useAuth } from '../auth'
import { labelForFeature, roleLabelOf } from '../features'
import './AppLayout.css'

type SectionId = 'campus' | 'platform' | 'agent'

function sectionFromPath(pathname: string): SectionId | null {
  if (pathname.startsWith('/campus')) return 'campus'
  if (pathname.startsWith('/platform')) return 'platform'
  if (pathname.startsWith('/agent')) return 'agent'
  return null
}

export default function AppLayout() {
  const {
    session,
    campusServices,
    platformServices,
    conversations,
    setConversations,
    clearAuth,
    refreshServiceCatalog
  } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const params = useParams()
  const [error, setError] = useState('')
  const routeSection = sectionFromPath(location.pathname)
  const [openSection, setOpenSection] = useState<SectionId | null>(routeSection ?? 'agent')
  const activeConversationId = params.conversationId ? Number(params.conversationId) : null

  useEffect(() => {
    if (routeSection) setOpenSection(routeSection)
  }, [routeSection])

  useEffect(() => {
    void (async () => {
      await refreshServiceCatalog()
      try {
        setConversations(await fetchConversations())
      } catch (e) {
        console.error('[CampusAI] conversations failed', e)
      }
    })()
  }, [refreshServiceCatalog, setConversations])

  async function removeConversation(id: number, ev: MouseEvent) {
    ev.preventDefault()
    ev.stopPropagation()
    try {
      await deleteConversation(id)
      setConversations(conversations.filter(c => c.id !== id))
      if (activeConversationId === id) navigate('/agent')
    } catch (e) {
      console.error('[CampusAI] delete conversation failed', e)
      setError('刪除失敗')
    }
  }

  function goSection(id: SectionId, hubPath: string) {
    setOpenSection(id)
    navigate(hubPath)
  }

  return (
    <div className="shell">
      <aside className="nav">
        <div className="brand">Campus AI</div>
        <div className="who">
          <strong>{session?.displayName}</strong>
          <span>{roleLabelOf(session?.role)} · {session?.subtitle}</span>
        </div>

        <NavLink
          className={({ isActive }) => `side-home${isActive ? ' on' : ''}`}
          to="/"
          end
          onClick={() => setOpenSection(null)}
        >
          主畫面
        </NavLink>

        <div className={`side-acc${openSection === 'campus' ? ' open' : ''}${routeSection === 'campus' ? ' active' : ''}`}>
          <button
            type="button"
            className="side-acc-head"
            aria-expanded={openSection === 'campus'}
            onClick={() => goSection('campus', '/campus')}
          >
            <span>校務系統</span>
            <span className="side-acc-chevron" aria-hidden>▾</span>
          </button>
          {openSection === 'campus' && (
            <nav className="side-nav">
              {campusServices.map(s => (
                <NavLink key={s.id} className="side-link" to={`/campus/${s.id}`}>
                  {s.name}
                </NavLink>
              ))}
            </nav>
          )}
        </div>

        <div className={`side-acc${openSection === 'platform' ? ' open' : ''}${routeSection === 'platform' ? ' active' : ''}`}>
          <button
            type="button"
            className="side-acc-head"
            aria-expanded={openSection === 'platform'}
            onClick={() => goSection('platform', '/platform')}
          >
            <span>平台服務</span>
            <span className="side-acc-chevron" aria-hidden>▾</span>
          </button>
          {openSection === 'platform' && (
            <nav className="side-nav">
              {platformServices.map(s => (
                <NavLink key={s.id} className="side-link" to={`/platform/${s.id}`}>
                  {s.name}
                </NavLink>
              ))}
            </nav>
          )}
        </div>

        <div className={`side-acc${openSection === 'agent' ? ' open' : ''}${routeSection === 'agent' ? ' active' : ''}`}>
          <button
            type="button"
            className="side-acc-head"
            aria-expanded={openSection === 'agent'}
            onClick={() => goSection('agent', '/agent')}
          >
            <span>AI 功能</span>
            <span className="side-acc-chevron" aria-hidden>▾</span>
          </button>
          {openSection === 'agent' && (
            <>
              <NavLink className="side-btn primary" to="/agent" end>
                ＋ 新對話
              </NavLink>
              <div className="side-label">對話紀錄</div>
              <nav className="conv-list">
                {conversations.map(c => (
                  <button
                    key={c.id}
                    type="button"
                    className={`conv-item${activeConversationId === c.id ? ' on' : ''}`}
                    onClick={() => navigate(`/agent/chat/${c.id}`)}
                  >
                    <span className="conv-title">{c.title}</span>
                    <span className="conv-meta">{labelForFeature(c.feature)}</span>
                    <span className="conv-del" title="刪除" onClick={ev => void removeConversation(c.id, ev)}>×</span>
                  </button>
                ))}
                {!conversations.length && (
                  <p className="muted tiny">尚無對話，從 AI 功能主頁選功能開始</p>
                )}
              </nav>
            </>
          )}
        </div>

        {error && <p className="error tiny">{error}</p>}
        <button
          type="button"
          className="logout"
          onClick={() => {
            clearAuth()
            navigate('/login')
          }}
        >
          登出
        </button>
      </aside>

      <section className="workspace">
        <Outlet />
      </section>
    </div>
  )
}
