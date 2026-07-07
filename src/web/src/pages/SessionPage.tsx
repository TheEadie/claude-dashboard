import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import CircularProgress from '@mui/material/CircularProgress'
import Paper from '@mui/material/Paper'
import Stack from '@mui/material/Stack'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableRow from '@mui/material/TableRow'
import Typography from '@mui/material/Typography'
import { fetchSession } from '../api'
import type { SessionTrace, TokenBreakdown } from '../types'

type LoadState =
  | { status: 'loading' }
  | { status: 'not-found' }
  | { status: 'error'; message: string }
  | { status: 'ready'; trace: SessionTrace }

/** Uniform view-model shared by the timeline and the details panel. */
interface Span {
  id: string // '__main__' for the session, else agentId
  label: string // session title for main, role for sub-agents
  isMain: boolean
  failed: boolean
  startedAt: string | null
  endedAt: string | null
  durationMs: number
  models: string[]
  unpricedModels: string[]
  tokens: TokenBreakdown | null
  costUsd: number | null
}

const MAIN_SPAN_ID = '__main__'

function formatDuration(durationMs: number): string {
  const totalSeconds = Math.round(durationMs / 1000)
  const hours = Math.floor(totalSeconds / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60
  const parts = []
  if (hours > 0) parts.push(`${hours}h`)
  if (hours > 0 || minutes > 0) parts.push(`${minutes}m`)
  parts.push(`${seconds}s`)
  return parts.join(' ')
}

function formatUsd(value: number): string {
  return `$${value.toFixed(4)}`
}

function toSpans(trace: SessionTrace): Span[] {
  const mainSpan: Span = {
    id: MAIN_SPAN_ID,
    label: trace.session.title,
    isMain: true,
    failed: false,
    startedAt: trace.session.startedAt,
    endedAt: trace.session.endedAt,
    durationMs: trace.session.durationMs,
    models: trace.session.models,
    unpricedModels: trace.session.unpricedModels,
    tokens: trace.session.tokens,
    costUsd: trace.session.costUsd,
  }

  const subSpans: Span[] = trace.subAgents.map((s) => ({
    id: s.agentId,
    label: s.role,
    isMain: false,
    failed: s.failed,
    startedAt: s.startedAt,
    endedAt: s.endedAt,
    durationMs: s.durationMs,
    models: s.models,
    unpricedModels: s.unpricedModels,
    tokens: s.tokens,
    costUsd: s.costUsd,
  }))

  // Sub-agents ordered by start time ascending; undated/failed spans (no
  // startedAt) sorted to the end, stable otherwise.
  const orderedSubSpans = [...subSpans].sort((a, b) => {
    if (a.startedAt && b.startedAt) {
      return new Date(a.startedAt).getTime() - new Date(b.startedAt).getTime()
    }
    if (a.startedAt) return -1
    if (b.startedAt) return 1
    return 0
  })

  return [mainSpan, ...orderedSubSpans]
}

function ModelChips({ models, unpricedModels }: { models: string[]; unpricedModels: string[] }) {
  return (
    <Stack direction="row" spacing={1}>
      {models.map((model) => {
        const unpriced = unpricedModels.includes(model)
        return (
          <Chip
            key={model}
            label={unpriced ? `${model} (unpriced)` : model}
            color={unpriced ? 'warning' : 'default'}
          />
        )
      })}
    </Stack>
  )
}

function SpanDetails({ span }: { span: Span }) {
  if (span.failed) {
    return (
      <Stack spacing={2}>
        <Typography variant="h6">{span.label}</Typography>
        <Alert severity="error">failed to load — no stats available</Alert>
      </Stack>
    )
  }

  return (
    <Stack spacing={3}>
      <Typography variant="h6">{span.label}</Typography>

      <Box>
        <Typography variant="subtitle2" gutterBottom>
          Models
        </Typography>
        <ModelChips models={span.models} unpricedModels={span.unpricedModels} />
      </Box>

      <Box>
        <Typography variant="body2" color="text.secondary">
          {span.startedAt && span.endedAt
            ? `${span.startedAt} → ${span.endedAt} (${formatDuration(span.durationMs)})`
            : 'no timestamps'}
        </Typography>
      </Box>

      <Box>
        <Typography variant="subtitle2" gutterBottom>
          Token breakdown
        </Typography>
        <Paper variant="outlined">
          <Table size="small">
            <TableBody>
              <TableRow>
                <TableCell>Input</TableCell>
                <TableCell align="right">{span.tokens?.input.toLocaleString() ?? 0}</TableCell>
              </TableRow>
              <TableRow>
                <TableCell>Output</TableCell>
                <TableCell align="right">{span.tokens?.output.toLocaleString() ?? 0}</TableCell>
              </TableRow>
              <TableRow>
                <TableCell>Cache write</TableCell>
                <TableCell align="right">{span.tokens?.cacheWrite.toLocaleString() ?? 0}</TableCell>
              </TableRow>
              <TableRow>
                <TableCell>Cache read</TableCell>
                <TableCell align="right">{span.tokens?.cacheRead.toLocaleString() ?? 0}</TableCell>
              </TableRow>
              <TableRow>
                <TableCell>
                  <strong>Total</strong>
                </TableCell>
                <TableCell align="right">
                  <strong>{span.tokens?.total.toLocaleString() ?? 0}</strong>
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </Paper>
      </Box>

      <Box>
        <Typography variant="subtitle2" gutterBottom>
          Estimated cost
        </Typography>
        <Typography variant="h5">{formatUsd(span.costUsd ?? 0)}</Typography>
      </Box>
    </Stack>
  )
}

function Timeline({
  spans,
  selected,
  onSelect,
}: {
  spans: Span[]
  selected: string
  onSelect: (id: string) => void
}) {
  const datable = spans.filter((s) => s.startedAt && s.endedAt)

  if (datable.length === 0) {
    return (
      <Stack spacing={1}>
        {spans.map((s) => (
          <Box
            key={s.id}
            onClick={() => onSelect(s.id)}
            sx={{
              p: 1,
              cursor: 'pointer',
              border: '1px solid',
              borderColor: selected === s.id ? 'primary.main' : 'divider',
              bgcolor: selected === s.id ? 'action.selected' : 'background.paper',
            }}
          >
            <Typography>
              {s.label}
              {s.failed && <Chip size="small" color="error" label="failed to load" sx={{ ml: 1 }} />}
            </Typography>
          </Box>
        ))}
      </Stack>
    )
  }

  const axisMin = Math.min(...datable.map((s) => new Date(s.startedAt!).getTime()))
  const axisMax = Math.max(...datable.map((s) => new Date(s.endedAt!).getTime()))
  const axisSpan = axisMax - axisMin === 0 ? 1 : axisMax - axisMin

  const undatedSpans = spans.filter((s) => !(s.startedAt && s.endedAt))

  return (
    <Stack spacing={2}>
      <Stack spacing={1}>
        {datable.map((s) => {
          const start = new Date(s.startedAt!).getTime()
          const end = new Date(s.endedAt!).getTime()
          const left = ((start - axisMin) / axisSpan) * 100
          const width = Math.max(((end - start) / axisSpan) * 100, 0.5)
          return (
            <Stack key={s.id} direction="row" spacing={2} alignItems="center">
              <Box sx={{ width: 200, flexShrink: 0 }}>
                <Typography variant="body2" noWrap title={s.label}>
                  {s.label}
                  {s.failed && <Chip size="small" color="error" label="failed to load" sx={{ ml: 1 }} />}
                </Typography>
              </Box>
              <Box
                sx={{
                  position: 'relative',
                  flexGrow: 1,
                  height: 24,
                  bgcolor: 'action.hover',
                }}
              >
                <Box
                  onClick={() => onSelect(s.id)}
                  sx={{
                    position: 'absolute',
                    left: `${left}%`,
                    width: `${width}%`,
                    height: '100%',
                    cursor: 'pointer',
                    bgcolor: selected === s.id ? 'primary.main' : 'primary.light',
                    border: selected === s.id ? '2px solid' : 'none',
                    borderColor: 'primary.dark',
                  }}
                />
              </Box>
            </Stack>
          )
        })}
      </Stack>

      {undatedSpans.length > 0 && (
        <Box>
          <Typography variant="subtitle2" gutterBottom>
            Undated
          </Typography>
          <Stack spacing={1}>
            {undatedSpans.map((s) => (
              <Box
                key={s.id}
                onClick={() => onSelect(s.id)}
                sx={{
                  p: 1,
                  cursor: 'pointer',
                  border: '1px solid',
                  borderColor: selected === s.id ? 'primary.main' : 'divider',
                  bgcolor: selected === s.id ? 'action.selected' : 'background.paper',
                }}
              >
                <Typography variant="body2">
                  {s.label}{' '}
                  <Chip size="small" color={s.failed ? 'error' : 'default'} label={s.failed ? 'failed to load' : 'no timestamps'} />
                </Typography>
              </Box>
            ))}
          </Stack>
        </Box>
      )}
    </Stack>
  )
}

export default function SessionPage() {
  const { id } = useParams<{ id: string }>()
  const [state, setState] = useState<LoadState>({ status: 'loading' })
  const [selected, setSelected] = useState<string>(MAIN_SPAN_ID)

  useEffect(() => {
    if (!id) {
      return
    }

    let cancelled = false
    setState({ status: 'loading' })
    setSelected(MAIN_SPAN_ID)

    fetchSession(id)
      .then((result) => {
        if (cancelled) return
        setState(result.ok ? { status: 'ready', trace: result.data } : { status: 'not-found' })
      })
      .catch((error: unknown) => {
        if (cancelled) return
        setState({ status: 'error', message: error instanceof Error ? error.message : 'Unknown error' })
      })

    return () => {
      cancelled = true
    }
  }, [id])

  if (state.status === 'loading') {
    return (
      <Box display="flex" justifyContent="center" p={4}>
        <CircularProgress />
      </Box>
    )
  }

  if (state.status === 'not-found') {
    return (
      <Box p={4}>
        <Alert severity="error">No session with that id was found.</Alert>
      </Box>
    )
  }

  if (state.status === 'error') {
    return (
      <Box p={4}>
        <Alert severity="error">Something went wrong loading this session: {state.message}</Alert>
      </Box>
    )
  }

  const { trace } = state
  const allSpans = toSpans(trace)
  const selectedSpan = allSpans.find((s) => s.id === selected) ?? allSpans[0]
  const mainCostUsd = trace.session.costUsd
  // Derived as a difference of two JSON-deserialized decimals, so clamp away
  // floating-point residue (e.g. -1e-19 rendering as "$-0.0000").
  const subAgentsCostUsd = Math.max(0, trace.combined.costUsd - mainCostUsd)

  return (
    <Box p={4}>
      <Stack spacing={3}>
        <Box>
          <Typography variant="h4">{trace.session.title}</Typography>
          <Typography variant="body2" color="text.secondary">
            {trace.session.project} &middot; {trace.session.sessionId}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {trace.session.startedAt ?? 'unknown start'} &rarr; {trace.session.endedAt ?? 'unknown end'} (
            {formatDuration(trace.session.durationMs)})
          </Typography>
        </Box>

        <Paper variant="outlined" sx={{ p: 2 }}>
          <Typography variant="h6" gutterBottom>
            Combined totals
          </Typography>
          <Stack direction="row" spacing={4} alignItems="flex-start">
            <Box>
              <Typography variant="body2" color="text.secondary">
                Cost
              </Typography>
              <Typography variant="h5">{formatUsd(trace.combined.costUsd)}</Typography>
              <Typography variant="caption" color="text.secondary">
                main {formatUsd(mainCostUsd)} + sub-agents {formatUsd(subAgentsCostUsd)}
              </Typography>
            </Box>
            <Box>
              <Typography variant="body2" color="text.secondary">
                Total tokens
              </Typography>
              <Typography variant="h5">{trace.combined.tokens.total.toLocaleString()}</Typography>
            </Box>
            <Box>
              <Typography variant="body2" color="text.secondary" gutterBottom>
                Models
              </Typography>
              <ModelChips models={trace.combined.models} unpricedModels={trace.combined.unpricedModels} />
            </Box>
          </Stack>
        </Paper>

        <Box>
          <Typography variant="h6" gutterBottom>
            Timeline
          </Typography>
          <Timeline spans={allSpans} selected={selectedSpan.id} onSelect={setSelected} />
        </Box>

        <Box>
          <Typography variant="h6" gutterBottom>
            Details
          </Typography>
          <Paper variant="outlined" sx={{ p: 2 }}>
            <SpanDetails span={selectedSpan} />
          </Paper>
        </Box>
      </Stack>
    </Box>
  )
}
