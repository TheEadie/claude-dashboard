// Bundled, hardcoded per-model context-window sizes (tokens). Not user-
// configurable in this story (see #6). Unknown/unpriced models fall back to
// DEFAULT_WINDOW_SIZE. Mirrors the model set in Dashboard.Api/Pricing/PriceTable.cs.
export const DEFAULT_WINDOW_SIZE = 200_000

const WINDOW_SIZES: Record<string, number> = {
  'claude-opus-4-8': 200_000,
  'claude-opus-4-7': 200_000,
  'claude-opus-4-6': 200_000,
  'claude-sonnet-5': 200_000,
  'claude-sonnet-4-6': 200_000,
  'claude-fable-5': 200_000,
  'claude-haiku-4-5': 200_000,
}

export function windowSizeFor(model: string | null): number {
  return (model != null ? WINDOW_SIZES[model] : undefined) ?? DEFAULT_WINDOW_SIZE
}
