import { defineConfig, devices } from "@playwright/test";

// Own ports, so the tests never collide with the dev servers (3000 / 5051).
const WEB_PORT = 3100;
const API_PORT = 5151;

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
      env: { E2E_API_PORT: String(API_PORT) },
      reuseExistingServer: false,
      timeout: 240_000,
      stdout: "pipe",
      stderr: "pipe",
    },
    {
      // A production build, like the real thing. BACKEND_URL is needed at build time too: the /api rewrite is baked in then.
      command: `pnpm build && pnpm start --port ${WEB_PORT}`,
      url: `http://localhost:${WEB_PORT}/login`,
      env: { BACKEND_URL: `http://localhost:${API_PORT}` },
      reuseExistingServer: false,
      timeout: 240_000,
    },
  ],
});
