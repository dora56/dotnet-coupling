#!/usr/bin/env python3

from __future__ import annotations

import argparse
import json
import pathlib
import sys
import xml.etree.ElementTree as ET


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Validate Cobertura branch coverage and emit an octocov custom metric."
    )
    parser.add_argument("--coverage", required=True, type=pathlib.Path)
    parser.add_argument("--output", required=True, type=pathlib.Path)
    parser.add_argument("--minimum", type=float, default=80.0)
    parser.add_argument("--max-regression", type=float, default=0.1)
    return parser.parse_args()


def read_branch_coverage(path: pathlib.Path) -> float:
    root = ET.parse(path).getroot()
    covered = int(root.attrib["branches-covered"])
    valid = int(root.attrib["branches-valid"])
    if covered < 0 or valid < 0 or covered > valid:
        raise ValueError("Cobertura branch totals are inconsistent")
    return 100.0 if valid == 0 else covered / valid * 100.0


def main() -> int:
    args = parse_args()
    try:
        branch_coverage = read_branch_coverage(args.coverage)
    except (OSError, ET.ParseError, KeyError, ValueError) as error:
        print(f"Could not read branch coverage: {error}", file=sys.stderr)
        return 1

    metric = {
        "key": "branch_coverage",
        "name": "Branch Coverage",
        "metrics": [
            {
                "key": "branch_coverage",
                "name": "Branch coverage",
                "value": round(branch_coverage, 4),
                "unit": "%",
            }
        ],
        "acceptables": [
            f"current.branch_coverage >= {args.minimum:g}",
            f"diff.branch_coverage >= -{args.max_regression:g}"
        ],
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(metric, indent=2) + "\n")
    print(f"Branch coverage: {branch_coverage:.1f}%")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
