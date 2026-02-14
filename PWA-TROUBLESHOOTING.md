# PWA Installation Troubleshooting Guide

## Issue: App Icon Not Showing or Wrong Start Page

If you installed MoneyBrain as a PWA (Progressive Web App) and are experiencing issues with:
- The app icon not showing on your home screen
- The app opening to `/dashboard` instead of the homepage

This guide will help you fix these issues.

### Why This Happens

These issues can occur if:
1. You installed the PWA while on the `/dashboard` page instead of the homepage
2. Your browser cached an old version of the PWA manifest
3. Missing icon files in an earlier version of the app

### Solution: Reinstall the PWA

Follow these steps to fix the issue:

#### Step 1: Remove Current PWA Installation

**Android (Chrome/Edge):**
1. Long-press the MoneyBrain app icon on your home screen
2. Select "Remove" or "Uninstall"
3. Confirm removal

**iOS (Safari):**
1. Long-press the MoneyBrain app icon on your home screen
2. Tap the "X" or "Remove App"
3. Confirm removal

**Desktop (Chrome/Edge):**
1. Open Chrome/Edge
2. Go to Settings → Apps → Installed Apps
3. Find MoneyBrain and click "Uninstall"

#### Step 2: Clear Browser Cache

**Mobile:**
1. Open your browser (Chrome, Safari, Edge)
2. Go to Settings → Privacy → Clear Browsing Data
3. Select "Cached images and files"
4. Clear the cache

**Desktop:**
1. Press `Ctrl+Shift+Delete` (Windows/Linux) or `Cmd+Shift+Delete` (Mac)
2. Select "Cached images and files"
3. Clear the cache

#### Step 3: Reinstall the PWA from Homepage

**Important:** Make sure you're on the **homepage** (`/`) before installing!

**Android (Chrome/Edge):**
1. Visit your MoneyBrain site homepage (`https://your-domain.com/`)
2. Tap the menu (three dots) → "Install app" or "Add to Home screen"
3. Confirm installation

**iOS (Safari):**
1. Visit your MoneyBrain site homepage (`https://your-domain.com/`)
2. Tap the Share button (square with arrow)
3. Scroll down and tap "Add to Home Screen"
4. Tap "Add"

**Desktop (Chrome/Edge):**
1. Visit your MoneyBrain site homepage (`https://your-domain.com/`)
2. Look for the install icon in the address bar (computer with arrow)
3. Click it and confirm installation

### Verify the Fix

After reinstalling:
1. ✅ The app icon should now be visible on your home screen
2. ✅ Launching the app should open to the homepage (not `/dashboard`)
3. ✅ The app should run in standalone mode (no browser UI)

### Still Having Issues?

If you're still experiencing problems after following these steps:
1. Check that you're running the latest version of your browser
2. Ensure JavaScript is enabled in your browser
3. Try using a different browser (Chrome, Edge, or Safari)
4. Clear your browser cache again and wait a few minutes before reinstalling

### Technical Details

The latest version includes:
- All required PWA icons (72x72, 96x96, 128x128, 144x144, 152x152, 192x192, 512x512)
- Correct `start_url` configuration pointing to `/`
- Updated service worker to force cache refresh
- Proper manifest configuration for all platforms

---

**Last Updated:** February 2026  
**Fixed in Version:** Service Worker v2 (icon-fix)
