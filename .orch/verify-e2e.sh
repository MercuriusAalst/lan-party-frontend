#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."
project=tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj
run_dir=".orch/shots/e2e-$(date -u +%Y%m%dT%H%M%SZ)-$$"
mkdir -p "$run_dir"

if ! dotnet build "$project" -c Release -m:1 -nr:false -p:UseSharedCompilation=false >"$run_dir/build.log" 2>&1; then
  cat "$run_dir/build.log"; exit 1
fi
if ! dotnet test "$project" -c Release --no-build --no-restore --list-tests -m:1 -nr:false \
  -p:UseSharedCompilation=false >"$run_dir/discovery.log" 2>&1; then
  cat "$run_dir/discovery.log"; exit 1
fi

python - "$run_dir/discovery.log" "$run_dir/classes.tsv" <<'PY'
import collections, pathlib, re, sys
lines = pathlib.Path(sys.argv[1]).read_text(encoding="utf-8").splitlines()
headers = [i for i, line in enumerate(lines) if line.strip() == "The following Tests are available:"]
if len(headers) != 1: raise SystemExit("test discovery header missing or duplicated")
tests = []
for line in lines[headers[0] + 1:]:
    if not line.strip(): continue
    match = re.fullmatch(r"\s+Mercurius\.LAN\.Web\.E2ETests\.([^.]+)\.(.+)", line)
    if not match: raise SystemExit(f"unparsed discovery line: {line}")
    tests.append((match.group(1), line.strip()))
if not tests or len({name for _, name in tests}) != len(tests):
    raise SystemExit("discovery is empty or contains duplicate cases")
counts = collections.Counter(cls for cls, _ in tests)
with open(sys.argv[2], "w", encoding="utf-8") as output:
    for cls, count in sorted(counts.items()): output.write(f"{cls}\t{count}\n")
print(f"Discovered {len(tests)} unique cases in {len(counts)} classes")
PY

discovered=$(awk -F '\t' '{sum += $2} END {print sum + 0}' "$run_dir/classes.tsv")
classes=$(wc -l < "$run_dir/classes.tsv" | tr -d ' ')
(( discovered > 0 && classes > 0 )) || { echo "discovery produced no tests/classes" >&2; exit 1; }
executed=0; passed=0
while IFS=$'\t' read -r class expected; do
  log="$run_dir/$class.log"; trx="$run_dir/$class.trx"
  if ! dotnet test "$project" -c Release --no-build --no-restore -m:1 -nr:false \
    -p:UseSharedCompilation=false --filter "FullyQualifiedName~Mercurius.LAN.Web.E2ETests.$class." \
    --logger "trx;LogFileName=$class.trx" --results-directory "$run_dir" >"$log" 2>&1; then
    cat "$log"; exit 1
  fi
  python - "$trx" "$class" "$expected" <<'PY'
import pathlib, sys, xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
counters = next(node for node in root.iter() if node.tag.endswith("Counters"))
values = {key: int(counters.attrib.get(key, "0")) for key in ("total", "executed", "passed", "failed", "notExecuted", "aborted", "timeout", "inconclusive")}
expected = int(sys.argv[3])
results = [node for node in root.iter() if node.tag.endswith("UnitTestResult")]
ids = [node.attrib.get("testId") for node in results]
if len(results) != expected or any(not test_id for test_id in ids) or len(set(ids)) != expected:
    raise SystemExit(f"{sys.argv[2]} has missing or duplicate TRX case results")
if values["total"] != expected or values["executed"] != expected or values["passed"] != expected or any(values[k] for k in ("failed", "notExecuted", "aborted", "timeout", "inconclusive")):
    raise SystemExit(f"{sys.argv[2]} coverage mismatch: expected {expected}, TRX {values}")
print(f"PASS {sys.argv[2]}: {expected} cases")
PY
  executed=$((executed + expected)); passed=$((passed + expected))
done < "$run_dir/classes.tsv"

(( executed == discovered && passed == discovered )) || { echo "aggregate coverage mismatch" >&2; exit 1; }
echo "E2E coverage complete: $passed/$discovered cases across $classes classes; logs: $run_dir"
