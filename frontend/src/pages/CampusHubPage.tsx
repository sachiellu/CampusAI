import { useAuth } from '../auth'
import SectionHubPage from './SectionHubPage'

export default function CampusHubPage() {
  const { campusServices } = useAuth()
  return (
    <SectionHubPage
      title="校務系統"
      subtitle="查詢與申請相關校務服務。以下頁面僅供展示，用來銜接原始校務系統。"
      services={campusServices}
      basePath="/campus"
    />
  )
}
