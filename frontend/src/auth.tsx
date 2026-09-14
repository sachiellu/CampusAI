import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode
} from 'react'
import {
  fetchServiceCatalog,
  getToken,
  setToken,
  type ConversationDto,
  type ServiceCatalogItem,
  type UserDto
} from './api'

const USER_KEY = 'campusai_user'

const fallbackCatalog: ServiceCatalogItem[] = [
  { id: 'students', group: 'campusTools', name: '學生資料', path: '/api/services/students', roles: ['student', 'teacher', 'admin'], desc: '查詢學生檔案與修課／興趣資料' },
  { id: 'courses', group: 'campusTools', name: '課程資料', path: '/api/services/courses', roles: ['student', 'teacher', 'admin'], desc: '開課清單、時段、先修' },
  { id: 'activities', group: 'campusTools', name: '活動資料', path: '/api/services/activities', roles: ['student', 'teacher', 'admin'], desc: '校園活動與名額' },
  { id: 'applications', group: 'campusTools', name: '申請服務', path: '/api/services/applications', roles: ['student', 'admin'], desc: '獎學金／活動申請服務' },
  { id: 'notifications', group: 'campusTools', name: 'Email／通知', path: '/api/services/notifications', roles: ['student', 'teacher', 'admin'], desc: '站內與郵件通知' },
  { id: 'calendar', group: 'campusTools', name: '行事曆', path: '/api/services/calendar', roles: ['student', 'teacher', 'admin'], desc: '行程同步與提醒事件' },
  { id: 'campus', group: 'campusTools', name: '校務資料', path: '/api/services/campus', roles: ['teacher', 'admin'], desc: '校務統計與獎助摘要' },
  { id: 'platform-identity', group: 'platform', name: '身分驗證', path: '/api/services/platform-identity', roles: ['student', 'teacher', 'admin'], desc: '目前登入身分與驗證狀態' },
  { id: 'platform-permission', group: 'platform', name: '權限控管', path: '/api/services/platform-permission', roles: ['student', 'teacher', 'admin'], desc: '角色可使用功能範圍' },
  { id: 'platform-tools', group: 'platform', name: 'Tool Calling', path: '/api/services/platform-tools', roles: ['student', 'teacher', 'admin'], desc: 'Agent 工具呼叫介面（僅供展示，尚未實作）' }
]

function readUser(): UserDto | null {
  try {
    const raw = sessionStorage.getItem(USER_KEY)
    return raw ? (JSON.parse(raw) as UserDto) : null
  } catch {
    return null
  }
}

type AuthContextValue = {
  session: UserDto | null
  serviceCatalog: ServiceCatalogItem[]
  conversations: ConversationDto[]
  campusServices: ServiceCatalogItem[]
  platformServices: ServiceCatalogItem[]
  isLoggedIn: boolean
  setSessionUser: (user: UserDto | null) => void
  clearAuth: () => void
  setConversations: (list: ConversationDto[]) => void
  refreshServiceCatalog: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<UserDto | null>(() => readUser())
  const [serviceCatalog, setServiceCatalog] = useState<ServiceCatalogItem[]>([])
  const [conversations, setConversations] = useState<ConversationDto[]>([])

  const setSessionUser = useCallback((user: UserDto | null) => {
    setSession(user)
    if (!user) sessionStorage.removeItem(USER_KEY)
    else sessionStorage.setItem(USER_KEY, JSON.stringify(user))
  }, [])

  const clearAuth = useCallback(() => {
    setToken(null)
    setSessionUser(null)
    setServiceCatalog([])
    setConversations([])
  }, [setSessionUser])

  const refreshServiceCatalog = useCallback(async () => {
    const role = session?.role ?? 'student'
    setServiceCatalog(fallbackCatalog.filter(s => s.roles.includes(role)))
    try {
      const list = await fetchServiceCatalog()
      setServiceCatalog(
        list.map(s => ({
          ...s,
          group: s.group ?? (s.id.startsWith('platform') ? 'platform' : 'campusTools')
        }))
      )
    } catch (e) {
      console.error('[CampusAI] service catalog failed', e)
    }
  }, [session?.role])

  const value = useMemo<AuthContextValue>(() => ({
    session,
    serviceCatalog,
    conversations,
    campusServices: serviceCatalog.filter(s => (s.group ?? 'campusTools') === 'campusTools'),
    platformServices: serviceCatalog.filter(s => s.group === 'platform'),
    isLoggedIn: Boolean(getToken() && session),
    setSessionUser,
    clearAuth,
    setConversations,
    refreshServiceCatalog
  }), [session, serviceCatalog, conversations, setSessionUser, clearAuth, refreshServiceCatalog])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}
