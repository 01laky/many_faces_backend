/**
 * BE-RP35 — k6 load harness stub (mixed traffic profile).
 *
 * Usage:
 *   k6 run scripts/backend-load-test.k6.js
 *
 * Env (k6 -e or shell):
 *   BE_PERF_BASE_URL, BE_PERF_EMAIL, BE_PERF_PASSWORD,
 *   BE_PERF_CLIENT_ID, BE_PERF_CLIENT_SECRET, BE_PERF_FACE_PREFIX
 */
import http from "k6/http";
import { check, sleep } from "k6";
import { Trend, Rate } from "k6/metrics";

const baseUrl = (__ENV.BE_PERF_BASE_URL || "http://localhost:8000").replace(
  /\/$/,
  "",
);
const facePrefix = __ENV.BE_PERF_FACE_PREFIX || "public";
const email = __ENV.BE_PERF_EMAIL || "user01@demo.com";
const password = __ENV.BE_PERF_PASSWORD || "user123";
const clientId = __ENV.BE_PERF_CLIENT_ID || "be-demo-client";
const clientSecret =
  __ENV.BE_PERF_CLIENT_SECRET || "be-demo-secret-very-strong-key";

const authLatency = new Trend("auth_latency_ms", true);
const hotPathLatency = new Trend("hot_path_latency_ms", true);
const errorRate = new Rate("errors");

export const options = {
  scenarios: {
    mixed: {
      executor: "constant-vus",
      vus: 100,
      duration: "5m",
    },
  },
  thresholds: {
    errors: ["rate<0.01"],
    hot_path_latency_ms: ["p(95)<2000"],
  },
};

let cachedToken = "";

function fetchToken() {
  const res = http.post(
    `${baseUrl}/api/oauth2/token`,
    JSON.stringify({
      grantType: "password",
      clientId,
      clientSecret,
      username: email,
      password,
    }),
    { headers: { "Content-Type": "application/json" } },
  );
  authLatency.add(res.timings.duration);
  const ok = check(res, { "token 200": (r) => r.status === 200 });
  errorRate.add(!ok);
  if (!ok) return "";
  try {
    return JSON.parse(res.body).accessToken || "";
  } catch {
    return "";
  }
}

function authHeaders(token) {
  return {
    Authorization: `Bearer ${token}`,
    Accept: "application/json",
  };
}

export function setup() {
  const token = fetchToken();
  if (!token) {
    throw new Error("Setup: could not obtain OAuth token — is the API running?");
  }
  return { token };
}

export default function (data) {
  if (!cachedToken) cachedToken = data.token || fetchToken();
  const roll = Math.random();
  let res;

  if (roll < 0.4) {
    // 40% authenticated hot path (profile + capabilities)
    const path =
      Math.random() < 0.5
        ? `${baseUrl}/api/profile/me`
        : `${baseUrl}/${facePrefix}/api/me/capabilities`;
    res = http.get(path, { headers: authHeaders(cachedToken), tags: { path: "hot" } });
  } else if (roll < 0.7) {
    // 30% faces config
    res = http.get(`${baseUrl}/${facePrefix}/api/faces/config`, {
      headers: authHeaders(cachedToken),
      tags: { path: "faces-config" },
    });
  } else if (roll < 0.9) {
    // 20% grid snapshot (public face id 1 is typical demo seed)
    res = http.get(
      `${baseUrl}/${facePrefix}/api/faces/1/grid-snapshot?blocks=albums,blogs,reels&page=0`,
      { headers: authHeaders(cachedToken), tags: { path: "grid-snapshot" } },
    );
  } else {
    // 10% public stats
    res = http.get(`${baseUrl}/${facePrefix}/api/Stats/public`, {
      tags: { path: "stats-public" },
    });
  }

  hotPathLatency.add(res.timings.duration);
  const ok = check(res, { "status 2xx": (r) => r.status >= 200 && r.status < 300 });
  errorRate.add(!ok);
  sleep(0.1 + Math.random() * 0.2);
}

export function handleSummary(data) {
  const out = {
    schemaVersion: 1,
    engagement: "BE-RP35",
    generatedAt: new Date().toISOString(),
    baseUrl,
    metrics: data.metrics,
  };
  return {
    stdout: JSON.stringify(out, null, 2),
    "dist/backend-load-test-summary.json": JSON.stringify(out, null, 2),
  };
}
