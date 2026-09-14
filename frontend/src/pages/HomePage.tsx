import { useNavigate } from 'react-router-dom'
import './AgentHubPage.css'

const sections = [
  {
    path: '/campus',
    title: '校務系統',
    desc: '學生、課程、活動、申請、通知、行事曆等校務服務入口'
  },
  {
    path: '/platform',
    title: '平台服務',
    desc: '身分驗證、權限控管與 Tool Calling 等平台能力入口'
  },
  {
    path: '/agent',
    title: 'AI 功能',
    desc: '校園問答、獎學金、選課與活動等 AI 助理功能'
  }
]

export default function HomePage() {
  const navigate = useNavigate()

  return (
    <div className="hub">
      <header className="top">
        <h2>主畫面</h2>
        <p className="hub-sub">選擇要進入的系統區塊</p>
      </header>
      <div className="hub-grid">
        {sections.map(s => (
          <button
            key={s.path}
            type="button"
            className="hub-card"
            onClick={() => navigate(s.path)}
          >
            <strong>{s.title}</strong>
            <span>{s.desc}</span>
          </button>
        ))}
      </div>
    </div>
  )
}
