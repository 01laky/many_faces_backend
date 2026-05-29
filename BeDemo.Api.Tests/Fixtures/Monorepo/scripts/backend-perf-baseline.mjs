#!/usr/bin/env node
/**
 * Backend perf baseline — curl-like fetch against local API (BE-RP21).
 * Usage: node scripts/backend-perf-baseline.mjs [baseUrl]
 *
 * Env: BE_PERF_BASE_URL, BE_PERF_EMAIL, BE_PERF_PASSWORD, BE_PERF_CLIENT_ID,
 *      BE_PERF_CLIENT_SECRET, BE_PERF_FACE_PREFIX, BE_PERF_SAMPLES (default 50)
 */
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const rootDir = path.resolve(__dirname, "..");
const baseUrl = (
  process.argv[2] ??
  process.env.BE_PERF_BASE_URL ??
  "http://localhost:8000"
).replace(/\/$/, "");

const email = process.env.BE_PERF_EMAIL ?? "user01@demo.com";
const password = process.env.BE_PERF_PASSWORD ?? "user123";
const clientId = process.env.BE_PERF_CLIENT_ID ?? "be-demo-client";
const clientSecret =
  process.env.BE_PERF_CLIENT_SECRET ?? "be-demo-secret-very-strong-key";
const facePrefix = process.env.BE_PERF_FACE_PREFIX ?? "public";
const samples = Math.max(10, Number(process.env.BE_PERF_SAMPLES ?? 50));

function percentile(sorted, p) {
  if (sorted.length === 0) return 0;
  const idx = Math.ceil((p / 100) * sorted.length) - 1;
  return sorted[Math.max(0, Math.min(sorted.length - 1, idx))];
}

async function fetchToken() {
  const res = await fetch(`${baseUrl}/api/oauth2/token`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify({
      grantType: "password",
      clientId,
      clientSecret,
      username: email,
      password,
    }),
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`OAuth token failed ${res.status}: ${text.slice(0, 200)}`);
  }
  const body = await res.json();
  if (!body.accessToken) throw new Error("OAuth response missing accessToken");
  return body.accessToken;
}

async function timedFetch(label, url, headers = {}) {
  const start = performance.now();
  let status = 0;
  let ok = false;
  try {
    const res = await fetch(url, { headers, signal: AbortSignal.timeout(30_000) });
    status = res.status;
    ok = res.ok;
    await res.arrayBuffer();
  } catch (err) {
    status = 0;
    ok = false;
  }
  const ms = performance.now() - start;
  return { label, ms, status, ok };
}

async function benchEndpoint(label, url, headers, n) {
  const times = [];
  let errors = 0;
  for (let i = 0; i < n; i++) {
    const r = await timedFetch(label, url, headers);
    times.push(r.ms);
    if (!r.ok) errors++;
  }
  times.sort((a, b) => a - b);
  return {
    label,
    url,
    samples: n,
    errors,
    p50Ms: Math.round(percentile(times, 50) * 100) / 100,
    p95Ms: Math.round(percentile(times, 95) * 100) / 100,
    minMs: Math.round(times[0] * 100) / 100,
    maxMs: Math.round(times[times.length - 1] * 100) / 100,
  };
}

async function main() {
  console.error(`BE-RP21 baseline → ${baseUrl} (${samples} samples/endpoint)`);
  const token = await fetchToken();
  const auth = { Authorization: `Bearer ${token}`, Accept: "application/json" };

  const endpoints = [
    {
      label: "profile/me",
      url: `${baseUrl}/api/profile/me`,
      headers: auth,
    },
    {
      label: "faces/config",
      url: `${baseUrl}/${facePrefix}/api/faces/config`,
      headers: auth,
    },
    {
      label: "capabilities",
      url: `${baseUrl}/${facePrefix}/api/me/capabilities`,
      headers: auth,
    },
    {
      label: "stats/public",
      url: `${baseUrl}/${facePrefix}/api/Stats/public`,
      headers: { Accept: "application/json" },
    },
  ];

  const results = [];
  for (const ep of endpoints) {
    console.error(`  … ${ep.label}`);
    results.push(await benchEndpoint(ep.label, ep.url, ep.headers, samples));
  }

  const summary = {
    schemaVersion: 1,
    engagement: "BE-RP21",
    generatedAt: new Date().toISOString(),
    baseUrl,
    facePrefix,
    samplesPerEndpoint: samples,
    endpoints: results,
  };

  const outDir = path.join(rootDir, "dist");
  fs.mkdirSync(outDir, { recursive: true });
  const outPath = path.join(outDir, "backend-perf-baseline.json");
  fs.writeFileSync(outPath, `${JSON.stringify(summary, null, 2)}\n`);
  console.log(JSON.stringify(summary, null, 2));
  console.error(`Wrote ${outPath}`);
}

main().catch((err) => {
  console.error(err.message ?? err);
  process.exit(1);
});
