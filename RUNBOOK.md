# Runbook

## Local secrets
- Copy `.env.example` to `.env` and fill real values before running Docker Compose.
- For local .NET runs, prefer user secrets from `Chat_API/Chat_API`:
  - `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=db_co_pgvector;Username=postgres;Password=<password>"`
  - `dotnet user-secrets set "AiSettings:ApiKey" "<gemini-api-key>"`
  - `dotnet user-secrets set "AuthSettings:JwtKey" "<long-random-jwt-key>"`

## Restart
- `docker compose -f Chat_API/compose.yml up -d --build`

## Rollback
- Deploy previous image tag and restart service:
  - `docker compose -f Chat_API/compose.yml up -d`

## Backup Postgres
- `docker exec chatbot_postgres pg_dump -U admin chatbotdb > backup.sql`

## Restore Postgres
- `cat backup.sql | docker exec -i chatbot_postgres psql -U admin -d chatbotdb`
