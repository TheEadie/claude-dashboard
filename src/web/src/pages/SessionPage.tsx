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
import type { SessionSummary } from '../types'

type LoadState =
  | { status: 'loading' }
  | { status: 'not-found' }
  | { status: 'error'; message: string }
  | { status: 'ready'; summary: SessionSummary }

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

export default function SessionPage() {
  const { id } = useParams<{ id: string }>()
  const [state, setState] = useState<LoadState>({ status: 'loading' })

  useEffect(() => {
    if (!id) {
      return
    }

    let cancelled = false
    setState({ status: 'loading' })

    fetchSession(id)
      .then((result) => {
        if (cancelled) return
        setState(result.ok ? { status: 'ready', summary: result.data } : { status: 'not-found' })
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

  const { summary } = state

  return (
    <Box p={4}>
      <Stack spacing={3}>
        <Box>
          <Typography variant="h4">{summary.title}</Typography>
          <Typography variant="body2" color="text.secondary">
            {summary.project} &middot; {summary.sessionId}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {summary.startedAt ?? 'unknown start'} &rarr; {summary.endedAt ?? 'unknown end'} (
            {formatDuration(summary.durationMs)})
          </Typography>
        </Box>

        <Box>
          <Typography variant="h6" gutterBottom>
            Models
          </Typography>
          <Stack direction="row" spacing={1}>
            {summary.models.map((model) => {
              const unpriced = summary.unpricedModels.includes(model)
              return (
                <Chip
                  key={model}
                  label={unpriced ? `${model} (unpriced)` : model}
                  color={unpriced ? 'warning' : 'default'}
                />
              )
            })}
          </Stack>
        </Box>

        <Box>
          <Typography variant="h6" gutterBottom>
            Token breakdown
          </Typography>
          <Paper variant="outlined">
            <Table size="small">
              <TableBody>
                <TableRow>
                  <TableCell>Input</TableCell>
                  <TableCell align="right">{summary.tokens.input.toLocaleString()}</TableCell>
                </TableRow>
                <TableRow>
                  <TableCell>Output</TableCell>
                  <TableCell align="right">{summary.tokens.output.toLocaleString()}</TableCell>
                </TableRow>
                <TableRow>
                  <TableCell>Cache write</TableCell>
                  <TableCell align="right">{summary.tokens.cacheWrite.toLocaleString()}</TableCell>
                </TableRow>
                <TableRow>
                  <TableCell>Cache read</TableCell>
                  <TableCell align="right">{summary.tokens.cacheRead.toLocaleString()}</TableCell>
                </TableRow>
                <TableRow>
                  <TableCell>
                    <strong>Total</strong>
                  </TableCell>
                  <TableCell align="right">
                    <strong>{summary.tokens.total.toLocaleString()}</strong>
                  </TableCell>
                </TableRow>
              </TableBody>
            </Table>
          </Paper>
        </Box>

        <Box>
          <Typography variant="h6" gutterBottom>
            Estimated cost
          </Typography>
          <Typography variant="h5">{formatUsd(summary.costUsd)}</Typography>
        </Box>
      </Stack>
    </Box>
  )
}
