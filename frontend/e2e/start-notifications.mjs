// Starts the Notifications service for the Playwright tests (a `webServer` in playwright.config.ts): the inbox, the notification
// emails (real SMTP into Mailpit) and the live push. It takes its events from the broker, where the API's are published.
//
// Like start-api.mjs it compiles into e2e/.artifacts and gives the service its own database, recreated on every run. That script
// DROPS a database, so the same guards apply: the name has to end in `_e2e`, and EF has to name it as the target before it goes.

import { spawn, spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const frontendDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const backendDir = path.resolve(frontendDir, "..", "backend");
const workspacesApiProject = path.join(backendDir, "src", "Services", "Workspaces", "Partnr.Workspaces.Api");
const apiProject = path.join(backendDir, "src", "Services", "Notifications", "Partnr.Notifications.Api");
const infrastructureProject = path.join(backendDir, "src", "Services", "Notifications", "Partnr.Notifications.Infrastructure");
const artifactsPath = path.join(frontendDir, "e2e", ".artifacts");
const apiDll = path.join(artifactsPath, "bin", "Partnr.Notifications.Api", "debug", "Partnr.Notifications.Api.dll");

const TEST_DATABASE = "partnr_notifications_e2e";
const httpPort = process.env.E2E_NOTIFICATIONS_PORT ?? "5155";
const frontendUrl = process.env.E2E_FRONTEND_URL ?? "http://localhost:3100";

/** The Postgres credentials of the dev setup live with the Workspaces API (user secrets first, then appsettings). */
function devConnectionString() {
  const secrets = spawnSync("dotnet", ["user-secrets", "list", "--project", workspacesApiProject], { encoding: "utf8" });
  const fromSecrets = secrets.stdout?.match(/^ConnectionStrings:Postgres = (.+)$/m)?.[1];
  if (fromSecrets) {
    return fromSecrets.trim();
  }

  const settings = JSON.parse(readFileSync(path.join(workspacesApiProject, "appsettings.Development.json"), "utf8"));
  const fromSettings = settings.ConnectionStrings?.Postgres;
  if (fromSettings) {
    return fromSettings;
  }

  throw new Error("No dev connection string found (user secrets or appsettings.Development.json of the Workspaces API).");
}

const databaseOf = (connectionString) => connectionString.match(/(?:^|;)\s*Database=([^;]*)/i)?.[1]?.trim();

function testConnectionString() {
  const explicit = process.env.E2E_NOTIFICATIONS_POSTGRES_CONNECTION;
  if (explicit) {
    return explicit;
  }

  const dev = devConnectionString();
  return databaseOf(dev) === undefined
    ? `${dev};Database=${TEST_DATABASE}`
    : dev.replace(/(^|;)(\s*Database=)[^;]*/i, `$1$2${TEST_DATABASE}`);
}

const connectionString = testConnectionString();

const database = databaseOf(connectionString);
if (!database?.endsWith("_e2e")) {
  console.error(`Refusing to run: the e2e database name must end with "_e2e", got "${database}".`);
  process.exit(1);
}

const env = {
  ...process.env,
  ArtifactsPath: artifactsPath,
  ConnectionStrings__Postgres: connectionString,
  ASPNETCORE_ENVIRONMENT: "Development",
  // The endpoint is set in appsettings.Development.json (HTTP, for the gateway's proxy); this replaces it.
  Kestrel__Endpoints__Http__Url: `http://localhost:${httpPort}`,
  // Own exchanges and queues, so the tests never share them with a development service on the same broker.
  Events__Prefix: "e2e.",
  App__FrontendUrl: frontendUrl,
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

console.log("[e2e] building the Notifications service");
dotnet(["build", apiProject, "--nologo", "-v", "q"]);

// Ask EF which database it would drop and only go ahead if it is the test one. This catches an override that did not apply.
console.log(`[e2e] resetting database ${database}`);
const dryRun = dotnet(["ef", "database", "drop", "--dry-run", ...efArgs]);
if (!dryRun.includes(`'${TEST_DATABASE}'`) && !dryRun.includes(`'${database}'`)) {
  console.error(`Refusing to drop: EF did not report "${database}" as the target.\n${dryRun}`);
  process.exit(1);
}
dotnet(["ef", "database", "drop", "--force", ...efArgs]);
dotnet(["ef", "database", "update", ...efArgs]);

console.log(`[e2e] starting the Notifications service on port ${httpPort}`);
const service = spawn("dotnet", [apiDll], { cwd: apiProject, env, stdio: "inherit" });

const stop = () => service.kill();
process.on("SIGINT", stop);
process.on("SIGTERM", stop);
process.on("exit", stop);
service.on("exit", (code) => process.exit(code ?? 0));
