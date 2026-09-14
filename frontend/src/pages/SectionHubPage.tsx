import { useNavigate } from 'react-router-dom'
import type { ServiceCatalogItem } from '../api'
import './AgentHubPage.css'

type Props = {
  title: string
  subtitle: string
  services: ServiceCatalogItem[]
  basePath: '/campus' | '/platform'
}

export default function SectionHubPage({ title, subtitle, services, basePath }: Props) {
  const navigate = useNavigate()

  return (
    <div className="hub">
      <header className="top">
        <h2>{title}</h2>
        <button type="button" className="ghost" onClick={() => navigate('/')}>
          回主畫面
        </button>
      </header>
      <p className="hub-sub">{subtitle}</p>
      <div className="hub-grid">
        {services.map(s => (
          <button
            key={s.id}
            type="button"
            className="hub-card"
            onClick={() => navigate(`${basePath}/${s.id}`)}
          >
            <strong>{s.name}</strong>
            <span>{s.desc}</span>
          </button>
        ))}
        {!services.length && (
          <p className="hub-sub">目前角色尚無可顯示的服務。</p>
        )}
      </div>
    </div>
  )
}
