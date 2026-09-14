export type AgentAction = {
  agent: string
  action: string
  detail: string
  payload?: unknown
}

export type ChatResponse = {
  reply: string
  detectedStage: number
  intent: string
  actions: AgentAction[]
  result?: unknown
  mode?: string
  model?: string | null
  conversationId?: number | null
}

export type ConversationDto = {
  id: number
  feature: string
  title: string
  createdAtUtc: string
  updatedAtUtc: string
}

export type HealthInfo = {
  status: string
  llmEnabled: boolean
  mode: string
  secretSource?: string
  configuredProvider?: string
  auth?: string
  persistence?: string
}

export type AccountInfo = {
  username: string
  displayName: string
  role: string
  subtitle: string
  studentProfileId: string | null
  passwordHint: string
}

export type UserDto = {
  id: number
  username: string
  displayName: string
  role: string
  studentProfileId: string | null
  subtitle: string
}

export type LoginResponse = {
  accessToken: string
  tokenType: string
  expiresInMinutes: number
  user: UserDto
}

export type ChatMessage = {
  role: 'user' | 'assistant'
  content: string
  actions?: AgentAction[]
  stage?: number
}

export type ServiceCatalogItem = {
  id: string
  name: string
  path: string
  roles: string[]
  desc: string
  group?: 'campusTools' | 'platform'
}

const API_BASE = import.meta.env.VITE_API_BASE ?? ''
const TOKEN_KEY = 'campusai_token'

export function getToken() {
  return sessionStorage.getItem(TOKEN_KEY)
}

export function setToken(token: string | null) {
  if (!token) sessionStorage.removeItem(TOKEN_KEY)
  else sessionStorage.setItem(TOKEN_KEY, token)
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(init?.headers as Record<string, string> ?? {})
  }
  const token = getToken()
  if (token) headers.Authorization = `Bearer ${token}`

  const res = await fetch(`${API_BASE}${path}`, { ...init, headers })
  if (res.status === 401) throw new Error('未登入或登入已過期，請重新登入')
  if (!res.ok) {
    const text = await res.text()
    throw new Error(text || `HTTP ${res.status}`)
  }
  if (res.status === 204) return undefined as T
  return res.json() as Promise<T>
}

export function fetchHealth() {
  return request<HealthInfo>('/api/health')
}

export function fetchAccounts() {
  return request<AccountInfo[]>('/api/auth/accounts')
}

export function login(username: string, password: string) {
  return request<LoginResponse>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ username, password })
  })
}

export function sendChat(
  message: string,
  studentId: string | null,
  stage: number,
  feature: string,
  role: string,
  conversationId: number | null
) {
  return request<ChatResponse>('/api/chat', {
    method: 'POST',
    body: JSON.stringify({ message, studentId, stage, feature, role, conversationId })
  })
}

export function fetchConversations() {
  return request<ConversationDto[]>('/api/conversations')
}

export function createConversation(feature: string) {
  return request<ConversationDto>('/api/conversations', {
    method: 'POST',
    body: JSON.stringify({ feature })
  })
}

export function fetchConversationMessages(id: number) {
  return request<Array<{ id: number; feature: string; role: string; content: string; createdAtUtc: string }>>(
    `/api/conversations/${id}/messages`
  )
}

export function deleteConversation(id: number) {
  return request<void>(`/api/conversations/${id}`, { method: 'DELETE' })
}

export function fetchServiceCatalog() {
  return request<ServiceCatalogItem[]>('/api/services/catalog')
}

export function fetchServicePage(id: string) {
  return request<{ endpoint?: string; data?: unknown; note?: string } & Record<string, unknown>>(
    `/api/services/${encodeURIComponent(id)}`
  )
}
