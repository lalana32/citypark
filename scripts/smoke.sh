#!/usr/bin/env bash
# Smoke test against the running app profile: health, register, login, create a reservation.
# Usage: docker compose --profile app up --build -d --wait && bash scripts/smoke.sh [base-url]
set -euo pipefail

BASE_URL="${1:-http://localhost:${APP_PORT:-8080}}"
EMAIL="smoke-$(date +%s)-$RANDOM@example.test"
PASSWORD="Smoke-Pass-2026"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
JAR="$WORK/cookies"
BODY="$WORK/body"

fail() {
  echo "FAIL: $1" >&2
  cat "$BODY" >&2 2>/dev/null || true
  echo >&2
  exit 1
}

# Sends a request with the cookie jar; prints the status code, the body goes to $BODY.
call() {
  curl -sS -o "$BODY" -w '%{http_code}' -b "$JAR" -c "$JAR" "$@"
}

expect() {
  local expected="$1" step="$2"
  shift 2
  local status
  status="$(call "$@")"
  [[ "$status" == "$expected" ]] || fail "$step: expected $expected, got $status"
  echo "ok   $step ($status)"
}

# The antiforgery token is bound to the signed-in user, so a new one is fetched before every POST.
csrf_token() {
  [[ "$(call "$BASE_URL/api/v1/auth/csrf")" == "200" ]] || fail "csrf token"
  sed -n 's/.*"token":"\([^"]*\)".*/\1/p' "$BODY"
}

post_json() {
  local url="$1" json="$2"
  local token
  token="$(csrf_token)"
  call -X POST "$url" -H 'Content-Type: application/json' -H "X-XSRF-TOKEN: $token" --data "$json"
}

expect_post() {
  local expected="$1" step="$2" url="$3" json="$4"
  local status
  status="$(post_json "$url" "$json")"
  [[ "$status" == "$expected" ]] || fail "$step: expected $expected, got $status"
  echo "ok   $step ($status)"
}

# Tomorrow 09:00-10:00 UTC: on the 15-minute grid and inside every demo lot's opening hours.
tomorrow() { date -u -d '+1 day' "+%Y-%m-%d" 2>/dev/null || date -u -v+1d "+%Y-%m-%d"; }
DAY="$(tomorrow)"
START="${DAY}T09:00:00Z"
END="${DAY}T10:00:00Z"

echo "Smoke test against $BASE_URL"

expect 200 "health ready" "$BASE_URL/health/ready"

expect 200 "list lots" "$BASE_URL/api/v1/lots"
LOT_ID="$(sed -n 's/.*"items":\[{"id":"\([^"]*\)".*/\1/p' "$BODY")"
[[ -n "$LOT_ID" ]] || fail "no lot in the list (is DemoData__Enabled set?)"

expect_post 201 "register" "$BASE_URL/api/v1/auth/register" "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}"
expect_post 200 "login" "$BASE_URL/api/v1/auth/login" "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}"
grep -q 'citypark.auth' "$JAR" || fail "login set no session cookie"

expect 200 "me (cookie sent back)" "$BASE_URL/api/v1/auth/me"

expect_post 201 "create reservation" "$BASE_URL/api/v1/reservations" \
  "{\"lotId\":\"$LOT_ID\",\"start\":\"$START\",\"end\":\"$END\",\"plate\":\"A12K345\"}"

expect 200 "list own reservations" "$BASE_URL/api/v1/reservations"
grep -q 'A12K345' "$BODY" || fail "the new reservation is not in the list"

echo "Smoke test passed."
