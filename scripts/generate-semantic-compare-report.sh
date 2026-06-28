#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 3 ]]; then
  echo "Usage: $0 <dotnet-coupling-tool-path> <target-path> <output-directory>" >&2
  exit 1
fi

tool_path="$1"
target_path="$2"
output_directory="$3"

mkdir -p "$output_directory"

syntax_summary_path="$output_directory/syntax-summary.txt"
semantic_summary_path="$output_directory/semantic-summary.txt"
syntax_json_path="$output_directory/syntax-report.json"
semantic_json_path="$output_directory/semantic-report.json"
markdown_path="$output_directory/semantic-compare.md"

"$tool_path" --mode syntax --summary --no-git "$target_path" > "$syntax_summary_path"
"$tool_path" --mode semantic --summary --no-git "$target_path" > "$semantic_summary_path"
"$tool_path" --mode syntax --json --no-git "$target_path" > "$syntax_json_path"
"$tool_path" --mode semantic --json --no-git "$target_path" > "$semantic_json_path"

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

if [[ "$(sed -n '1,3p' "$syntax_summary_path")" == "$(sed -n '1,3p' "$semantic_summary_path")" ]]; then
  comparison_note="No grade, coupling, or issue delta was observed on this target. semantic-preview currently adds mode metadata without changing the headline result."
else
  comparison_note="A headline result delta was observed between syntax and semantic-preview."
fi

cat > "$markdown_path" <<EOF
# Syntax vs Semantic Compare

- Target: \`$target_path\`
- Generated: \`$(date -u +"%Y-%m-%dT%H:%M:%SZ")\`
- Tool: \`$tool_path\`

## Comparison Note

$comparison_note

## Headline Diff

| Mode | Summary |
| --- | --- |
| syntax | $syntax_headline_escaped |
| semantic-preview | $semantic_headline_escaped |

## Syntax Summary

\`\`\`text
$(cat "$syntax_summary_path")
\`\`\`

## Semantic Summary

\`\`\`text
$(cat "$semantic_summary_path")
\`\`\`

## Artifacts

- \`$(basename "$syntax_summary_path")\`
- \`$(basename "$semantic_summary_path")\`
- \`$(basename "$syntax_json_path")\`
- \`$(basename "$semantic_json_path")\`
EOF

echo "$markdown_path"
