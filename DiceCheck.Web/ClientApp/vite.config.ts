import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: 'https://localhost:7000',
        secure: false
      }
    }
  },
  build: {
    outDir: '../wwwroot',
    emptyOutDir: true
  }
});
