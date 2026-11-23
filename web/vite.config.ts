import react, { reactCompilerPreset } from '@vitejs/plugin-react'
import babel from '@rolldown/plugin-babel'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    babel({ presets: [reactCompilerPreset()] })
  ],
  server: {
    // The browser only talks to the dev server, so the API and its cookies are same-origin.
    proxy: {
      '/api': 'http://localhost:5114',
    },
  },
})
