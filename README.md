# claude-dashboard

A dashboard for viewing Claude Code session transcripts — token usage, cost, and models used per session.

It reads session transcripts directly from `~/.claude/projects/*/` (read-only — it never writes to or deletes anything under `~/.claude`) and serves a local web UI for browsing them, one session at a time.

## Prerequisites

- .NET 10 SDK
- Node.js 20+ and npm

## Run it

```sh
dotnet run --project src/Dashboard.Api
```

This builds the SPA (via an MSBuild target that runs `npm ci && npm run build` in `src/web`) into `src/Dashboard.Api/wwwroot`, then starts the API, which serves both the API and the built SPA on the same origin. Visit the URL printed on startup (e.g. `http://localhost:5103`), then navigate to `/session/<session-id>` for any session id present under your `~/.claude/projects/*/` directory.

### Manual SPA build (fallback)

If the automatic SPA build target doesn't run (e.g. offline, or `web/package.json` is missing), build it yourself first:

```sh
cd src/web
npm ci
npm run build
cd ../..
dotnet run --project src/Dashboard.Api
```

### Frontend development

For SPA hot-reload during development, run the API and the Vite dev server side by side:

```sh
dotnet run --project src/Dashboard.Api   # serves the API on http://localhost:5103
cd src/web && npm run dev                # serves the SPA on a Vite dev port, proxying /api to the API
```

## Tests

```sh
dotnet test ClaudeDashboard.slnx
```
