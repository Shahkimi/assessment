#!/usr/bin/env bash
# Blue/green releases for TaskFlow on Kubernetes (see README, section 5).
#
#   bluegreen.sh bootstrap <tag>           first install: infrastructure, migrations, blue colour
#   bluegreen.sh deploy <tag> [--switch]   migrate, deploy <tag> to the idle colour, smoke-test it
#   bluegreen.sh switch                    send live traffic to the idle colour
#   bluegreen.sh rollback                  send live traffic back to the previous colour
#   bluegreen.sh status                    show the live colour, deployments and autoscalers
#
# Uses the current kubectl context and the `taskflow` namespace.
# Image repositories can be overridden, e.g. for GHCR:
#   BACKEND_REPO=ghcr.io/<owner>/taskflow-backend FRONTEND_REPO=ghcr.io/<owner>/taskflow-frontend \
#     scripts/bluegreen.sh deploy sha-<commit>
set -euo pipefail

NS=taskflow
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
K8S_DIR="$ROOT/k8s"
BACKEND_REPO="${BACKEND_REPO:-taskflow-backend}"
FRONTEND_REPO="${FRONTEND_REPO:-taskflow-frontend}"
CURL_IMAGE="${CURL_IMAGE:-curlimages/curl:8.10.1}"
WAIT_SECONDS="${WAIT_SECONDS:-300}"

log() { printf '==> %s\n' "$*"; }
die() { printf 'error: %s\n' "$*" >&2; exit 1; }
k() { kubectl -n "$NS" "$@"; }

# kubectl.exe (Git Bash on Windows) needs a native path; elsewhere the path is already fine.
native_path() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

other_slot() { if [ "$1" = blue ]; then echo green; else echo blue; fi; }

live_slot() {
  local slot
  slot="$(k get svc frontend -o jsonpath='{.spec.selector.slot}' 2>/dev/null || true)"
  [ -n "$slot" ] || die "service 'frontend' not found or has no slot selector. Run bootstrap first."
  echo "$slot"
}

# Renders a directory under k8s/ (e.g. slots/green) with the image tag applied. A throwaway kustomization
# is generated next to it (kustomize refuses absolute resource paths) and removed again, so the checked-in
# files stay untouched.
render() {
  local rel="$1" tag="$2" tmp
  tmp="$(mktemp -d "$K8S_DIR/.render.XXXXXX")"
  cat > "$tmp/kustomization.yaml" <<EOF
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
resources:
  - ../$rel
images:
  - name: taskflow-backend
    newName: $BACKEND_REPO
    newTag: "$tag"
  - name: taskflow-frontend
    newName: $FRONTEND_REPO
    newTag: "$tag"
EOF
  kubectl kustomize "$(native_path "$tmp")" || { rm -rf "$tmp"; return 1; }
  rm -rf "$tmp"
}

run_migrations() {
  local tag="$1" deadline=$((SECONDS + WAIT_SECONDS + 60)) failed
  log "Running database migrations (backend image tag $tag)"
  # A Job's pod template is immutable, so replace it. The fixed name also stops two deploys migrating at once.
  k delete job backend-migrate --ignore-not-found --cascade=foreground --wait=true >/dev/null
  render jobs "$tag" | kubectl apply -f - >/dev/null
  while :; do
    if [ "$(k get job backend-migrate -o jsonpath='{.status.succeeded}')" = 1 ]; then
      k logs job/backend-migrate --tail=20 || true
      return 0
    fi
    failed="$(k get job backend-migrate -o jsonpath='{.status.conditions[?(@.type=="Failed")].status}')"
    if [ "$failed" = True ]; then
      k logs job/backend-migrate --tail=100 || true
      die "migration job failed; nothing was deployed"
    fi
    [ "$SECONDS" -lt "$deadline" ] || die "migration job did not finish in time"
    sleep 3
  done
}

apply_slot() {
  local slot="$1" tag="$2"
  log "Deploying tag $tag to the $slot colour"
  render "slots/$slot" "$tag" | kubectl apply -f -
}

# Rollout done AND at least the HPA's minimum number of pods ready.
wait_ready() {
  local slot="$1" app min ready deadline
  for app in backend frontend; do
    log "Waiting for $app-$slot"
    k rollout status "deploy/$app-$slot" --timeout="${WAIT_SECONDS}s"
    min="$(k get hpa "$app-$slot" -o jsonpath='{.spec.minReplicas}')"
    deadline=$((SECONDS + WAIT_SECONDS))
    while :; do
      ready="$(k get deploy "$app-$slot" -o jsonpath='{.status.readyReplicas}')"
      [ "${ready:-0}" -ge "$min" ] && break
      [ "$SECONDS" -lt "$deadline" ] || die "$app-$slot has ${ready:-0}/$min ready pods"
      sleep 3
    done
  done
}

# Runs curl inside the cluster against the colour's own Services (not the live one).
smoke_test() {
  local slot="$1" email pass name phase
  email="$(k get configmap taskflow-config -o jsonpath='{.data.DEMO_USER_EMAIL}')"
  pass="$(k get secret taskflow-secrets -o jsonpath='{.data.DEMO_USER_PASSWORD}' | base64 -d)"
  name="smoke-$slot-$RANDOM"
  log "Smoke-testing the $slot colour (pod $name)"
  k run "$name" --image="$CURL_IMAGE" --restart=Never \
    --env="SLOT=$slot" --env="EMAIL=$email" --env="PASS=$pass" \
    --command -- sh -c '
      set -eu
      NEW="http://frontend-$SLOT.taskflow.svc.cluster.local"
      LIVE="http://frontend.taskflow.svc.cluster.local"
      code() { curl -s -o /dev/null -w "%{http_code}" "$@"; }
      login() {
        curl -fsS -X POST "$1/api/auth/login" -H "Content-Type: application/json" \
          -d "{\"email\":\"$EMAIL\",\"password\":\"$PASS\"}" | sed -n "s/.*\"token\":\"\([^\"]*\)\".*/\1/p"
      }
      curl -fsS "$NEW/health/ready"; echo
      [ "$(code "$NEW/api/tasks")" = 401 ] || { echo "expected 401 without a token"; exit 1; }
      TOKEN="$(login "$NEW")"; [ -n "$TOKEN" ] || { echo "login returned no token"; exit 1; }
      [ "$(code -H "Authorization: Bearer $TOKEN" "$NEW/api/tasks")" = 200 ] || { echo "expected 200 with a token"; exit 1; }
      # A session issued by the live colour must keep working after the switch.
      LIVE_TOKEN="$(login "$LIVE")"; [ -n "$LIVE_TOKEN" ] || { echo "login on live returned no token"; exit 1; }
      [ "$(code -H "Authorization: Bearer $LIVE_TOKEN" "$NEW/api/tasks")" = 200 ] || { echo "live token rejected by new colour"; exit 1; }
      echo "smoke test passed"
    ' >/dev/null
  local deadline=$((SECONDS + 120))
  while :; do
    phase="$(k get pod "$name" -o jsonpath='{.status.phase}')"
    case "$phase" in Succeeded | Failed) break ;; esac
    [ "$SECONDS" -lt "$deadline" ] || { phase=Timeout; break; }
    sleep 2
  done
  k logs "$name" || true
  k delete pod "$name" --ignore-not-found --wait=false >/dev/null
  [ "$phase" = Succeeded ] || die "smoke test on the $slot colour ended in phase $phase"
}

switch_to() {
  local target="$1" live
  live="$(live_slot)"
  [ "$target" != "$live" ] || die "$target is already live"
  k get deploy "backend-$target" "frontend-$target" >/dev/null 2>&1 || die "nothing is deployed to $target; run deploy first"
  wait_ready "$target"
  log "Switching live traffic: $live -> $target"
  k patch svc frontend --type merge \
    -p "{\"metadata\":{\"annotations\":{\"taskflow-previous-slot\":\"$live\"}},\"spec\":{\"selector\":{\"slot\":\"$target\"}}}" >/dev/null
  log "Live colour is now $target (rollback target: $live)"
}

cmd_bootstrap() {
  local tag="${1:?usage: bluegreen.sh bootstrap <tag>}"
  log "Context: $(kubectl config current-context)"
  if k get svc frontend >/dev/null 2>&1; then
    die "already bootstrapped. Use deploy; re-applying the infrastructure would reset the live colour."
  fi
  kubectl apply -k "$(native_path "$K8S_DIR")"
  k rollout status deploy/postgres --timeout="${WAIT_SECONDS}s"
  run_migrations "$tag"
  apply_slot blue "$tag"
  wait_ready blue
  smoke_test blue
  log "Bootstrap done: blue is live on tag $tag"
}

cmd_deploy() {
  local tag="${1:?usage: bluegreen.sh deploy <tag> [--switch]}" auto_switch="${2:-}" live idle
  log "Context: $(kubectl config current-context)"
  live="$(live_slot)"
  idle="$(other_slot "$live")"
  log "Live colour: $live. Deploying to: $idle"
  run_migrations "$tag"
  apply_slot "$idle" "$tag"
  wait_ready "$idle"
  smoke_test "$idle"
  if [ "$auto_switch" = --switch ]; then
    switch_to "$idle"
  else
    log "$idle is ready and tested but not live. Run: scripts/bluegreen.sh switch"
  fi
}

cmd_switch() { switch_to "$(other_slot "$(live_slot)")"; }

cmd_rollback() {
  local live previous
  live="$(live_slot)"
  previous="$(k get svc frontend -o jsonpath='{.metadata.annotations.taskflow-previous-slot}')"
  [ -n "$previous" ] || previous="$(other_slot "$live")"
  switch_to "$previous"
}

cmd_status() {
  local live
  live="$(live_slot)"
  log "Live colour: $live"
  k get svc,deploy,hpa -l app.kubernetes.io/part-of=taskflow -L slot
  k get deploy -l app.kubernetes.io/part-of=taskflow \
    -o custom-columns='NAME:.metadata.name,IMAGE:.spec.template.spec.containers[0].image,READY:.status.readyReplicas'
}

# Allows `source scripts/bluegreen.sh` (e.g. for tests) without running a command.
if [ "${BASH_SOURCE[0]}" = "$0" ]; then
  case "${1:-}" in
    bootstrap) shift; cmd_bootstrap "$@" ;;
    deploy)    shift; cmd_deploy "$@" ;;
    switch)    cmd_switch ;;
    rollback)  cmd_rollback ;;
    status)    cmd_status ;;
    *) sed -n '2,10p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 1 ;;
  esac
fi
