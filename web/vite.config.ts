import { defineConfig } from 'vite';
import preact from '@preact/preset-vite';

// The mod serves the built UI from its wwwroot folder.
// In `npm run dev`, API calls are proxied to the running game; the Origin header is dropped
// because the mod refuses cross-origin calls.
export default defineConfig({
  plugins: [preact()],
  build: {
    outDir: '../mod/ValleyEditor/wwwroot',
    emptyOutDir: true,
  },
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:47800',
        configure: (proxy) => proxy.on('proxyReq', (req) => req.removeHeader('origin')),
      },
    },
  },
});
