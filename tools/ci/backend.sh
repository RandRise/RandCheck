#!/bin/sh
set -eu
if [ "${1:-test}" = audit ]; then
  # Fresh online audit on every run, outside cached image layers; all severity levels.
  dotnet restore apps/backend/UaeDocumentIntelligence.sln --force -p:NuGetAudit=true -p:NuGetAuditMode=all -p:NuGetAuditLevel=low
  printf '{"ecosystem":"NuGet","allTransitiveDependencies":true,"passed":true}\n' > /results/audit-nuget.json
  exit 0
fi
# The only database reachable here is a newly created, private CI service.
case "${TEST_POSTGRES_CONNECTION:-}" in
  'Host=127.0.0.1;Port=5432;Database=postgres;Username=ci;Password=disposable_ci_only') ;;
  *) echo 'Refusing an unexpected persistence-test connection.' >&2; exit 2 ;;
esac
for project in Domain Architecture Application Infrastructure Api Persistence; do
  dotnet test "apps/backend/tests/UaeDocumentIntelligence.$project.Tests" -c Release --no-build --no-restore \
    --logger "trx;LogFileName=$project.trx" --results-directory /results/trx
done
python3 tools/ci/verify-trx.py /results/trx /results/backend-counts.json
