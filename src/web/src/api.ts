import type { SessionListItem, SessionTrace } from './types'

export type FetchSessionResult =
  | { ok: true; data: SessionTrace }
  | { ok: false }

export async function fetchSession(sessionId: string): Promise<FetchSessionResult> {
  const response = await fetch(`/api/sessions/${encodeURIComponent(sessionId)}`)

  if (response.status === 404) {
    return { ok: false }
  }

  if (!response.ok) {
    throw new Error(`Unexpected response fetching session ${sessionId}: ${response.status}`)
  }

  const data = (await response.json()) as SessionTrace
  return { ok: true, data }
}

export async function fetchSessions(): Promise<SessionListItem[]> {
  const response = await fetch('/api/sessions')
  if (!response.ok) {
    throw new Error(`Unexpected response fetching sessions: ${response.status}`)
  }
  return (await response.json()) as SessionListItem[]
}
