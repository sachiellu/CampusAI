import { useAuth } from '../auth'
import SectionHubPage from './SectionHubPage'

export default function PlatformHubPage() {
  const { platformServices } = useAuth()
  return (
    <SectionHubPage
      title="平台服務"
      subtitle="身分、權限與工具介面。以下頁面僅供展示，用來銜接平台／IdP。"
      services={platformServices}
      basePath="/platform"
    />
  )
}
