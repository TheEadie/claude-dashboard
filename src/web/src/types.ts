// Mirrors Dashboard.Api's Models/SessionContracts.cs (the star contract for
// GET /api/sessions/{sessionId}). Keep field names in lockstep with the API.

export interface TokenBreakdown {
  input: number
  output: number
  cacheWrite: number
  cacheRead: number
  total: number
}

export interface SessionSummary {
  sessionId: string
  project: string
  title: string
  startedAt: string | null
  endedAt: string | null
  durationMs: number
  models: string[]
  unpricedModels: string[]
  tokens: TokenBreakdown
  costUsd: number
}

export interface SessionListItem {
  sessionId: string
  project: string
  failed: boolean
  summary: SessionSummary | null
}
