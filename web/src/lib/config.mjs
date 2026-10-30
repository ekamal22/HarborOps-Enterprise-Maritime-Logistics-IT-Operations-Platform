export function normalizeApiOrigin(value) {
  const input = (value ?? "http://localhost:8080").trim();
  const url = new URL(input);
  if (!["http:", "https:"].includes(url.protocol) || url.username || url.password || url.search || url.hash)
    throw new Error("API origin must use HTTP(S) without credentials or query parameters");
  if (url.pathname !== "/")
    throw new Error("API origin must not contain a path");
  return url.origin;
}
