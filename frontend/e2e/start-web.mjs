// Starts the web app for the Playwright tests (the last `webServer` in playwright.config.ts): a production build, like the real
// thing, served on the test port. The build takes the better part of a minute, so it is reused when nothing it depends on changed
// since the last test run: the sources, the config files and the gateway address (the /api rewrite is baked into the build).

import { spawn, spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { existsSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const frontendDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const nextBin = path.join(frontendDir, "node_modules", "next", "dist", "bin", "next");
const buildIdFile = path.join(frontendDir, ".next", "BUILD_ID");
const markerFile = path.join(frontendDir, ".next", "e2e-build.json");

const port = process.env.E2E_WEB_PORT ?? "3100";
const env = { ...process.env };

/** What the build is made of: the sources, the config files and the gateway address. */
function fingerprint() {
  const hash = createHash("sha256");
  hash.update(`BACKEND_URL=${env.BACKEND_URL ?? ""}\n`);

  const files = ["package.json", "pnpm-lock.yaml", "next.config.ts", "tsconfig.json", "postcss.config.mjs", "components.json"];
  for (const directory of ["src", "public"]) {
    const walk = (current) => {
      for (const entry of readdirSync(current, { withFileTypes: true })) {
        const full = path.join(current, entry.name);
        if (entry.isDirectory()) {
          walk(full);
        } else {
          files.push(path.relative(frontendDir, full));
        }
      }
    };
    walk(path.join(frontendDir, directory));
  }

  for (const file of files.sort()) {
    const full = path.join(frontendDir, file);
    if (existsSync(full)) {
      hash.update(`${file.replaceAll("\\", "/")}\n`).update(readFileSync(full)).update("\n");
    }
  }

  return hash.digest("hex");
}

/** The last build is ours and still there: its marker matches the sources and the build id (a build made by hand changes the id). */
function canReuseBuild(current) {
  if (!existsSync(buildIdFile) || !existsSync(markerFile)) {
    return false;
  }

  try {
    const marker = JSON.parse(readFileSync(markerFile, "utf8"));
    return marker.fingerprint === current && marker.buildId === readFileSync(buildIdFile, "utf8").trim();
  } catch {
    return false;
  }
}

const current = fingerprint();
if (canReuseBuild(current)) {
  console.log("[e2e] the web app is unchanged since the last run: reusing its production build");
} else {
  console.log("[e2e] building the web app");
  const build = spawnSync(process.execPath, [nextBin, "build"], { cwd: frontendDir, env, stdio: "inherit" });
  if (build.status !== 0) {
    process.exit(build.status ?? 1);
  }

  writeFileSync(markerFile, JSON.stringify({ fingerprint: current, buildId: readFileSync(buildIdFile, "utf8").trim() }));
}

console.log(`[e2e] starting the web app on port ${port}`);
const web = spawn(process.execPath, [nextBin, "start", "--port", port], { cwd: frontendDir, env, stdio: "inherit" });

const stop = () => web.kill();
process.on("SIGINT", stop);
process.on("SIGTERM", stop);
process.on("exit", stop);
web.on("exit", (code) => process.exit(code ?? 0));
