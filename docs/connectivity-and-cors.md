# Frontend API Connectivity and CORS

## Purpose

FijiLaw AI uses a split deployment model:

```text
Browser
  |
  v
Vercel - Next.js frontend
  |
  | HTTPS fetch
  v
Railway - ASP.NET Core API
```

The browser enforces Cross-Origin Resource Sharing (CORS). If the API does not explicitly trust the frontend origin, requests can fail in the browser with a generic message such as `Failed to fetch`, even when the Railway API itself is healthy.

## Production endpoints

Frontend production alias:

```text
https://fijilaw-ai-pasifika-solutions.vercel.app
```

Backend production API:

```text
https://fijilaw-api-production-production.up.railway.app
```

Backend health endpoint:

```text
GET /health
```

The web application should use:

```text
NEXT_PUBLIC_API_URL=https://fijilaw-api-production-production.up.railway.app
```

## CORS policy

The API does not allow arbitrary origins.

Allowed origins are:

1. Origins explicitly supplied through `WebOrigin` or `AllowedWebOrigins`.
2. Local development origins using `http://localhost` or `http://127.0.0.1`.
3. The FijiLaw AI production Vercel alias.
4. FijiLaw AI preview deployments matching both:
   - hostname starts with `fijilaw-`
   - hostname ends with `-pasifika-solutions.vercel.app`

This intentionally avoids a broad `*.vercel.app` rule.

### Railway variables

Recommended production configuration:

```text
WebOrigin=https://fijilaw-ai-pasifika-solutions.vercel.app
AllowedWebOrigins=https://fijilaw-ai-pasifika-solutions.vercel.app
```

Multiple manually approved origins may be supplied as a comma-separated value in `AllowedWebOrigins`.

## Frontend health detection

The frontend performs a request to:

```text
GET {NEXT_PUBLIC_API_URL}/health
```

and tracks one of three states:

- `checking`
- `online`
- `offline`

When the API is online, the header displays `Service online`.

When the API cannot be reached, the page displays a visible recovery banner and disables legal triage and document analysis until the service becomes reachable again.

The user-visible error is intentionally different from the browser's `Failed to fetch` message:

> FijiLaw AI is temporarily unable to reach the legal service. Your information has not been submitted. Please try again shortly.

This wording is important because it tells the user that the request did not reach the legal service and avoids implying that case information was successfully submitted.

## Request failure behavior

### Legal triage

If `/api/legal/triage` cannot be reached:

- the current result is not replaced with fabricated output;
- the UI marks the API as offline;
- the user's text remains in the form;
- the UI states that the information was not submitted;
- the user can retry after connectivity is restored.

### Document analysis

If `/api/legal/documents/analyse` cannot be reached:

- the selected file is not assumed to have been uploaded;
- the UI states that the legal service is unavailable;
- the file remains selected locally in the browser;
- the user can retry after connectivity returns.

### Legal-services directory

If `/api/legal-services` fails:

- no fake directory results are generated;
- the UI displays a service-unavailable message;
- filters remain available once data can be reloaded.

## Troubleshooting `Failed to fetch`

Check these items in order.

### 1. Confirm Railway is healthy

Open:

```text
https://fijilaw-api-production-production.up.railway.app/health
```

Expected response includes:

```json
{
  "status": "ok",
  "service": "FijiLaw.Api",
  "connectivity": "ready"
}
```

### 2. Confirm the frontend API URL

Vercel should expose:

```text
NEXT_PUBLIC_API_URL=https://fijilaw-api-production-production.up.railway.app
```

If this changes, rebuild the Vercel frontend because `NEXT_PUBLIC_*` variables are embedded into the client bundle during build.

### 3. Confirm the browser origin is allowed

The production alias should remain:

```text
https://fijilaw-ai-pasifika-solutions.vercel.app
```

For additional custom domains, add the exact HTTPS origin to `AllowedWebOrigins` in Railway.

### 4. Check browser developer tools

Typical CORS symptoms include:

```text
blocked by CORS policy
No 'Access-Control-Allow-Origin' header
Failed to fetch
```

A server-side 400/500 response is different: the request reached the API and should be investigated through Railway HTTP/deploy logs.

### 5. Confirm HTTPS

Production browser requests must use HTTPS for both Vercel and Railway. Do not mix an HTTPS frontend with a plain-HTTP production API.

## Security notes

- Do not use `AllowAnyOrigin()` for the production legal API.
- Do not add `*.vercel.app` as an unrestricted wildcard.
- Do not expose `ADMIN_API_KEY` or `OPENAI_API_KEY` to the frontend.
- `NEXT_PUBLIC_API_URL` is safe to expose because it contains only the public API address.
- Any future authenticated endpoints should use authorization independently of CORS; CORS is not an authentication mechanism.

## Future improvements

Planned improvements should include:

- authenticated case APIs;
- request correlation IDs surfaced in frontend errors;
- central application telemetry;
- uptime monitoring for `/health`;
- retry/backoff for idempotent GET requests;
- custom production domains for both web and API;
- rate limiting and abuse protection;
- structured client-side error categories rather than generic network errors.
