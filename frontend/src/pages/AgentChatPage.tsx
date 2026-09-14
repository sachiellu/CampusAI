import { useEffect, useMemo, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import {
  deleteConversation,
  fetchConversationMessages,
  fetchConversations,
  sendChat,
  type AgentAction,
  type ChatMessage
} from '../api'
import { useAuth } from '../auth'
import { featuresForRole, labelForFeature, type FeatureId } from '../features'
import './AgentChatPage.css'

export default function AgentChatPage() {
  const { conversationId } = useParams()
  const convId = Number(conversationId)
  const { session, conversations, setConversations } = useAuth()
  const navigate = useNavigate()

  const [feature, setFeature] = useState<FeatureId>('qa')
  const [stage, setStage] = useState(2)
  const [input, setInput] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [latestActions, setLatestActions] = useState<AgentAction[]>([])
  const chatBox = useRef<HTMLDivElement | null>(null)
  const features = featuresForRole(session?.role)
  const featureLabel = labelForFeature(feature)

  const quickPrompts = useMemo(() => {
    if (session?.role === 'teacher') {
      return feature === 'scholarship'
        ? ['目前有哪些畢業或成績門檻要追蹤？', '列出需要提醒的學生']
        : ['目前有哪些需關注的導生狀況？', '誰需要優先約談？']
    }
    if (session?.role === 'admin') {
      return feature === 'scholarship'
        ? ['給我本學期決策支援建議', '獎助與開課資源如何配置？']
        : ['目前校務統計摘要？', '學生與活動指標如何？']
    }
    switch (feature) {
      case 'qa':
        return ['獎學金申請辦法是什麼？', '選課注意事項有哪些？']
      case 'scholarship':
        return ['我可以申請哪些獎學金？', '幫我產生申請草稿並檢查缺件']
      case 'course':
        return ['下學期我該怎麼選課？', '幫我排出沒有衝堂的課表']
      case 'activity':
        return ['有哪些適合我的校園活動？', '幫我報名並加入行事曆']
    }
  }, [feature, session?.role])

  function welcomeMessages(label: string, stageValue: number): ChatMessage[] {
    return [{
      role: 'assistant',
      content: `${session?.displayName ?? ''}，目前在「${label}」。直接提問或點建議即可；系統會查詢校務資料並回覆。`,
      stage: stageValue
    }]
  }

  useEffect(() => {
    void (async () => {
      setError('')
      if (!Number.isFinite(convId)) {
        setError('無效的對話')
        return
      }
      try {
        const list = conversations.length ? conversations : await fetchConversations()
        if (!conversations.length) setConversations(list)
        const conv = list.find(c => c.id === convId)
        const nextFeature = ((conv?.feature as FeatureId) || 'qa')
        setFeature(nextFeature)
        const meta = features.find(f => f.id === nextFeature)
        const nextStage = meta?.stage ?? 2
        setStage(nextStage)
        const items = await fetchConversationMessages(convId)
        setMessages(
          items.length
            ? items.map(h => ({ role: h.role as 'user' | 'assistant', content: h.content }))
            : welcomeMessages(labelForFeature(nextFeature), nextStage)
        )
        setLatestActions([])
      } catch (e) {
        console.error('[CampusAI] open conversation failed', e)
        setError('無法開啟對話')
      }
    })()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [convId])

  useEffect(() => {
    if (chatBox.current) chatBox.current.scrollTop = chatBox.current.scrollHeight
  }, [messages, loading])

  async function removeConversation() {
    try {
      await deleteConversation(convId)
      setConversations(await fetchConversations())
      navigate('/agent')
    } catch (e) {
      console.error('[CampusAI] delete conversation failed', e)
      setError('刪除失敗')
    }
  }

  async function ask(text?: string) {
    const content = (text ?? input).trim()
    if (!content || loading || !session || !Number.isFinite(convId)) return
    setError('')
    setInput('')
    setMessages(prev => [...prev, { role: 'user', content }])
    setLoading(true)
    try {
      const res = await sendChat(
        content,
        session.studentProfileId,
        stage,
        feature,
        session.role,
        convId
      )
      setMessages(prev => [...prev, {
        role: 'assistant',
        content: res.reply,
        actions: res.actions,
        stage: res.detectedStage
      }])
      setLatestActions(res.actions ?? [])
      setConversations(await fetchConversations())
    } catch (e) {
      console.error('[CampusAI] chat failed', e)
      setError(e instanceof Error ? e.message : '送出失敗')
    } finally {
      setLoading(false)
    }
  }

  function onKeyDown(e: KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      void ask()
    }
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault()
    void ask()
  }

  return (
    <div className="chat-wrap">
      <header className="top">
        <h2>{featureLabel}</h2>
        <div className="top-actions">
          <button type="button" className="ghost" onClick={() => navigate('/')}>
            回主畫面
          </button>
          <button type="button" className="ghost" onClick={() => void removeConversation()}>
            刪除此對話
          </button>
        </div>
      </header>

      <div className="chat-panel">
        <div ref={chatBox} className="messages">
          {messages.map((m, idx) => (
            <article key={idx} className={`bubble ${m.role}`}>
              {m.role === 'assistant' && <header>助理</header>}
              <pre>{m.content}</pre>
            </article>
          ))}
          {loading && <p className="loading">處理中…</p>}
        </div>

        <div className="quick">
          {quickPrompts.map(q => (
            <button key={q} type="button" onClick={() => void ask(q)}>{q}</button>
          ))}
        </div>

        <form className="composer" onSubmit={onSubmit}>
          <textarea
            rows={2}
            placeholder="輸入問題…"
            value={input}
            onChange={e => setInput(e.target.value)}
            onKeyDown={onKeyDown}
          />
          <button type="submit" disabled={loading}>送出</button>
        </form>
        {error && <p className="error">{error}</p>}

        {latestActions.length > 0 && (
          <details className="agent-log">
            <summary>
              執行紀錄
              <span className="count">{latestActions.length}</span>
            </summary>
            <ul className="actions">
              {latestActions.map((a, i) => (
                <li key={i}>
                  <div className="agent">{a.agent}</div>
                  <div className="action">{a.action}</div>
                  <div className="detail">{a.detail}</div>
                </li>
              ))}
            </ul>
          </details>
        )}
      </div>
    </div>
  )
}
