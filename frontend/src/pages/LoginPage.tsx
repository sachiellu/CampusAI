import { useEffect, useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { fetchAccounts, fetchHealth, login, setToken, type AccountInfo } from '../api'
import { useAuth } from '../auth'
import './LoginPage.css'

type Role = 'student' | 'teacher' | 'admin'

export default function LoginPage() {
  const { isLoggedIn, setSessionUser, refreshServiceCatalog } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [params] = useSearchParams()
  const [accounts, setAccounts] = useState<AccountInfo[]>([])
  const [loginRole, setLoginRole] = useState<Role>('student')
  const [password, setPassword] = useState('Passw0rd!')
  const [loggingIn, setLoggingIn] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    void (async () => {
      try {
        const [list, health] = await Promise.all([fetchAccounts(), fetchHealth()])
        setAccounts(list)
        console.info('[CampusAI] backend ok', {
          auth: health.auth,
          persistence: health.persistence,
          llmEnabled: health.llmEnabled,
          mode: health.mode
        })
      } catch (e) {
        console.error('[CampusAI] backend unreachable', e)
        setError('目前無法登入，請稍後再試')
      }
    })()
  }, [])

  if (isLoggedIn) return <Navigate to="/" replace />

  const filtered = accounts.filter(a => a.role === loginRole)

  async function doLogin(account: AccountInfo) {
    setLoggingIn(true)
    setError('')
    try {
      const res = await login(account.username, password || account.passwordHint)
      setToken(res.accessToken)
      setSessionUser(res.user)
      await refreshServiceCatalog()
      const fromState = (location.state as { from?: string } | null)?.from
      navigate(params.get('redirect') || fromState || '/', { replace: true })
    } catch (e) {
      console.error('[CampusAI] login failed', e)
      setError('登入失敗，請確認帳號密碼')
    } finally {
      setLoggingIn(false)
    }
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault()
  }

  return (
    <div className="login">
      <form className="login-card" onSubmit={onSubmit}>
        <p className="eyebrow">Campus AI Agent</p>
        <h1>登入</h1>
        <p className="login-desc">請選擇身分與帳戶進入系統</p>

        <label className="pwd">
          密碼
          <input
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={e => setPassword(e.target.value)}
          />
        </label>

        <div className="role-tabs">
          {(['student', 'teacher', 'admin'] as Role[]).map(role => (
            <button
              key={role}
              type="button"
              className={loginRole === role ? 'on' : undefined}
              onClick={() => setLoginRole(role)}
            >
              {role === 'student' ? '學生' : role === 'teacher' ? '教師' : '行政'}
            </button>
          ))}
        </div>

        <div className="accounts">
          {filtered.map(a => (
            <button
              key={a.username}
              type="button"
              className={`account ${a.role}`}
              disabled={loggingIn}
              onClick={() => void doLogin(a)}
            >
              <strong>{a.displayName}</strong>
              <span>{a.subtitle}</span>
              <span className="tag">{a.username}</span>
            </button>
          ))}
          {!filtered.length && <p className="muted">載入帳戶中…</p>}
        </div>
        {error && <p className="error">{error}</p>}
      </form>
    </div>
  )
}
