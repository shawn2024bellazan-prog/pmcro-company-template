#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
command -v python3 >/dev/null || { echo 'Python 3 is required for JSON validation.' >&2; exit 2; }
python3 - <<'PY'
import json
from pathlib import Path
files = sorted(Path('.').rglob('*.json'))
if not files: raise SystemExit('No JSON files found')
for p in files: json.loads(p.read_text(encoding='utf-8'))
print(f'JSON validation passed: {len(files)} files')
PY
if command -v ruby >/dev/null; then
  ruby - <<'RUBY'
require 'yaml'
files = Dir.glob('**/*.{yaml,yml}', File::FNM_EXTGLOB).sort
abort 'No YAML files found' if files.empty?
files.each { |p| YAML.safe_load_file(p, permitted_classes: [], permitted_symbols: [], aliases: false) }
puts "YAML parse passed: #{files.length} files"
RUBY
else
  echo 'Ruby not found; YAML parsing skipped.'
fi
if command -v dotnet >/dev/null; then
  dotnet restore PMCR-O.sln
  dotnet build PMCR-O.sln --configuration Release --no-restore
  dotnet run --project tests/PMCR.O.Core.Tests/PMCR.O.Core.Tests.csproj --configuration Release --no-build
  dotnet run --project src/PMCR.O.Demo/PMCR.O.Demo.csproj --configuration Release --no-build
else
  echo '.NET SDK 10 not found; build/tests/demo skipped.'
  exit 3
fi
