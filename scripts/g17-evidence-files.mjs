// Diagnostics are captured only from Compose services belonging to the
// uniquely named disposable project. Arbitrary .log files remain prohibited.
const diagnosticLog=/^m17-failure-diagnostics\/(?:postgres|postgres_tests|migrator|api|web|otelcol|tempo|prometheus|grafana|dashboard_gateway|worker)\.log$/;
const standard=/\.(?:json|txt|trx|xml|png|webm|zip)$/i;
const standardText=/\.(?:json|txt|trx|xml)$/i;
export function allowedEvidencePath(relative) {
  return typeof relative==="string" && !relative.includes("\\") &&
    !relative.startsWith("/") && !relative.split("/").includes("..") &&
    (standard.test(relative)||diagnosticLog.test(relative));
}
export function textEvidencePath(relative) {
  return allowedEvidencePath(relative) &&
    (standardText.test(relative)||diagnosticLog.test(relative));
}
