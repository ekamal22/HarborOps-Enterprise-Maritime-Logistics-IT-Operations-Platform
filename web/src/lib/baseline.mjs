export function parseBaselineStatus(payload) {
  if (payload === null || typeof payload !== "object" || Array.isArray(payload))
    throw new Error("Unexpected baseline response");

  const { service, datasetVersion, seededAtUtc, source } = payload;

  if (service !== "HarborOps" || datasetVersion !== "m0-synthetic-v1" ||
      source !== "synthetic" || typeof seededAtUtc !== "string" ||
      !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|\+00:00)$/.test(seededAtUtc))
    throw new Error("Unexpected baseline response");

  const timestamp = Date.parse(seededAtUtc);
  if (!Number.isFinite(timestamp))
    throw new Error("Unexpected baseline timestamp");

  return { service, datasetVersion, seededAtUtc, source };
}
