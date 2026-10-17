// Starts the .NET API for the Playwright tests (the first `webServer` in playwright.config.ts).
//
// Playwright starts its web servers before any global setup, so "compile -> reset the test database -> run the API"
// lives here. The API gets its own database (`client_portal_e2e`), recreated on every run, and is compiled into
// e2e/.artifacts so it never collides with a dev API that has bin/ locked.
//
// This script DROPS a database, so it has three guards before anything destructive happens (the numbered comments).

import { spawn, spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const frontendDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const backendDir = path.resolve(frontendDir, "..", "backend");
const apiProject = path.join(backendDir, "src", "ClientPortal.Api");
const infrastructureProject = path.join(backendDir, "src", "ClientPortal.Infrastructure");
const artifactsPath = path.join(frontendDir, "e2e", ".artifacts");
const apiDll = path.join(artifactsPath, "bin", "ClientPortal.Api", "debug", "ClientPortal.Api.dll");

const TEST_DATABASE = "client_portal_e2e";
const apiPort = process.env.E2E_API_PORT ?? "5151";
const mailpitUrl = process.env.MAILPIT_URL ?? "http://localhost:8025";

/** The dev connection string: user secrets first (that is where a local Postgres password lives), then appsettings. */
function devConnectionString() {
  const secrets = spawnSync("dotnet", ["user-secrets", "list", "--project", apiProject], { encoding: "utf8" });
  const fromSecrets = secrets.stdout?.match(/^ConnectionStrings:Postgres = (.+)$/m)?.[1];
  if (fromSecrets) {
    return fromSecrets.trim();
  }

  const settings = JSON.parse(readFileSync(path.join(apiProject, "appsettings.Development.json"), "utf8"));
  const fromSettings = settings.ConnectionStrings?.Postgres;
  if (fromSettings) {
    return fromSettings;
  }

  throw new Error("No dev connection string found (user secrets or appsettings.Development.json).");
}

const databaseOf = (connectionString) => connectionString.match(/(?:^|;)\s*Database=([^;]*)/i)?.[1]?.trim();

function testConnectionString() {
  // 1. An explicit override wins; otherwise reuse the dev server and credentials but point at the test database.
  const explicit = process.env.E2E_POSTGRES_CONNECTION;
  if (explicit) {
    return explicit;
  }

  const dev = devConnectionString();
  return databaseOf(dev) === undefined
    ? `${dev};Database=${TEST_DATABASE}`
    : dev.replace(/(^|;)(\s*Database=)[^;]*/i, `$1$2${TEST_DATABASE}`);
}

const connectionString = testConnectionString();

// 2. Never touch a database that is not clearly a test database. (The message deliberately omits the connection string.)
const database = databaseOf(connectionString);
if (!database?.endsWith("_e2e")) {
  console.error(`Refusing to run: the e2e database name must end with "_e2e", got "${database}".`);
  process.exit(1);
}

// Invitations send real SMTP, so a missing Mailpit would fail the tests with a confusing 500. Say so up front instead.
try {
  const response = await fetch(`${mailpitUrl}/livez`);
  if (!response.ok) {
    throw new Error(`HTTP ${response.status}`);
  }
} catch {
  console.error(`Mailpit is not reachable at ${mailpitUrl}. Start it with "docker compose up -d" in backend/ (the e2e tests read invitation emails from it).`);
  process.exit(1);
}

const env = {
  ...process.env,
  ArtifactsPath: artifactsPath,
  ConnectionStrings__Postgres: connectionString,
  ASPNETCORE_ENVIRONMENT: "Development", // outside Development the auth cookie is Secure-only and would not work over http
  ASPNETCORE_URLS: `http://localhost:${apiPort}`,
  // Links in emails must open the frontend that the tests run against (see playwright.config.ts), not the dev one on :3000.
  App__FrontendUrl: process.env.E2E_FRONTEND_URL ?? "http://localhost:3100",
};

function dotnet(args, { cwd = backendDir } = {}) {
  const result = spawnSync("dotnet", args, { cwd, env, encoding: "utf8" });
  if (result.status !== 0) {
    console.error(`dotnet ${args.slice(0, 3).join(" ")} failed:\n${result.stdout}\n${result.stderr}`);
    process.exit(1);
  }
  return result.stdout;
}

const efArgs = ["--no-build", "--project", infrastructureProject, "--startup-project", apiProject];

console.log("[e2e] building the API");
dotnet(["build", apiProject, "--nologo", "-v", "q"]);

// 3. Ask EF which database it would drop and only go ahead if it is the test one. This catches an override that did not apply.
console.log(`[e2e] resetting database ${database}`);
const dryRun = dotnet(["ef", "database", "drop", "--dry-run", ...efArgs]);
if (!dryRun.includes(`'${TEST_DATABASE}'`) && !dryRun.includes(`'${database}'`)) {
  console.error(`Refusing to drop: EF did not report "${database}" as the target.\n${dryRun}`);
  process.exit(1);
}
dotnet(["ef", "database", "drop", "--force", ...efArgs]);
dotnet(["ef", "database", "update", ...efArgs]);

console.log(`[e2e] starting the API on port ${apiPort}`);
const api = spawn("dotnet", [apiDll], { cwd: apiProject, env, stdio: "inherit" });

const stop = () => api.kill();
process.on("SIGINT", stop);
process.on("SIGTERM", stop);
process.on("exit", stop);
api.on("exit", (code) => process.exit(code ?? 0));
