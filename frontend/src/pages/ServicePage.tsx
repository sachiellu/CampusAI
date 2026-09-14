import { useMemo } from 'react'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '../auth'
import './ServicePage.css'

export default function ServicePage() {
  const { serviceId = '' } = useParams()
  const { serviceCatalog } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  const isPlatform = location.pathname.startsWith('/platform')

  const meta = useMemo(
    () => serviceCatalog.find(s => s.id === serviceId) ?? null,
    [serviceCatalog, serviceId]
  )

  const name = meta?.name ?? serviceId
  const boxText = isPlatform
    ? `此頁面尚未實作「${name}」。\n\n僅供展示，預留平台 API（身分／權限／Tool Calling），用來銜接正式平台或 IdP。`
    : `此頁面尚未實作「${name}」。\n\n僅供展示，預留校務系統 API，用來銜接原始校務系統。`

  return (
    <div className="service-page">
      <header className="top">
        <div>
          <h2>{name}</h2>
          <p className="sub">{meta?.desc ?? '服務頁'}</p>
        </div>
        <button type="button" className="ghost" onClick={() => navigate('/')}>
          回主畫面
        </button>
      </header>
      <div className="placeholder-box">
        <p>{boxText}</p>
      </div>
    </div>
  )
}
