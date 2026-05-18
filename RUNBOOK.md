# Runbook

## Restart
- `docker compose -f Chat_API/compose.yml up -d --build`

## Rollback
- Deploy previous image tag and restart service:
  - `docker compose -f Chat_API/compose.yml up -d`

## Backup Postgres
- `docker exec chatbot_postgres pg_dump -U admin chatbotdb > backup.sql`

## Restore Postgres
- `cat backup.sql | docker exec -i chatbot_postgres psql -U admin -d chatbotdb`
