# MoneyBrain PWA Implementation Guide

This document provides comprehensive guidance for the Progressive Web App (PWA) implementation in MoneyBrain.

## 🎯 Overview

MoneyBrain is now a full-featured Progressive Web App with:
- **Offline support** - Works without internet connection
- **Installable** - Add to home screen on any device
- **App-like experience** - Runs in standalone mode
- **Smart caching** - Fast loading and reduced data usage
- **Auto-updates** - Seamless version updates
- **Cross-platform** - iOS, Android, Windows, macOS, Linux

---

## 📋 Implementation Checklist

### ✅ Core PWA Files

- [x] `manifest.json` - App manifest with metadata
- [x] `service-worker.js` - Service worker with caching strategies
- [x] `offline.html` - Offline fallback page
- [x] `pwa.js` - PWA JavaScript integration
- [x] `PwaInstallPrompt.razor` - Install prompt component
- [ ] Icons (72x72 to 512x512) - See `/wwwroot/icons/README.md`
- [ ] Screenshots - See `/wwwroot/screenshots/README.md`

### ✅ Integration Points

- [x] Manifest link in `App.razor`
- [x] PWA meta tags in `App.razor`
- [x] Service worker registration in `pwa.js`
- [x] Install prompt in `MainLayout.razor`
- [x] Apple-specific meta tags for iOS

---

## 🚀 Getting Started

### 1. Generate Icons

The app needs icons in multiple sizes. See `/wwwroot/icons/README.md` for details.

**Quick method:**
```bash
# Install PWA asset generator
npm install -g @pwa/asset-generator

# Generate all icon sizes from a source image
npx @pwa/asset-generator logo.svg wwwroot/icons/ --icon-only
```

**Manual method:**
- Create icons in: 72, 96, 128, 144, 152, 192, 384, 512 pixels
- Ensure 192 and 512 are "maskable" (safe zone design)
- Place in `/wwwroot/icons/`

### 2. Capture Screenshots

Take screenshots for the PWA listing:

**Desktop (1280x720):**
- Open MoneyBrain in Chrome
- Navigate to Dashboard
- Press F12 > Device toolbar > Responsive > Set to 1280x720
- Click screenshot icon or use capture tool

**Mobile (750x1334):**
- F12 > Device toolbar > iPhone X
- Take screenshot of Dashboard, Transactions, Budgets

Place in `/wwwroot/screenshots/`

### 3. Test Installation

**Desktop (Chrome/Edge):**
1. Run the app: `dotnet run`
2. Open in Chrome: https://localhost:7123
3. Look for install icon in address bar
4. Click and select "Install"

**Mobile (iOS Safari):**
1. Access the app on your phone
2. Tap Share button
3. "Add to Home Screen"
4. Confirm installation

**Mobile (Android Chrome):**
1. Open app in Chrome
2. Tap menu (⋮)
3. "Add to Home screen"
4. Or use the in-app install prompt

### 4. Verify PWA Features

**Chrome DevTools:**
1. Press F12
2. Go to Application tab
3. Check:
   - Manifest: Should show all metadata
   - Service Workers: Should be registered
   - Cache Storage: Should show cached assets
   - Offline: Toggle offline mode and reload

**Lighthouse Audit:**
1. F12 > Lighthouse tab
2. Select "Progressive Web App"
3. Click "Generate report"
4. Aim for 100% PWA score

---

## 🎨 Customization

### Update App Metadata

Edit `/wwwroot/manifest.json`:

```json
{
  "name": "Your Custom Name",
  "short_name": "CustomName",
  "description": "Your description",
  "theme_color": "#yourcolor",
  "background_color": "#yourcolor"
}
```

### Modify Caching Strategy

Edit `/wwwroot/service-worker.js`:

```javascript
// Adjust what gets cached on install
const STATIC_ASSETS = [
  '/',
  '/your-important-page'
];

// Add custom route caching rules
const ROUTE_CONFIG = [
  { pattern: /\/your-api\//, strategy: CACHE_STRATEGIES.NETWORK_FIRST }
];
```

### Customize Install Prompt

Edit `Components/Shared/PwaInstallPrompt.razor` to change:
- Prompt text
- Button labels
- Styling
- Display timing

---

## 📱 Platform-Specific Features

### iOS Enhancements

Already configured:
- `apple-mobile-web-app-capable` - Runs in standalone mode
- `apple-mobile-web-app-status-bar-style` - Status bar appearance
- `apple-touch-icon` - Home screen icon

Add more in `App.razor`:
```html
<meta name="apple-mobile-web-app-title" content="MoneyBrain">
<link rel="apple-touch-startup-image" href="/splash-screen.png">
```

### Android Enhancements

Enhance `manifest.json`:
```json
{
  "display_override": ["window-controls-overlay", "standalone"],
  "orientation": "portrait",
  "categories": ["finance", "productivity"]
}
```

### Desktop (Windows/macOS/Linux)

Features automatically enabled:
- Window controls overlay
- File handling (if configured)
- Protocol handlers (if configured)
- Shortcuts (already configured)

---

## 🔧 Advanced Features

### 1. Background Sync

The service worker supports background sync for offline transactions:

```javascript
// In your transaction service
navigator.serviceWorker.ready.then(registration => {
  return registration.sync.register('sync-transactions');
});
```

### 2. Push Notifications

Enable push notifications (requires backend):

```javascript
// Request permission
Notification.requestPermission().then(permission => {
  if (permission === 'granted') {
    // Subscribe to push
  }
});
```

### 3. App Badge

Show unread count badge:

```javascript
// Set badge
window.moneybrainPwa.setBadge(5);

// Clear badge
window.moneybrainPwa.clearBadge();
```

### 4. Share API

Share content from the app:

```javascript
window.moneybrainPwa.share(
  'MoneyBrain Report',
  'Check out my budget report',
  'https://yourapp.com/report'
);
```

### 5. Shortcuts

Already configured in manifest. Users can right-click app icon to:
- Open Dashboard
- Add Transaction
- View Budgets

---

## 🧪 Testing Checklist

### Installation
- [ ] Desktop Chrome/Edge install works
- [ ] iOS Safari "Add to Home Screen" works
- [ ] Android Chrome install works
- [ ] Install prompt appears after 3 seconds
- [ ] "Not Now" dismisses for 7 days
- [ ] Manual install via browser menu works

### Offline Functionality
- [ ] App loads when offline
- [ ] Offline page shows when navigating while offline
- [ ] Cached pages work offline
- [ ] Online/offline status updates appear
- [ ] App reconnects automatically when online

### Caching
- [ ] Static assets cached on first load
- [ ] Framework files (Blazor) cached
- [ ] API responses cached appropriately
- [ ] Cache updates on new version
- [ ] Old caches cleaned up on activation

### Updates
- [ ] Service worker updates on deployment
- [ ] Update notification appears
- [ ] Reload applies new version
- [ ] No breaking changes during update

### UI/UX
- [ ] App runs in standalone mode (no browser UI)
- [ ] Theme color applies to system UI
- [ ] Status bar styled correctly
- [ ] Navigation works in standalone mode
- [ ] Install prompt is not intrusive

### Performance
- [ ] Lighthouse PWA score > 90
- [ ] Fast load times (< 3s on 3G)
- [ ] Smooth animations
- [ ] No layout shift

---

## 🐛 Troubleshooting

### Install Button Not Showing

**Possible causes:**
- Already installed
- Not served over HTTPS (required)
- Service worker not registered
- Manifest invalid

**Solutions:**
1. Check DevTools > Application > Manifest
2. Uninstall app and try again
3. Clear cache and service workers
4. Validate manifest at https://manifest-validator.appspot.com/

### Service Worker Not Updating

**Solution:**
```javascript
// Force update
navigator.serviceWorker.getRegistrations().then(registrations => {
  registrations.forEach(registration => {
    registration.unregister();
  });
  window.location.reload();
});
```

### Offline Page Not Showing

**Check:**
1. Is `/offline.html` in static assets cache?
2. Service worker properly catching failed requests?
3. Browser cache cleared?

### iOS Not Installing

**Requirements:**
- Must use Safari (not Chrome)
- Must manually use "Add to Home Screen"
- No automatic install prompt on iOS

### Cache Growing Too Large

**Monitor cache size:**
```javascript
// Get current cache usage
window.moneybrainPwa.getCacheSize().then(console.log);
```

**Clear cache:**
```javascript
window.moneybrainPwa.clearCache();
```

---

## 📊 Monitoring

### Service Worker Status

Check in DevTools > Application > Service Workers:
- Status: Should be "activated and running"
- Update on reload: Useful for development
- Bypass for network: Debug caching issues

### Cache Inspection

DevTools > Application > Cache Storage:
- `moneybrain-v1`: Static assets
- `moneybrain-runtime-v1`: Runtime caches
- `moneybrain-data-v1`: API responses

### Analytics

Track PWA usage:
- Installation events
- Standalone mode usage
- Offline usage
- Update acceptance rate

---

## 🚢 Deployment

### Production Checklist

Before deploying:
- [ ] All icons generated and optimized
- [ ] Screenshots captured and added
- [ ] Manifest tested and valid
- [ ] Service worker tested offline
- [ ] HTTPS enabled (required)
- [ ] Cache version updated
- [ ] Performance tested on real devices

### HTTPS Setup

PWAs **require** HTTPS in production:

**Options:**
1. Use reverse proxy (nginx, Caddy) with Let's Encrypt
2. Cloud hosting with built-in SSL (Azure, AWS, etc.)
3. Cloudflare (free SSL)

**Example nginx config:**
```nginx
server {
    listen 443 ssl http2;
    server_name moneybrain.yourdomain.com;
    
    ssl_certificate /path/to/cert.pem;
    ssl_certificate_key /path/to/key.pem;
    
    location / {
        proxy_pass http://localhost:5103;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
```

### Version Updates

When deploying a new version:

1. Update cache version in `service-worker.js`:
   ```javascript
   const CACHE_VERSION = 'moneybrain-v2'; // Increment
   ```

2. Service worker will:
   - Auto-detect new version
   - Prompt users to update
   - Clean old caches

3. No manual cache clearing needed

---

## 📚 Resources

### Tools
- [PWA Builder](https://www.pwabuilder.com/) - Test and package PWA
- [Lighthouse](https://developers.google.com/web/tools/lighthouse) - PWA audit
- [Workbox](https://developers.google.com/web/tools/workbox) - Service worker library (reference)
- [Maskable.app](https://maskable.app/) - Icon editor

### Documentation
- [MDN PWA Guide](https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps)
- [Web.dev PWA](https://web.dev/progressive-web-apps/)
- [Apple PWA Guide](https://developer.apple.com/library/archive/documentation/AppleApplications/Reference/SafariWebContent/ConfiguringWebApplications/ConfiguringWebApplications.html)

### Validation
- [Manifest Validator](https://manifest-validator.appspot.com/)
- [PWA Checklist](https://web.dev/pwa-checklist/)

---

## 🎉 What's Next

Your PWA is ready! Next steps:

1. **Generate icons** - See `/wwwroot/icons/README.md`
2. **Capture screenshots** - See `/wwwroot/screenshots/README.md`
3. **Test installation** - Try on multiple devices
4. **Submit to stores** - Microsoft Store, Play Store (via TWA)
5. **Monitor usage** - Track installs and engagement

**Need help?** Check the troubleshooting section or review the PWA resources above.

---

*MoneyBrain PWA - Built with ❤️ for offline-first personal finance*
