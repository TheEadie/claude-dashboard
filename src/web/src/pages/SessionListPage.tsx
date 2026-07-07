import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import CircularProgress from '@mui/material/CircularProgress'
import Paper from '@mui/material/Paper'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import Typography from '@mui/material/Typography'
import { fetchSessions } from '../api'
import type { SessionListItem } from '../types'

type LoadState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; items: SessionListItem[] }

function formatUsd2(value: number): string {
  return `$${value.toFixed(2)}`
}

export default function SessionListPage() {
  const navigate = useNavigate()
  const [state, setState] = useState<LoadState>({ status: 'loading' })

  useEffect(() => {
    let cancelled = false
    setState({ status: 'loading' })

    fetchSessions()
      .then((items) => {
        if (cancelled) return
        setState({ status: 'ready', items })
      })
      .catch((error: unknown) => {
        if (cancelled) return
        setState({ status: 'error', message: error instanceof Error ? error.message : 'Unknown error' })
      })

    return () => {
      cancelled = true
    }
  }, [])

  if (state.status === 'loading') {
    return (
      <Box display="flex" justifyContent="center" p={4}>
        <CircularProgress />
      </Box>
    )
  }

  if (state.status === 'error') {
    return (
      <Box p={4}>
        <Alert severity="error">Something went wrong loading sessions: {state.message}</Alert>
      </Box>
    )
  }

  const { items } = state

  if (items.length === 0) {
    return (
      <Box p={4}>
        <Typography>No sessions found under ~/.claude/projects.</Typography>
      </Box>
    )
  }

  const hasUnpriced = items.some((item) => (item.combined?.unpricedModels.length ?? 0) > 0)

  return (
    <Box p={4}>
      <Typography variant="h4" gutterBottom>
        Sessions
      </Typography>
      <Paper variant="outlined">
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Started</TableCell>
              <TableCell>Title</TableCell>
              <TableCell>Project</TableCell>
              <TableCell>Models</TableCell>
              <TableCell>Tokens</TableCell>
              <TableCell>Cost</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {items.map((item) => (
              <TableRow
                key={item.sessionId}
                hover
                sx={{ cursor: 'pointer' }}
                onClick={() => navigate(`/session/${item.sessionId}`)}
              >
                {!item.failed && item.summary && item.combined ? (
                  <>
                    <TableCell>
                      {item.summary.startedAt ? new Date(item.summary.startedAt).toLocaleString() : ''}
                    </TableCell>
                    <TableCell>{item.summary.title}</TableCell>
                    <TableCell>{item.summary.project}</TableCell>
                    <TableCell>{item.combined.models.join(', ')}</TableCell>
                    <TableCell>{item.combined.tokens.total.toLocaleString()}</TableCell>
                    <TableCell>
                      {formatUsd2(item.combined.costUsd)}
                      {item.combined.unpricedModels.length > 0 && (
                        <Typography
                          component="span"
                          color="warning.main"
                          title="cost is understated — the session used a model with no price in the table"
                        >
                          {' '}
                          *
                        </Typography>
                      )}
                    </TableCell>
                  </>
                ) : (
                  <>
                    <TableCell />
                    <TableCell>{item.sessionId}</TableCell>
                    <TableCell>{item.project}</TableCell>
                    <TableCell colSpan={3}>
                      <Chip size="small" color="error" label="failed to load" />
                    </TableCell>
                  </>
                )}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>
      {hasUnpriced && (
        <Typography variant="caption" color="text.secondary">
          * cost is understated — the session used a model with no price in the table.
        </Typography>
      )}
    </Box>
  )
}
