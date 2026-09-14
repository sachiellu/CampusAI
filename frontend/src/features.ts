export type FeatureId = 'qa' | 'scholarship' | 'course' | 'activity'

export const studentFeatures = [
  { id: 'qa' as const, label: '校園問答', stage: 1, desc: '校規、申請辦法與常見問題' },
  { id: 'scholarship' as const, label: '獎學金', stage: 3, desc: '資格比對、草稿與缺件檢查' },
  { id: 'course' as const, label: '選課規劃', stage: 3, desc: '課表建議與衝堂避開' },
  { id: 'activity' as const, label: '校園活動', stage: 3, desc: '活動推薦、報名與行事曆' }
]

export const teacherFeatures = [
  { id: 'qa' as const, label: '導生關懷', stage: 2, desc: '導生預警與約談建議' },
  { id: 'scholarship' as const, label: '門檻提醒', stage: 2, desc: '成績與畢業門檻追蹤' }
]

export const adminFeatures = [
  { id: 'qa' as const, label: '校務研究', stage: 1, desc: '校務統計摘要' },
  { id: 'scholarship' as const, label: '決策支援', stage: 1, desc: '資源配置建議' }
]

export function featuresForRole(role?: string | null) {
  if (role === 'teacher') return teacherFeatures
  if (role === 'admin') return adminFeatures
  return studentFeatures
}

export function labelForFeature(id: string) {
  const all = [...studentFeatures, ...teacherFeatures, ...adminFeatures]
  return all.find(f => f.id === id)?.label ?? id
}

export function roleLabelOf(role?: string | null) {
  const map: Record<string, string> = { student: '學生', teacher: '教師', admin: '行政' }
  return role ? map[role] ?? role : ''
}
