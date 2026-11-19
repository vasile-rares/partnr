import { defineConfig, devices } from "@playwright/test";

// Own ports, so the tests never collide with the dev servers (3000 / 5050 / 5051 / 5052 / 5053 / 5054 / 5055).
const WEB_PORT = 3100;
const GATEWAY_PORT = 5150;
const API_PORT = 5151;
const API_GRPC_PORT = 5152;
const INTEGRATIONS_PORT = 5153;
const INTEGRATIONS_GRPC_PORT = 5154;
const NOTIFICATIONS_PORT = 5155;

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  reporter: [["list"], ["html", { open: "never" }]],

  use: {
    baseURL: `http://localhost:${WEB_PORT}`,
    trace: "retain-on-failure",
  },

  projects: [{ name: "desktop", use: { ...devices["Desktop Chrome"] } }],

  webServer: [
    {
      // Compiles the API, recreates the test database and runs the API against it (see e2e/start-api.mjs).
      command: "node e2e/start-api.mjs",
      url: `http://localhost:${API_PORT}/api/auth/me`, // answers 401 once the API is up, which counts as ready
      env: { E2E_API_PORT: String(API_PORT), E2E_API_GRPC_PORT: String(API_GRPC_PORT), E2E_INTEGRATIONS_GRPC_PORT: String(INTEGRATIONS_GRPC_PORT) },
      reuseExistingServer: false,
      timeout: 240_000,
      stdout: "pipe",
      stderr: "pipe",
    },
    {
      // Google: the connection and the sending of meetings (see e2e/start-integrations.mjs).
      command: "node e2e/start-integrations.mjs",
      url: `http://localhost:${INTEGRATIONS_PORT}/health`,
      env: { E2E_INTEGRATIONS_PORT: String(INTEGRATIONS_PORT), E2E_INTEGRATIONS_GRPC_PORT: String(INTEGRATIONS_GRPC_PORT) },
      reuseExistingServer: false,
      timeout: 240_000,
      stdout: "pipe",
      stderr: "pipe",
    },
    {
      // The inbox, the notification emails and the live push (see e2e/start-notifications.mjs).
      command: "node e2e/start-notifications.mjs",
      url: `http://localhost:${NOTIFICATIONS_PORT}/health`,
      env: { E2E_NOTIFICATIONS_PORT: String(NOTIFICATIONS_PORT) },
      reuseExistingServer: false,
      timeout: 240_000,
      stdout: "pipe",
      stderr: "pipe",
    },
    {
      // The only door to the backend, like in the real thing: the web app's /api rewrite points here (see e2e/start-gateway.mjs).
      command: "node e2e/start-gateway.mjs",
      url: `http://localhost:${GATEWAY_PORT}/api/auth/me`, // 401 through the proxy: the gateway and the API are both up
      env: {
        E2E_GATEWAY_PORT: String(GATEWAY_PORT),
        E2E_API_PORT: String(API_PORT),
        E2E_API_GRPC_PORT: String(API_GRPC_PORT),
        E2E_INTEGRATIONS_PORT: String(INTEGRATIONS_PORT),
        E2E_NOTIFICATIONS_PORT: String(NOTIFICATIONS_PORT),
      },
      reuseExistingServer: false,
      timeout: 240_000,
      stdout: "pipe",
      stderr: "pipe",
    },
    {
      // A production build, like the real thing. BACKEND_URL is needed at build time too: the /api rewrite is baked in then.
      command: `pnpm build && pnpm start --port ${WEB_PORT}`,
      url: `http://localhost:${WEB_PORT}/login`,
      env: { BACKEND_URL: `http://localhost:${GATEWAY_PORT}` },
      reuseExistingServer: false,
      timeout: 240_000,
    },
  ],
});
