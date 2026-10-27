#!/usr/bin/env bash
set -Eeuo pipefail

command_name="${1:-help}"
database_name="${CINECLAY_DB_NAME:-cineclay}"
schema_file="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/src/Cineclay.Api/schema.sql"

require_psql() {
  command -v psql >/dev/null 2>&1 || {
    echo "psql is required. Install the PostgreSQL client first." >&2
    exit 1
  }
}

case "$command_name" in
  create)
    require_psql
    exists="$(psql --dbname postgres --tuples-only --no-align --set ON_ERROR_STOP=1 --command "SELECT 1 FROM pg_database WHERE datname = :'database_name'" --set "database_name=$database_name")"
    if [[ "$exists" != "1" ]]; then
      createdb "$database_name"
      echo "Created PostgreSQL database: $database_name"
    else
      echo "Database already exists: $database_name"
    fi
    ;;
  migrate)
    require_psql
    psql --dbname "$database_name" --set ON_ERROR_STOP=1 --file "$schema_file"
    echo "Applied schema to: $database_name"
    ;;
  setup)
    "$0" create
    "$0" migrate
    ;;
  verify)
    require_psql
    psql --dbname "$database_name" --set ON_ERROR_STOP=1 --command "SELECT (SELECT count(*) FROM movies) AS movies, (SELECT count(*) FROM albums) AS albums, (SELECT count(*) FROM songs) AS songs;"
    ;;
  *)
    echo "Usage: $0 {create|migrate|setup|verify}"
    echo "Connection: standard PGHOST, PGPORT, PGUSER and PGPASSWORD variables."
    echo "Database: CINECLAY_DB_NAME (default: cineclay)."
    ;;
esac
