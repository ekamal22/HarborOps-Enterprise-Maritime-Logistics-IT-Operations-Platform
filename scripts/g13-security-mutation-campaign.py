#!/usr/bin/env python3
"""Execute scoped SQL policy mutations only against exclusive disposable PostgreSQL."""
from __future__ import annotations
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parent.parent
TARGET = ROOT / "src/HarborOps.Infrastructure/Persistence/Migrations/20261109020000_M13OperationalKpis.cs"
OUT = ROOT / ".verification/g13-security-mutations"
FILTER = ("FullyQualifiedName~M13OperationalKpiRuntimeTests.ForgedTokensUnsafeQueriesAndDirectSqlCannotDiscloseReports"
          "|FullyQualifiedName~M13OperationalKpiRuntimeTests.SignedAdminAndUngrantedAuditorScopesCannotReadKpiData")
MUTANTS = [
    ("report-select-unrestricted",
        """CREATE POLICY m13_report_select ON operational_kpi_reports
                FOR SELECT USING (m13_can_view(organization_id)
                    AND m13_scope_matches(organization_id,actor_user_id,actor_session_id));""",
        """CREATE POLICY m13_report_select ON operational_kpi_reports
                FOR SELECT USING (true);"""),
    ("administrator-sql-grant-bypass",
        "IF role_name<>'Auditor' THEN RETURN false; END IF;",
        "IF role_name<>'Auditor' THEN RETURN true; END IF;",
        "SignedAdminAndUngrantedAuditorScopesCannotReadKpiData"),
    ("auditor-sql-grant-bypass",
        "IF role_name<>'Auditor' THEN RETURN false; END IF;",
        "IF role_name<>'Auditor' THEN RETURN false; END IF; RETURN true;",
        "SignedAdminAndUngrantedAuditorScopesCannotReadKpiData"),
    ("member-select-unrestricted",
        """CREATE POLICY m13_member_select ON operational_kpi_members
                FOR SELECT USING (
                    organization_id=current_setting(
                        'harborops.m13_materialize_org',true)::uuid
                    AND report_id=current_setting(
                        'harborops.m13_materialize_report',true)::uuid
                    AND (SELECT m13_member_access(
                        current_setting('harborops.m13_materialize_org',true)::uuid,
                        current_setting('harborops.m13_materialize_report',true)::uuid))
                );""",
        """CREATE POLICY m13_member_select ON operational_kpi_members
                FOR SELECT USING (true);"""),
]
def sha(content: bytes) -> str:
    return hashlib.sha256(content).hexdigest()

def run(args: list[str], *, env: dict[str,str] | None = None, timeout: int = 400):
    return subprocess.run(args, cwd=ROOT, env=env, check=False,
                          text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                          timeout=timeout)
def db_call(container: str, sql: str, database: str):
    result=run(["docker","exec",container,"psql","-v","ON_ERROR_STOP=1",
                "-U","harborops","-d",database,"-c",sql],timeout=60)
    if result.returncode:
        raise RuntimeError(f"Disposable database setup failed: {result.stdout[-1500:]}")

def main() -> int:
    if os.getenv("HARBOROPS_G13_EXCLUSIVE_POSTGRES") != "1":
        raise RuntimeError("Exclusive disposable PostgreSQL required")
    base=os.environ["HARBOROPS_TEST_DB"]
    match=re.search(r"Database=([a-zA-Z0-9_]+)",base)
    if not match:
        raise RuntimeError("No predictable disposable database name")
    found=run(["docker","ps","--filter","ancestor=postgres:16.15","--format","{{.ID}}"])
    active=found.stdout.strip().splitlines()
    if len(active)!=1:
        raise RuntimeError("Exactly one disposable PostgreSQL container required")
    container=active[0]
    OUT.mkdir(parents=True,exist_ok=True)
    original=TARGET.read_bytes()
    original_text=original.decode()
    results=[]
    try:
        for i,case in enumerate([("clean-control","","")]+MUTANTS):
            name,before,after,*case_target = case
            active_filter = ("FullyQualifiedName~" + case_target[0]
                             if case_target else FILTER)
            if before and original_text.count(before)!=1:
                raise RuntimeError(f"Mutation {name} anchor count {original_text.count(before)}")
            source=original_text.replace(before,after) if before else original_text
            TARGET.write_text(source,encoding="utf-8")
            database=match.group(1)
            if database not in ("harborops_integration","harborops_test"):
                raise RuntimeError("Only the explicitly disposable test database is permitted")
            # The test harness intentionally rejects arbitrary database names.
            # Restore an empty public schema between variants on this one
            # exclusive GitHub-hosted PostgreSQL service.
            db_call(container,
                "DROP SCHEMA IF EXISTS public CASCADE; "
                "CREATE SCHEMA public AUTHORIZATION harborops; "
                "GRANT USAGE,CREATE ON SCHEMA public TO PUBLIC;",
                database)
            env=os.environ.copy()
            start=time.monotonic()
            try:
                result=run(["dotnet","test",
                            "tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj",
                            "-c","Release","--filter",active_filter,
                            "--logger","console;verbosity=normal"],env=env)
                (OUT / f"{name}.log").write_text(result.stdout,encoding="utf-8")
                if "error CS" in result.stdout or "Build FAILED" in result.stdout or result.returncode==124:
                    verdict="INVALID"
                elif name=="clean-control":
                    verdict="PASS" if result.returncode==0 else "FAIL"
                elif result.returncode==0:
                    verdict="SURVIVED"
                elif ("Failed " in result.stdout or "[FAIL]" in result.stdout) and (
                    (case_target[0] if case_target else
                     "ForgedTokensUnsafeQueriesAndDirectSqlCannotDiscloseReports") in result.stdout):
                    verdict="KILLED"
                else:
                    verdict="INVALID"
                results.append({"mutation":name,"verdict":verdict,
                                "exit_code":result.returncode,
                                "source_sha256":sha(source.encode()),
                                "duration_seconds":round(time.monotonic()-start,2)})
                print(f"G13_SECURITY_MUTANT {name} {verdict}",flush=True)
                if name=="clean-control" and verdict!="PASS":
                    break
            finally:
                pass  # Next variant resets this disposable database schema
                TARGET.write_bytes(original)
    finally:
        TARGET.write_bytes(original)
        if sha(TARGET.read_bytes())!=sha(original):
            raise RuntimeError("Source restoration failure")
        (OUT / "manifest.json").write_text(json.dumps({
            "commit":os.getenv("GITHUB_SHA",""),"baseline_sha256":sha(original),
            "test_filter":FILTER,"results":results},indent=2)+"\n",encoding="utf-8")
    return 0 if len(results)==len(MUTANTS)+1 and results[0]["verdict"]=="PASS" and all(
        r["verdict"]=="KILLED" for r in results[1:]) else 1

if __name__=="__main__":
    sys.exit(main())
