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

export interface ContextWindowTurn {
  tokens: number
  model: string | null
}

export interface SubAgentSpan {
  agentId: string
  role: string
  failed: boolean
  startedAt: string | null
  endedAt: string | null
  durationMs: number
  models: string[]
  unpricedModels: string[]
  tokens: TokenBreakdown | null
  costUsd: number | null
  contextWindow: ContextWindowTurn[]
}

export interface CombinedTotals {
  costUsd: number
  tokens: TokenBreakdown
  models: string[]
  unpricedModels: string[]
}

export interface SessionTrace {
  session: SessionSummary
  contextWindow: ContextWindowTurn[]
  subAgents: SubAgentSpan[]
  combined: CombinedTotals
}

export interface SessionListItem {
  sessionId: string
  project: string
  failed: boolean
  summary: SessionSummary | null
  combined: CombinedTotals | null
}
