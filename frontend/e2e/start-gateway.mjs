// Starts the gateway for the Playwright tests (the second `webServer` in playwright.config.ts): the web app's /api
// rewrite points at it, and it forwards to the test API that start-api.mjs runs. Compiled into e2e/.artifacts, like the
// API, so it never collides with a dev gateway that has bin/ locked.

import { spawn, spawnSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const frontendDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const backendDir = path.resolve(frontendDir, "..", "backend");
const gatewayProject = path.join(backendDir, "src", "Gateway", "Partnr.Gateway");
const artifactsPath = path.join(frontendDir, "e2e", ".artifacts");
const gatewayDll = path.join(artifactsPath, "bin", "Partnr.Gateway", "debug", "Partnr.Gateway.dll");

const gatewayPort = process.env.E2E_GATEWAY_PORT ?? "5150";
const apiPort = process.env.E2E_API_PORT ?? "5151";
const apiGrpcPort = process.env.E2E_API_GRPC_PORT ?? "5152";
const integrationsPort = process.env.E2E_INTEGRATIONS_PORT ?? "5153";
const notificationsPort = process.env.E2E_NOTIFICATIONS_PORT ?? "5155";

const env = {
  ...process.env,
  ArtifactsPath: artifactsPath,
  ASPNETCORE_ENVIRONMENT: "Development",
  ASPNETCORE_URLS: `http://localhost:${gatewayPort}`,
  ReverseProxy__Clusters__workspaces__Destinations__api__Address: `http://localhost:${apiPort}`,
  Services__Workspaces__GrpcAddress: `http://localhost:${apiGrpcPort}`,
  ReverseProxy__Clusters__integrations__Destinations__api__Address: `http://localhost:${integrationsPort}`,
  ReverseProxy__Clusters__notifications__Destinations__api__Address: `http://localhost:${notificationsPort}`,
};

console.log("[e2e] building the gateway");
const build = spawnSync("dotnet", ["build", gatewayProject, "--nologo", "-v", "q"], { cwd: backendDir, env, encoding: "utf8" });
if (build.status !== 0) {
  console.error(`dotnet build failed:\n${build.stdout}\n${build.stderr}`);
  process.exit(1);
}

console.log(`[e2e] starting the gateway on port ${gatewayPort}`);
const gateway = spawn("dotnet", [gatewayDll], { cwd: gatewayProject, env, stdio: "inherit" });

const stop = () => gateway.kill();
process.on("SIGINT", stop);
process.on("SIGTERM", stop);
process.on("exit", stop);
gateway.on("exit", (code) => process.exit(code ?? 0));
