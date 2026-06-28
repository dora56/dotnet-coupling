#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: scripts/generate-ci-summary.sh --coverage-dir DIR --mutation-dir DIR

Prints a GitHub-flavored markdown summary for CI coverage and mutation reports.
EOF
}

coverage_dir=""
mutation_dir=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --coverage-dir)
      coverage_dir="${2:-}"
      shift 2
      ;;
    --mutation-dir)
      mutation_dir="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

if [[ -z "$coverage_dir" || -z "$mutation_dir" ]]; then
  usage >&2
  exit 1
fi

python3 - "$coverage_dir" "$mutation_dir" <<'PY'
from __future__ import annotations

import collections
import json
import pathlib
import sys
import xml.etree.ElementTree as ET

coverage_dir = pathlib.Path(sys.argv[1])
mutation_dir = pathlib.Path(sys.argv[2])

coverage_files = sorted(
    list(coverage_dir.rglob("coverage.cobertura.xml"))
    + list(coverage_dir.rglob("*.coverage.cobertura.xml"))
)
mutation_files = sorted(mutation_dir.rglob("mutation-report.json"))

if not coverage_files:
    raise SystemExit(f"No Cobertura reports found under {coverage_dir}")
if not mutation_files:
    raise SystemExit(f"No mutation report found under {mutation_dir}")

covered_lines = 0
valid_lines = 0
covered_branches = 0
valid_branches = 0
for path in coverage_files:
    root = ET.parse(path).getroot()
    covered_lines += int(root.attrib.get("lines-covered", "0"))
    valid_lines += int(root.attrib.get("lines-valid", "0"))
    covered_branches += int(root.attrib.get("branches-covered", "0"))
    valid_branches += int(root.attrib.get("branches-valid", "0"))

line_rate = (covered_lines / valid_lines) if valid_lines else 0.0
branch_rate = (covered_branches / valid_branches) if valid_branches else 0.0

mutation_report = json.loads(mutation_files[0].read_text())
statuses = collections.Counter()
for file_report in mutation_report["files"].values():
    for mutant in file_report["mutants"]:
        statuses[mutant["status"]] += 1

ignored = statuses.get("Ignored", 0)
killed = statuses.get("Killed", 0)
survived = statuses.get("Survived", 0)
timeout = statuses.get("Timeout", 0)
runtime_error = statuses.get("RuntimeError", 0)
no_coverage = statuses.get("NoCoverage", 0)
compile_error = statuses.get("CompileError", 0)
tracked = killed + survived + timeout + runtime_error + no_coverage
tracked_score = (killed / tracked * 100.0) if tracked else 0.0

def pct(value: float) -> str:
    return f"{value * 100:.1f}%"

print("## CI Report")
print()
print("| Area | Result |")
print("| --- | --- |")
print(f"| Coverage | {pct(line_rate)} line / {pct(branch_rate)} branch |")
print(f"| Mutation | {tracked_score:.1f}% kill ratio across tracked mutants |")
print()
print("### Coverage")
print()
print(f"- Reports: {len(coverage_files)}")
print(f"- Lines: {covered_lines}/{valid_lines} ({pct(line_rate)})")
print(f"- Branches: {covered_branches}/{valid_branches} ({pct(branch_rate)})")
print()
print("### Mutation")
print()
print(f"- Reports: {len(mutation_files)}")
print(f"- Killed: {killed}")
print(f"- Survived: {survived}")
print(f"- Timeout: {timeout}")
print(f"- Runtime error: {runtime_error}")
print(f"- No coverage: {no_coverage}")
print(f"- Compile error: {compile_error}")
print(f"- Ignored: {ignored}")
print(f"- Thresholds: low {mutation_report['thresholds']['low']} / high {mutation_report['thresholds']['high']}")
print()
print("### Notes")
print()
print("- The summary is written from the generated test and mutation artifacts, so it reflects the actual CI run.")
print("- The mutation ratio above is a simple killed / tracked-mutants ratio for the report; Stryker still enforces the official gate in the mutation job.")
PY
