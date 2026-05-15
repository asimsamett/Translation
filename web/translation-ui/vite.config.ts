import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

const isGitHubPages = process.env.GITHUB_PAGES === "true";
const repositoryName = process.env.GITHUB_REPOSITORY?.split("/")[1];

export default defineConfig({
  plugins: [react()],
  base: isGitHubPages && repositoryName ? `/${repositoryName}/` : "/",
  build: {
    outDir: process.env.VITE_OUT_DIR ?? "../../src/Translation.Api/wwwroot",
    emptyOutDir: true
  },
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: "https://localhost:7297",
        changeOrigin: true,
        secure: false
      }
    }
  }
});
