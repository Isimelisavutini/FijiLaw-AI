# Deployment

## Web — Vercel
Set the Vercel project root directory to `src/FijiLaw.Web`.

Environment variable:

```text
NEXT_PUBLIC_API_URL=https://<your-api-host>
```

Vercel will use `npm run build` for the Next.js application.

## API — Azure App Service / Container Apps
Deploy `src/FijiLaw.Api` using .NET 8.

Configuration:

```text
WebOrigin=https://<your-vercel-domain>
```

For production, configure database, storage, AI provider, telemetry, identity, and secrets through managed cloud configuration. Never commit credentials.

## Database
For local development:

```bash
docker compose up -d
```

This starts PostgreSQL 16 with pgvector and initializes the legal-source/audit schema.

For production, use managed PostgreSQL or Azure AI Search according to the final retrieval design. Require encryption, backups, restricted networking, and least-privilege credentials.

## Local run

Terminal 1:
```bash
dotnet run --project src/FijiLaw.Api --urls http://localhost:5000
```

Terminal 2:
```bash
cd src/FijiLaw.Web
npm install
npm run dev
```

Open `http://localhost:3000`.

## Production gate
Do not expose the system as a production legal service until authoritative Fiji-law retrieval, authentication/authorization, privacy controls, audit persistence, monitoring, security testing, and qualified legal review are in place.
