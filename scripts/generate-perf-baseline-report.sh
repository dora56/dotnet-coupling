#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 3 ]]; then
  echo "Usage: $0 <dotnet-coupling-tool-path> <target-path> <output-directory>" >&2
  exit 1
fi

tool_path="$1"
target_path="$2"
output_directory="$3"

if [[ ! -f "$tool_path" ]]; then
  echo "Tool executable was not found: $tool_path" >&2
  exit 2
fi

if [[ ! -x "$tool_path" ]]; then
  echo "Tool executable is not runnable: $tool_path" >&2
  exit 2
fi

if [[ ! -e "$target_path" ]]; then
  echo "Target path was not found: $target_path" >&2
  exit 2
fi

tool_path="$(cd "$(dirname "$tool_path")" && pwd)/$(basename "$tool_path")"
target_path="$(cd "$(dirname "$target_path")" && pwd)/$(basename "$target_path")"
output_directory="$(mkdir -p "$output_directory" && cd "$output_directory" && pwd)"

mkdir -p "$output_directory"

syntax_summary_path="$output_directory/syntax-summary.txt"
semantic_summary_path="$output_directory/semantic-summary.txt"
syntax_stderr_path="$output_directory/syntax.stderr.txt"
semantic_stderr_path="$output_directory/semantic.stderr.txt"
syntax_time_path="$output_directory/syntax.time.txt"
semantic_time_path="$output_directory/semantic.time.txt"
markdown_path="$output_directory/perf-baseline.md"

run_mode() {
  local mode="$1"
  local summary_path="$2"
  local stderr_path="$3"
  local time_path="$4"

  set +e
  /usr/bin/time -p -o "$time_path" \
    "$tool_path" --mode "$mode" --summary --no-git "$target_path" \
    >"$summary_path" 2>"$stderr_path"
  local exit_code=$?
  set -e

  printf '%s' "$exit_code"
}

extract_wall_time() {
  local time_path="$1"

  awk '$1 == "real" { print $2 "s" }' "$time_path"
}

classify_result() {
  local mode="$1"
  local exit_code="$2"
  local stderr_path="$3"

  if [[ "$exit_code" == "0" ]]; then
    echo "PASS"
    return
  fi

  if [[ "$mode" == "semantic" ]] && grep -q "Semantic workspace could not be loaded" "$stderr_path"; then
    echo "LOAD_BLOCKED(exit=$exit_code)"
    return
  fi

  echo "FAIL(exit=$exit_code)"
}

syntax_exit_code="$(run_mode syntax "$syntax_summary_path" "$syntax_stderr_path" "$syntax_time_path")"
semantic_exit_code="$(run_mode semantic "$semantic_summary_path" "$semantic_stderr_path" "$semantic_time_path")"

syntax_result="$(classify_result syntax "$syntax_exit_code" "$syntax_stderr_path")"
semantic_result="$(classify_result semantic "$semantic_exit_code" "$semantic_stderr_path")"
syntax_wall_time="$(extract_wall_time "$syntax_time_path")"
semantic_wall_time="$(extract_wall_time "$semantic_time_path")"

semantic_failure_excerpt="None"
if [[ "$semantic_exit_code" != "0" ]]; then
  semantic_failure_excerpt="$(sed -n '1,12p' "$semantic_stderr_path")"
fi

cat > "$markdown_path" <<EOF
# Perf Baseline Report

- Target: \`$target_path\`
- Generated: \`$(date -u +"%Y-%m-%dT%H:%M:%SZ")\`
- Tool: \`$tool_path\`

## Measurements

| Mode | Result | Wall time |
| --- | --- | --- |
| \`syntax\` | \`$syntax_result\` | \`$syntax_wall_time\` |
| \`semantic-preview\` | \`$semantic_result\` | \`$semantic_wall_time\` |

## Syntax Summary

\`\`\`text
$(cat "$syntax_summary_path")
\`\`\`

## Semantic Summary

\`\`\`text
$(cat "$semantic_summary_path")
\`\`\`

## Semantic Failure Excerpt

\`\`\`text
$semantic_failure_excerpt
\`\`\`

## Artifacts

- \`$(basename "$syntax_summary_path")\`
- \`$(basename "$semantic_summary_path")\`
- \`$(basename "$syntax_stderr_path")\`
- \`$(basename "$semantic_stderr_path")\`
- \`$(basename "$syntax_time_path")\`
- \`$(basename "$semantic_time_path")\`
EOF

echo "$markdown_path"
