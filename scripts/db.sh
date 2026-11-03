#!/usr/bin/env bash
set -euo pipefail

taskapi_root="$(cd "$(dirname "$0")/.." && pwd)"
taskapi_compose=(docker compose --project-directory "$taskapi_root" -f "$taskapi_root/compose.yaml")

case "${1:-status}" in
    start)
        "${taskapi_compose[@]}" up -d --wait postgres
        ;;
    stop)
        "${taskapi_compose[@]}" stop postgres
        ;;
    status)
        "${taskapi_compose[@]}" ps postgres
        ;;
    logs)
        "${taskapi_compose[@]}" logs --tail=100 postgres
        ;;
    *)
        echo 'Usage: bash scripts/db.sh {start|stop|status|logs}' >&2
        exit 1
        ;;
esac
