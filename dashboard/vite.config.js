import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
// ✅ FIX 5.3: Security headers plugin for development
var securityHeadersPlugin = function () { return ({
    name: 'security-headers',
    configureServer: function (server) {
        server.middlewares.use(function (_req, res, next) {
            // Content Security Policy (relaxed for dev with HMR)
            res.setHeader('Content-Security-Policy', "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " + // unsafe-eval needed for Vite HMR
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data: blob: https:; " +
                "font-src 'self' data:; " +
                "connect-src 'self' ws://localhost:3000 http://localhost:5000 ws://localhost:5000 http://localhost:6333; " +
                "frame-ancestors 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self';");
            // Prevent MIME sniffing
            res.setHeader('X-Content-Type-Options', 'nosniff');
            // Prevent clickjacking
            res.setHeader('X-Frame-Options', 'DENY');
            // Legacy XSS protection
            res.setHeader('X-XSS-Protection', '1; mode=block');
            // Referrer policy
            res.setHeader('Referrer-Policy', 'strict-origin-when-cross-origin');
            // Permissions policy
            res.setHeader('Permissions-Policy', 'geolocation=(), microphone=(), camera=()');
            next();
        });
    }
}); };
export default defineConfig({
    plugins: [
        react(),
        securityHeadersPlugin()
    ],
    server: {
        port: 3000,
        proxy: {
            '/api': {
                target: 'http://localhost:5000',
                changeOrigin: true
            },
            // Proxy SignalR negotiate and websocket upgrades
            '/hubs': {
                target: 'http://localhost:5000',
                changeOrigin: true,
                ws: true,
            }
        }
    }
});
