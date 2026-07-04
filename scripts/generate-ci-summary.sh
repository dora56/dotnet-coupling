#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: scripts/generate-ci-summary.sh --coverage-dir DIR --mutation-dir DIR [--coupling-dir DIR]

Prints a GitHub-flavored markdown summary for CI coverage, mutation, and coupling feedback reports.
EOF
}

coverage_dir=""
mutation_dir=""
coupling_dir=""

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
    --coupling-dir)
      coupling_dir="${2:-}"
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

python3 - "$coverage_dir" "$mutation_dir" "$coupling_dir" <<'PY'
from __future__ import annotations

import collections
import json
import pathlib
import sys
import xml.etree.ElementTree as ET

coverage_dir = pathlib.Path(sys.argv[1])
mutation_dir = pathlib.Path(sys.argv[2])
coupling_dir = pathlib.Path(sys.argv[3]) if len(sys.argv) > 3 and sys.argv[3] else None

coverage_files = sorted(
    {
        path
        for pattern in (
            "coverage.cobertura.xml",
            "coverage-*.cobertura.xml",
            "*.coverage.cobertura.xml",
            "coverage.cobertura*.xml",
            "*.coverage.cobertura*.xml",
        )
        for path in coverage_dir.rglob(pattern)
    }
)
mutation_files = sorted(mutation_dir.rglob("mutation-report.json"))
coupling_sarif_files = sorted(coupling_dir.rglob("*.sarif")) if coupling_dir and coupling_dir.exists() else []
hotspot_files = sorted(
    {
        path
        for pattern in ("*hotspot*.txt", "*hotspots*.txt")
        for path in coupling_dir.rglob(pattern)
    }
) if coupling_dir and coupling_dir.exists() else []

if not coverage_files:
    raise SystemExit(f"No Cobertura reports found under {coverage_dir}")

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

mutation_report = None
statuses = collections.Counter()
if mutation_files:
    mutation_report = json.loads(mutation_files[0].read_text())
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
tracked_score = (killed / tracked * 100.0) if tracked else None

def pct(value: float) -> str:
    return f"{value * 100:.1f}%"

mutation_result = (
    f"{tracked_score:.1f}% kill ratio across tracked mutants"
    if tracked_score is not None
    else "not available for this run"
)
coupling_result = (
    f"{len(coupling_sarif_files)} SARIF / {len(hotspot_files)} hotspots artifact(s)"
    if coupling_sarif_files or hotspot_files
    else "not available for this run"
)

hotspot_excerpt = []
if hotspot_files:
    hotspot_excerpt = hotspot_files[0].read_text().splitlines()[:20]

print("## CI Report")
print()
print("| Area | Result |")
print("| --- | --- |")
print(f"| Coverage | {pct(line_rate)} line / {pct(branch_rate)} branch |")
print(f"| Mutation | {mutation_result} |")
print(f"| Coupling feedback | {coupling_result} |")
print()
print("### Coverage")
print()
print(f"- Reports: {len(coverage_files)}")
print(f"- Lines: {covered_lines}/{valid_lines} ({pct(line_rate)})")
print(f"- Branches: {covered_branches}/{valid_branches} ({pct(branch_rate)})")
print()
print("### Mutation")
print()
if mutation_report is None:
    print("- Reports: 0")
    print("- Status: not available for this run")
else:
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
print("### Coupling Feedback")
print()
if not coupling_sarif_files and not hotspot_files:
    print("- Status: not available for this run")
else:
    print(f"- SARIF artifacts: {len(coupling_sarif_files)}")
    print(f"- Hotspots artifacts: {len(hotspot_files)}")
    if hotspot_excerpt:
        print()
        print("```text")
        print("\n".join(hotspot_excerpt))
        print("```")
print()
print("### Notes")
print()
print("- The summary is written from the generated test, mutation, and coupling feedback artifacts, so it reflects the actual CI run.")
if mutation_report is None:
    print("- Mutation reporting is skipped when the workflow does not produce a Stryker report.")
else:
    print("- The mutation ratio above is a simple killed / tracked-mutants ratio for the report; Stryker still enforces the official gate in nightly or manual mutation runs.")
if coupling_sarif_files or hotspot_files:
    print("- Coupling feedback is generated from the packaged CLI and uploaded as SARIF / hotspots artifacts.")
else:
    print("- Coupling feedback is reported as unavailable when the artifact job is skipped, cancelled, or cannot upload artifacts.")
PY
