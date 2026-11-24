// Starts the Storage service for the Playwright tests (a `webServer` in playwright.config.ts): the bytes of the uploaded files, in an
// S3-compatible bucket (RustFS in docker-compose.yml), served to the API over gRPC. It takes the deletions from the broker.
//
// Like start-api.mjs it compiles into e2e/.artifacts and gives the service its own database, recreated on every run. That script
// DROPS a database, so the same guards apply: the name has to end in `_e2e`, and EF has to name it as the target before it goes.

import { spawn, spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const frontendDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const backendDir = path.resolve(frontendDir, "..", "backend");
const coreApiProject = path.join(backendDir, "src", "Services", "Core", "Partnr.Core.Api");
const apiProject = path.join(backendDir, "src", "Services", "Storage", "Partnr.Storage.Api");
const infrastructureProject = path.join(backendDir, "src", "Services", "Storage", "Partnr.Storage.Infrastructure");
const artifactsPath = path.join(frontendDir, "e2e", ".artifacts");
const apiDll = path.join(artifactsPath, "bin", "Partnr.Storage.Api", "debug", "Partnr.Storage.Api.dll");

const TEST_DATABASE = "partnr_storage_e2e";
const httpPort = process.env.E2E_STORAGE_PORT ?? "5156";
const grpcPort = process.env.E2E_STORAGE_GRPC_PORT ?? "5157";
const storageUrl = process.env.E2E_STORAGE_URL ?? "http://localhost:9000";
const storageBucket = process.env.E2E_STORAGE_BUCKET ?? "partnr-e2e";

/** The Postgres credentials of the dev setup live with the Core API (user secrets first, then appsettings). */
function devConnectionString() {
  const secrets = spawnSync("dotnet", ["user-secrets", "list", "--project", coreApiProject], { encoding: "utf8" });
  const fromSecrets = secrets.stdout?.match(/^ConnectionStrings:Postgres = (.+)$/m)?.[1];
  if (fromSecrets) {
    return fromSecrets.trim();
  }

  const settings = JSON.parse(readFileSync(path.join(coreApiProject, "appsettings.Development.json"), "utf8"));
  const fromSettings = settings.ConnectionStrings?.Postgres;
  if (fromSettings) {
    return fromSettings;
  }

  throw new Error("No dev connection string found (user secrets or appsettings.Development.json of the Core API).");
}

const databaseOf = (connectionString) => connectionString.match(/(?:^|;)\s*Database=([^;]*)/i)?.[1]?.trim();

function testConnectionString() {
  const explicit = process.env.E2E_STORAGE_POSTGRES_CONNECTION;
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

// The bytes go to the S3-compatible storage (RustFS in docker-compose.yml). Say so up front if it is not there.
try {
  // Any answer under 500 means it is up: without a signature the S3 endpoint refuses with 403.
  const response = await fetch(storageUrl);
  if (response.status >= 500) {
    throw new Error(`HTTP ${response.status}`);
  }
} catch {
  console.error(`The file storage is not reachable at ${storageUrl}. Start it with "docker compose up -d" in backend/ (the e2e tests upload and download files).`);
  process.exit(1);
}

const env = {
  ...process.env,
  ArtifactsPath: artifactsPath,
  ConnectionStrings__Postgres: connectionString,
  ASPNETCORE_ENVIRONMENT: "Development",
  // Its own bucket, created on first use, so the tests never touch the files of the dev one.
  Storage__ServiceUrl: storageUrl,
  Storage__Bucket: storageBucket,
  // The endpoints are set in appsettings.Development.json (HTTP for the health check, HTTP/2 for the API's gRPC calls); these replace them.
  Kestrel__Endpoints__Http__Url: `http://localhost:${httpPort}`,
  Kestrel__Endpoints__Grpc__Url: `http://localhost:${grpcPort}`,
  // Own exchanges and queues, so the tests never share them with a development service on the same broker.
  Events__Prefix: "e2e.",
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

console.log("[e2e] building the Storage service");
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

console.log(`[e2e] starting the Storage service on port ${httpPort}`);
const service = spawn("dotnet", [apiDll], { cwd: apiProject, env, stdio: "inherit" });

const stop = () => service.kill();
process.on("SIGINT", stop);
process.on("SIGTERM", stop);
process.on("exit", stop);
service.on("exit", (code) => process.exit(code ?? 0));
