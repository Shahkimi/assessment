/**
 * Relative on purpose: in Docker/Kubernetes nginx serves the app and proxies /api to the backend,
 * and `ng serve` does the same via proxy.conf.json. One origin means no CORS and no per-environment URL.
 */
export const API_BASE_URL = '/api';
