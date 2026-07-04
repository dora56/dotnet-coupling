#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 3 || $# -gt 4 ]]; then
  echo "Usage: $0 <dotnet-coupling-tool-path> <target-path> <output-directory> [config-path]" >&2
  exit 1
fi

tool_path="$1"
target_path="$2"
output_directory="$3"
config_path="${4:-}"
config_args=()

if [[ -n "$config_path" ]]; then
  config_args=(--config "$config_path")
fi

mkdir -p "$output_directory"

syntax_summary_path="$output_directory/syntax-summary.txt"
semantic_summary_path="$output_directory/semantic-summary.txt"
syntax_json_path="$output_directory/syntax-report.json"
semantic_json_path="$output_directory/semantic-report.json"
markdown_path="$output_directory/semantic-compare.md"

extract_json_number() {
  local file_path="$1"
  local property_name="$2"
  local line

  line="$(grep -m1 "\"$property_name\"" "$file_path" || true)"
  if [[ -z "$line" ]]; then
    echo "0"
    return
  fi

  echo "$line" | tr -cd '0-9'
}

count_json_property() {
  local file_path="$1"
  local property_name="$2"

  (grep -o "\"$property_name\"" "$file_path" || true) | wc -l | tr -d ' '
}

build_issue_type_delta_markdown() {
  local syntax_report_path="$1"
  local semantic_report_path="$2"

  python3 - "$syntax_report_path" "$semantic_report_path" <<'PY'
import json
import sys


def load_counts(path):
    with open(path, encoding="utf-8") as stream:
        report = json.load(stream)

    counts = {}
    for issue in report.get("issues", []):
        issue_type = issue.get("type") or "Unknown"
        counts[issue_type] = counts.get(issue_type, 0) + 1
    return counts


syntax_counts = load_counts(sys.argv[1])
semantic_counts = load_counts(sys.argv[2])
issue_types = sorted(set(syntax_counts) | set(semantic_counts))

print("| Issue Type | syntax | semantic-preview | delta |")
print("| --- | ---: | ---: | ---: |")

if not issue_types:
    print("| _none_ | `0` | `0` | `0` |")
else:
    for issue_type in issue_types:
        syntax_count = syntax_counts.get(issue_type, 0)
        semantic_count = semantic_counts.get(issue_type, 0)
        delta = semantic_count - syntax_count
        signed_delta = f"+{delta}" if delta > 0 else str(delta)
        print(f"| `{issue_type}` | `{syntax_count}` | `{semantic_count}` | `{signed_delta}` |")
PY
}

build_semantic_only_high_issues_markdown() {
  local syntax_report_path="$1"
  local semantic_report_path="$2"

  python3 - "$syntax_report_path" "$semantic_report_path" <<'PY'
import json
import sys


def load_issues(path):
    with open(path, encoding="utf-8") as stream:
        report = json.load(stream)
    return report.get("issues", [])


def issue_key(issue):
    return (
        issue.get("type") or "",
        issue.get("source") or "",
        issue.get("target") or "",
    )


def escape_cell(value):
    return str(value or "n/a").replace("\\", "\\\\").replace("|", "\\|").replace("`", "\\`")


syntax_issues = load_issues(sys.argv[1])
semantic_issues = load_issues(sys.argv[2])
syntax_keys = {issue_key(issue) for issue in syntax_issues}
semantic_only_issues = [
    issue
    for issue in semantic_issues
    if issue_key(issue) not in syntax_keys and issue.get("severity") in {"Critical", "High"}
]

print("| Severity | Issue Type | Source | Target | Problem |")
print("| --- | --- | --- | --- | --- |")

if not semantic_only_issues:
    print("| _none_ |  |  |  |  |")
else:
    for issue in semantic_only_issues[:10]:
        severity = escape_cell(issue.get("severity"))
        issue_type = escape_cell(issue.get("type"))
        source = escape_cell(issue.get("source"))
        target = escape_cell(issue.get("target"))
        problem = escape_cell(issue.get("problem"))
        print(f"| `{severity}` | `{issue_type}` | `{source}` | `{target}` | {problem} |")
PY
}

"$tool_path" --mode syntax --summary --no-git "${config_args[@]}" "$target_path" > "$syntax_summary_path"
"$tool_path" --mode semantic --summary --no-git "${config_args[@]}" "$target_path" > "$semantic_summary_path"
"$tool_path" --mode syntax --json --no-git "${config_args[@]}" "$target_path" > "$syntax_json_path"
"$tool_path" --mode semantic --json --no-git "${config_args[@]}" "$target_path" > "$semantic_json_path"

syntax_grade_line="$(sed -n '1p' "$syntax_summary_path")"
syntax_files_line="$(sed -n '2p' "$syntax_summary_path")"
syntax_issues_line="$(sed -n '3p' "$syntax_summary_path")"
semantic_grade_line="$(sed -n '1p' "$semantic_summary_path")"
semantic_files_line="$(sed -n '2p' "$semantic_summary_path")"
semantic_issues_line="$(sed -n '3p' "$semantic_summary_path")"
syntax_headline="$(printf '%s / %s / %s' "$syntax_grade_line" "$syntax_files_line" "$syntax_issues_line")"
semantic_headline="$(printf '%s / %s / %s' "$semantic_grade_line" "$semantic_files_line" "$semantic_issues_line")"
syntax_headline_escaped="${syntax_headline//|/\\|}"
semantic_headline_escaped="${semantic_headline//|/\\|}"
syntax_internal_couplings="$(extract_json_number "$syntax_json_path" "internal")"
semantic_internal_couplings="$(extract_json_number "$semantic_json_path" "internal")"
syntax_high_issues="$(extract_json_number "$syntax_json_path" "high")"
semantic_high_issues="$(extract_json_number "$semantic_json_path" "high")"
syntax_medium_issues="$(extract_json_number "$syntax_json_path" "medium")"
semantic_medium_issues="$(extract_json_number "$semantic_json_path" "medium")"
syntax_diagnostics_count="$(count_json_property "$syntax_json_path" "code")"
semantic_diagnostics_count="$(count_json_property "$semantic_json_path" "code")"
issue_type_delta_markdown="$(build_issue_type_delta_markdown "$syntax_json_path" "$semantic_json_path")"
semantic_only_high_issues_markdown="$(build_semantic_only_high_issues_markdown "$syntax_json_path" "$semantic_json_path")"

comparison_note=""

if [[ "$(sed -n '1,3p' "$syntax_summary_path")" == "$(sed -n '1,3p' "$semantic_summary_path")" ]]; then
  comparison_note="No grade, coupling, or issue delta was observed on this target. semantic-preview currently adds mode metadata without changing the headline result."
else
  comparison_note="A headline result delta was observed between syntax and semantic-preview."
fi

if [[ "$semantic_diagnostics_count" -gt "$syntax_diagnostics_count" ]]; then
  comparison_note="$comparison_note Semantic-only diagnostics were also observed and should be reviewed as compare output, not treated as incidental stderr noise."
fi

if [[ "$semantic_internal_couplings" -gt "$syntax_internal_couplings" && "$semantic_high_issues" == "$syntax_high_issues" && "$semantic_medium_issues" == "$syntax_medium_issues" ]]; then
  comparison_note="$comparison_note The larger internal coupling count did not change issue totals on this target, so the artifact records the delta as expanded symbol-aware coverage rather than a CLI contract change."
fi

cat > "$markdown_path" <<EOF
# Syntax vs Semantic Compare

- Target: \`$target_path\`
- Generated: \`$(date -u +"%Y-%m-%dT%H:%M:%SZ")\`
- Tool: \`$tool_path\`
- Config: \`${config_path:-none}\`

## Comparison Note

$comparison_note

## Headline Diff

| Mode | Summary |
| --- | --- |
| syntax | $syntax_headline_escaped |
| semantic-preview | $semantic_headline_escaped |

## Metric Diff

| Metric | syntax | semantic-preview |
| --- | --- | --- |
| Internal couplings | \`$syntax_internal_couplings\` | \`$semantic_internal_couplings\` |
| High issues | \`$syntax_high_issues\` | \`$semantic_high_issues\` |
| Medium issues | \`$syntax_medium_issues\` | \`$semantic_medium_issues\` |
| Recoverable diagnostics | \`$syntax_diagnostics_count\` | \`$semantic_diagnostics_count\` |

## Issue Type Delta

$issue_type_delta_markdown

## Semantic-Only High Issues Top 10

$semantic_only_high_issues_markdown

## Syntax Summary

\`\`\`text
$(cat "$syntax_summary_path")
\`\`\`

## Semantic Summary

\`\`\`text
$(cat "$semantic_summary_path")
\`\`\`

## Interpretation

- Treat recoverable diagnostics as compare output. A semantic-only diagnostic
  usually means a workspace prerequisite or loadability difference, not a crash.
- If internal coupling count rises without changing grade or issue totals, read
  the delta as expanded symbol-aware coverage unless the diagnostic context
  suggests an environment regression.
- Use the paired JSON artifacts to inspect issue lists and manifest diagnostics
  without changing the supported CLI summary contract.

## Artifacts

- \`$(basename "$syntax_summary_path")\`
- \`$(basename "$semantic_summary_path")\`
- \`$(basename "$syntax_json_path")\`
- \`$(basename "$semantic_json_path")\`
EOF

echo "$markdown_path"
