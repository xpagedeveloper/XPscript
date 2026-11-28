#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
todo_file="${1:-$repo_root/todo/ast-compiler-migration-todo.md}"
state_file="${2:-$repo_root/todo/.progress-state}"

head_commit="$(git -C "$repo_root" rev-parse HEAD)"
first_open="$(rg -n -m 1 '^\s*(?:-\s+|[- ]+)?\[ \]' "$todo_file" || true)"
if [[ -z "$first_open" ]]; then
  echo "TODO_PROGRESS=complete"
  exit 0
fi

current="$head_commit|$first_open"
previous=""
if [[ -f "$state_file" ]]; then previous="$(<"$state_file")"; fi

if [[ "$current" == "$previous" ]]; then
  echo "TODO_PROGRESS=stuck"
  echo "TODO_PROGRESS_HEAD=$head_commit"
  echo "TODO_PROGRESS_ITEM=$first_open"
  exit 2
fi

mkdir -p "$(dirname "$state_file")"
printf '%s\n' "$current" > "$state_file"
echo "TODO_PROGRESS=advanced"
echo "TODO_PROGRESS_HEAD=$head_commit"
echo "TODO_PROGRESS_ITEM=$first_open"
