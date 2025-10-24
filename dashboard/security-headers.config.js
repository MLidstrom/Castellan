/**
 * ✅ FIX 5.3: Content Security Policy and Security Headers Configuration
 *
 * This file contains recommended security headers for the CastellanAI dashboard.
 * Apply these headers in your web server (Nginx, Apache, IIS) or backend (ASP.NET Core, etc.)
 *
 * Usage Examples:
 *
 * **ASP.NET Core (Program.cs)**:
 * ```csharp
 * app.Use(async (context, next) =>
 * {
 *     context.Response.Headers.Add("Content-Security-Policy", cspHeader);
 *     context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
 *     context.Response.Headers.Add("X-Frame-Options", "DENY");
 *     context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
 *     context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
 *     context.Response.Headers.Add("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
 *     await next();
 * });
 * ```
 *
 * **Nginx**:
 * ```nginx
 * add_header Content-Security-Policy "...";
 * add_header X-Content-Type-Options "nosniff";
 * add_header X-Frame-Options "DENY";
 * ```
 *
 * **Apache (.htaccess)**:
 * ```apache
 * Header set Content-Security-Policy "..."
 * Header set X-Content-Type-Options "nosniff"
 * Header set X-Frame-Options "DENY"
 * ```
 */

/**
 * Content Security Policy (CSP)
 *
 * Defines which resources can be loaded and from where.
 * This significantly reduces XSS attack surface.
 */
const contentSecurityPolicy = {
  // Default fallback for all resource types
  'default-src': ["'self'"],

  // Scripts: Allow self and inline scripts (needed for Vite dev)
  // PRODUCTION: Remove 'unsafe-inline' and use nonces or hashes
  'script-src': [
    "'self'",
    "'unsafe-inline'", // ⚠️ Remove in production, use nonces
    "'unsafe-eval'",   // ⚠️ Needed for dev mode, remove in production
  ],

  // Styles: Allow self and inline styles (Tailwind CSS generates inline styles)
  'style-src': [
    "'self'",
    "'unsafe-inline'", // Required for Tailwind CSS
  ],

  // Images: Allow self, data URIs (base64), and external threat intel sources
  'img-src': [
    "'self'",
    'data:',
    'blob:',
    'https://www.virustotal.com',
    'https://otx.alienvault.com',
  ],

  // Fonts: Allow self and data URIs
  'font-src': ["'self'", 'data:'],

  // Connect (AJAX/WebSocket): Allow API and SignalR connections
  'connect-src': [
    "'self'",
    'http://localhost:5000',  // Worker API
    'ws://localhost:5000',    // SignalR WebSocket
    'http://localhost:6333',  // Qdrant vector DB (if accessed from frontend)
  ],

  // Media: Allow self
  'media-src': ["'self'"],

  // Objects: Disallow plugins (Flash, Java, etc.)
  'object-src': ["'none'"],

  // Frame ancestors: Prevent clickjacking
  'frame-ancestors': ["'none'"],

  // Base URI: Restrict document base URL
  'base-uri': ["'self'"],

  // Form actions: Only allow forms to submit to self
  'form-action': ["'self'"],

  // Upgrade insecure requests in production
  // 'upgrade-insecure-requests': true, // Uncomment for HTTPS-only production
};

/**
 * Convert CSP object to header string
 */
function buildCSPHeader(policy) {
  return Object.entries(policy)
    .map(([directive, sources]) => {
      if (typeof sources === 'boolean') {
        return directive;
      }
      return `${directive} ${sources.join(' ')}`;
    })
    .join('; ');
}

/**
 * Security Headers Configuration
 */
const securityHeaders = {
  /**
   * Content Security Policy
   * Prevents XSS by controlling resource loading
   */
  'Content-Security-Policy': buildCSPHeader(contentSecurityPolicy),

  /**
   * X-Content-Type-Options
   * Prevents MIME sniffing attacks
   */
  'X-Content-Type-Options': 'nosniff',

  /**
   * X-Frame-Options
   * Prevents clickjacking by disallowing iframe embedding
   */
  'X-Frame-Options': 'DENY',

  /**
   * X-XSS-Protection
   * Legacy XSS filter for older browsers
   * Note: Modern browsers rely on CSP instead
   */
  'X-XSS-Protection': '1; mode=block',

  /**
   * Referrer-Policy
   * Controls how much referrer information is sent
   */
  'Referrer-Policy': 'strict-origin-when-cross-origin',

  /**
   * Permissions-Policy
   * Controls browser features and APIs
   * Disable features not needed by the dashboard
   */
  'Permissions-Policy': 'geolocation=(), microphone=(), camera=(), payment=(), usb=()',

  /**
   * Strict-Transport-Security (HSTS)
   * Forces HTTPS connections (only in production with HTTPS)
   * Uncomment when deploying with HTTPS:
   */
  // 'Strict-Transport-Security': 'max-age=31536000; includeSubDomains; preload',
};

/**
 * Production-optimized CSP
 * Remove unsafe directives and add nonce support
 */
const productionCSP = {
  ...contentSecurityPolicy,
  'script-src': [
    "'self'",
    // Use nonces in production: "'nonce-{RANDOM_VALUE}'"
    // Generate nonce server-side and inject into HTML
  ],
  'connect-src': [
    "'self'",
    // Replace with production API URLs
    'https://api.yourdomain.com',
    'wss://api.yourdomain.com',
  ],
  // Enable upgrade-insecure-requests in production
  'upgrade-insecure-requests': true,
};

module.exports = {
  securityHeaders,
  contentSecurityPolicy,
  productionCSP,
  buildCSPHeader,
};
