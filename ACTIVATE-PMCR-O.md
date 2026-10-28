# PMCR-O Activation Kit

This kit contains the cumulative source tree through Increment 19 plus the individual Increment 1–19 ZIPs. It is a starter/template, not a live hosted PMCR-O deployment. Extract it, publish the source to your GitHub repository, and let GitHub Actions run CI.

## 1. What's included

- `pmcro-company-template/`: cumulative source, manifests, schemas, tests, demo, and `.github/workflows/ci.yml`.
- `increments/`: original incremental ZIP packages 1–19, kept for traceability/reference.
- CI workflow: triggers on push, pull request, manual `workflow_dispatch`, reusable `workflow_call`, and weekly schedule.
- Protocol/capability boundaries: A2A, MCP, HTTP/OpenAPI and marketplace sources are documented contracts or placeholders unless explicitly marked implemented. Do not treat catalog entries as connected services.

## 2. Activate GitHub Actions

1. Extract this ZIP.
2. Open a terminal in `pmcro-company-template/`.
3. Ensure the target GitHub repository is the intended repository and that you have write access.
4. If the folder is not already a Git checkout, run:

   ```bash
   git init
   git branch -M main
   git add .
   git commit -m "Add PMCR-O company template and CI"
   git remote add origin https://github.com/OWNER/REPOSITORY.git
   git push -u origin main
   ```

   Replace `OWNER/REPOSITORY` with your actual repository. If you already cloned the repository, do not run `git init` or add another remote; copy the project files into that checkout, inspect `git status`, commit, and push on the intended branch.

5. In GitHub, open **Actions** and enable workflows if prompted.
6. Push any new commit or open/update a pull request. The workflow at `.github/workflows/ci.yml` should start automatically. You can also run it from **Actions → PMCR-O CI → Run workflow**.
7. Open the run and inspect each step. A package is not production-ready merely because CI is green; review security, deployment configuration, and runtime integration separately.

## 3. Local validation

Requires Git, Python 3, Ruby, and .NET SDK 10. From the project root:

```bash
python - <<'PY'
import json
from pathlib import Path
for p in sorted(Path('.').rglob('*.json')):
    json.loads(p.read_text(encoding='utf-8'))
print('JSON validation passed')
PY
ruby -e 'require "yaml"; Dir.glob("**/*.{yaml,yml}", File::FNM_EXTGLOB).each { |p| YAML.safe_load_file(p, permitted_classes: [], permitted_symbols: [], aliases: false) }; puts "YAML parse passed"'
dotnet restore PMCR-O.sln
dotnet build PMCR-O.sln --configuration Release --no-restore
dotnet run --project tests/PMCR.O.Core.Tests/PMCR.O.Core.Tests.csproj --configuration Release --no-build
dotnet run --project src/PMCR.O.Demo/PMCR.O.Demo.csproj --configuration Release --no-build
```

## 4. Integration/plugin status

- **GitHub Actions CI**: workflow file included; it becomes active only after this workflow is committed and pushed to GitHub with Actions enabled.
- **GitHub connector**: available in the current assistant environment, but the attempted branch creation for `feat/a2a-declarative-enterprise` was rejected with HTTP 403. No remote branch or PR is claimed.
- **MCP**: architecture/contract boundary only; a live MCP transport/server must still be configured and tested.
- **A2A**: manifest/interop design boundary only; live agent discovery/task transport must still be configured and tested.
- **GitHub Marketplace, VS Code Marketplace, MCP registry, private registries**: catalog/source placeholders, not authenticated live marketplace connections.
- **MAF / .NET Aspire / external skill adapters**: integration work remains; do not infer they are already wired merely from the architecture docs.
- **Credential/secrets**: never paste tokens into source or manifests. Use GitHub's credential manager or repository/environment secrets when a workflow genuinely needs them. Current CI uses `contents: read` and does not deploy or auto-merge.

## 5. Autonomy and safety

PMCR-O may continue routine bounded work and retries within configured budgets. Auto-continue is not blanket authorization. Keep production deployments, destructive actions, privilege expansion, security-policy changes, and external side effects behind explicit policy/approval gates. Uncertain outcomes require reconciliation rather than blind retries.

## 6. Current verification limits

The package's JSON/YAML and ZIP checks can be run independently, but C# compilation and tests have not been confirmed in the packaging environment because .NET SDK 10 was unavailable. The first successful GitHub Actions run is the next authoritative build/test verification.
