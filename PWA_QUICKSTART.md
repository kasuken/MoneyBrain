# MoneyBrain PWA - Quick Start

This guide helps you get MoneyBrain's PWA features up and running in 5 minutes.

## ⚡ Quick Setup

### 1. Add Icons (2 minutes)

**Option A: Generate from logo**
```bash
npx @pwa/asset-generator logo.svg wwwroot/icons/ --icon-only
```

**Option B: Use placeholder**
Download placeholder icons from [RealFaviconGenerator](https://realfavicongenerator.net/)

Place these files in `wwwroot/icons/`:
- icon-72x72.png
- icon-96x96.png
- icon-128x128.png
- icon-144x144.png
- icon-152x152.png
- icon-192x192.png ⭐ (required)
- icon-384x384.png
- icon-512x512.png ⭐ (required)

### 2. Test It (1 minute)

```bash
dotnet run
```

Open https://localhost:7123 in Chrome/Edge (must be HTTPS!)

Look for the install icon in the address bar ⬇️

### 3. Verify (2 minutes)

Press F12 → Application tab:
- ✅ Manifest: Should show MoneyBrain info
- ✅ Service Workers: Should show registered
- ✅ Cache Storage: Should have cached files

## 🧪 Test Offline

1. Install the app (click install icon or browser menu)
2. In DevTools: Application → Service Workers → ✅ Offline
3. Reload - app should still work!
4. Navigate - cached pages load instantly

## 🎯 What You Get

✅ **Instant loading** - Cached assets load in milliseconds  
✅ **Offline mode** - Works without internet  
✅ **Install prompt** - Shows after 3 seconds  
✅ **Auto-updates** - New versions install seamlessly  
✅ **App shortcuts** - Dashboard, Transactions, Budgets  
✅ **Smart caching** - API responses cached intelligently  

## 📝 Optional: Add Screenshots

For install dialog previews:

1. Open Dashboard in browser
2. Take screenshot: 1280x720 (desktop) or 750x1334 (mobile)
3. Save to `wwwroot/screenshots/dashboard-desktop.png`

## 🐛 Troubleshooting

**Install button not showing?**
- Check you're on HTTPS (required for PWA)
- Clear cache: DevTools → Application → Clear storage
- Uninstall any existing installation

**Service worker not registering?**
- Check browser console for errors
- Verify `/service-worker.js` is accessible
- Check `pwa.js` loaded successfully

**Offline mode not working?**
- DevTools → Network → Disable cache (should be OFF)
- Service worker must be active
- Try hard refresh: Ctrl+Shift+R

## 📚 Full Guide

See [PWA_IMPLEMENTATION.md](PWA_IMPLEMENTATION.md) for:
- Detailed configuration
- Advanced features (push notifications, background sync)
- Platform-specific optimizations
- Production deployment checklist
- Complete troubleshooting guide

## 🎉 You're Done!

Your PWA is ready! Users can now:
- Install MoneyBrain on their device
- Use it offline
- Get fast, app-like performance
- Receive automatic updates

**Next:** Share your PWA with users and start tracking installations!
