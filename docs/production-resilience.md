# Production Connectivity and Resilience

This document defines how FijiLaw AI should handle frontend-to-API connectivity in production.

## Production topology

```text
Vercel (Next.js frontend)
        |
        | HTTPS
        v
Railway (ASP.NET Core API)
        |
        +-- /health
        +-- /api/legal/triage
        +-- /api/legal/documents/analyse
        +-- /api/legal-services
```

Current production API domain:

```text
https://fijilaw-api-production-production.up.railway.app
```

Primary production frontend origin:

```text
https://fijilaw-ai-pasifika-solutions.vercel.app
```

## CORS policy

The API must never use `AllowAnyOrigin()` in production.

Allowed origins are limited to:

1. Explicit origins configured with `WebOrigin` or `AllowedWebOrigins`.
2. `http://localhost` and `http://127.0.0.1` for local development.
3. The stable FijiLaw Vercel production domain.
4. FijiLaw Vercel preview deployments matching both the FijiLaw project prefix and the Pasifika Solutions team suffix.

Arbitrary `*.vercel.app` origins are not trusted.

## Health checking

The frontend checks:

```text
GET /health
```

The API returns a JSON object with `status: "ok"` when the application is available. The frontend checks at startup and approximately once per minute.

The health endpoint is also Railway's deployment health check.

## User-facing failure behaviour

Raw browser messages such as:

```text
Failed to fetch
```

must not be shown directly to end users.

Network failures are normalized to:

> FijiLaw AI is temporarily unable to reach the legal service. Your information has not been submitted. Please try again shortly.

A persistent availability banner is shown while the API health check is failing.

For request timeouts, the user receives a specific timeout message instead of a generic browser exception.

## Privacy behaviour during failures

If a network request fails before the API receives it, the interface states that the user's information has not been submitted. The frontend must not imply that a legal assessment was created when the request did not complete.

Document uploads must continue to follow the MVP rule: processing occurs in memory through the analysis endpoint and the endpoint does not intentionally persist uploaded files.

## Deployment configuration

### Railway

Production service:

```text
fijilaw-api-production
```

Required configuration:

```text
WebOrigin=https://fijilaw-ai-pasifika-solutions.vercel.app
```

Additional production/custom origins can be supplied as a comma-separated value through:

```text
AllowedWebOrigins=https://example1,https://example2
```

Keep `/health` configured as the Railway health-check path.

### Vercel

The frontend should define:

```text
NEXT_PUBLIC_API_URL=https://fijilaw-api-production-production.up.railway.app
```

The source code has the same URL as a fallback for the MVP, but production deployments should prefer an explicit environment variable so infrastructure can be changed without a code modification.

## Operational checklist

Before promoting a new frontend or API release:

1. Confirm Railway production service is `SUCCESS`.
2. Open `/health` and confirm `status` is `ok`.
3. Confirm the Vercel production origin is included in the API CORS allow-list.
4. Confirm `NEXT_PUBLIC_API_URL` points to the production Railway domain.
5. Submit one test legal-triage request from the deployed frontend.
6. Load the legal-services directory.
7. Test one small TXT/DOCX/PDF document where permitted.
8. Confirm no raw `Failed to fetch` message is displayed when the API is intentionally unavailable.
9. Confirm the availability banner disappears after connectivity is restored.

## Incident response

If users report connectivity failures:

1. Check Railway service status and deployment logs.
2. Check `/health` directly.
3. Check the current Vercel production domain/alias.
4. Check `WebOrigin` and `AllowedWebOrigins`.
5. Check `NEXT_PUBLIC_API_URL` in Vercel.
6. Verify TLS/HTTPS on both sides.
7. Redeploy the API only after configuration is corrected.
8. Roll back the frontend if a new frontend release introduced the failure.

## Future improvements

- Centralized structured logging for request IDs and failures.
- Synthetic uptime monitoring against `/health`.
- Error-rate dashboards and alerting.
- Retry logic only for safe idempotent GET requests.
- Custom production domains such as `app.fijilawlink.com` and `api.fijilawlink.com`.
- Content Security Policy and stricter browser security headers.
