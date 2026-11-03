#!/usr/bin/env python3
"""Read-only publication governance check using the authenticated GitHub CLI."""
import json
import os
from pathlib import Path
import subprocess
import sys

REPOSITORY = "ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform"


def github(resource):
    path = "repos/" + REPOSITORY + ("/" + resource if resource else "")
    result = subprocess.run(["gh", "api", path], text=True, capture_output=True)
    if result.returncode:
        return None
    try:
        return json.loads(result.stdout)
    except ValueError:
        return None


def main():
    if len(sys.argv) != 2:
        sys.exit("Usage: python scripts/check-publication-governance.py E:/evidence/governance.json")
    target = str(sys.argv[1]).replace("\\", "/").lower()
    if not target.startswith(("e:/", "/mnt/e/", "/e/")):
        sys.exit("Only E:-backed governance evidence is permitted.")

    repo = github("")
    rules = github("rulesets")
    branch = github("branches/main")
    protection = github("branches/main/protection")
    actions = github("actions/permissions")
    if not isinstance(repo, dict):
        sys.exit("Unable to inspect repository access.")

    reasons = []
    if repo.get("private") is not True:
        reasons.append("repository no longer private")
    if not isinstance(branch, dict) or branch.get("protected") is not True:
        reasons.append("main does not report enabled protection")
    if actions is None or actions.get("enabled") is not False:
        reasons.append("hosted Actions are not confirmed disabled")

    legacy = isinstance(protection, dict) and isinstance(
        protection.get("required_pull_request_reviews"), dict) and (
            protection["required_pull_request_reviews"].get("required_approving_review_count", 0) >= 1
        ) and not protection.get("allow_force_pushes", {}).get("enabled", False) and not (
            protection.get("allow_deletions", {}).get("enabled", False)
        )

    # If the ruleset response is available, record it for manual target/bypass review.
    active = []
    if isinstance(rules, list):
        active = [{"name": item.get("name"), "enforcement": item.get("enforcement"),
                   "target": item.get("target"), "id": item.get("id")}
                  for item in rules if item.get("enforcement") == "active"]
    if not legacy and not active:
        reasons.append("no effective pull-request review rule verified")
    if active and not legacy:
        reasons.append("active rulesets require manual target, review and bypass confirmation")

    result = {
        "repository": REPOSITORY,
        "private": repo.get("private"),
        "main_protected": branch.get("protected") if isinstance(branch, dict) else None,
        "actions_disabled": actions.get("enabled") is False if isinstance(actions, dict) else None,
        "legacy_review_protection": legacy,
        "active_rulesets": active,
        "pending": reasons,
        "publication_clearance": False
    }
    path = Path(sys.argv[1])
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))
    return 1 if reasons else 0


if __name__ == "__main__":
    raise SystemExit(main())
