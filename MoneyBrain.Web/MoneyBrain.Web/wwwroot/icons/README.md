# PWA Icons# PWA Icons Directory




















































- https://manifest-validator.appspot.com/- https://www.pwabuilder.com/- Chrome DevTools > Application > ManifestTest your icons using:## Validation3. Replace with proper branding when ready2. Use an icon generator to create all sizes1. Create a basic square image with "MB" textFor development/testing, you can use a simple placeholder:## Temporary Placeholder- **Maskable.app** - https://maskable.app/editor (for maskable icons)- **RealFaviconGenerator** - https://realfavicongenerator.net/- **PWA Asset Generator** - `npx @pwa/asset-generator logo.svg icons/`- **Figma/Adobe XD** - Design and exportYou can use tools like:## Quick Icon Generation- Maintain consistent branding- Use the MoneyBrain "M" logo or brain icon- Have a transparent background OR solid color backgroundOther icons should:### Standard Icons- Center the logo/icon- Use a solid background color (#6366f1 - MoneyBrain primary color)- Have the important content in the "safe zone" (80% of the canvas)Icons marked as "maskable" (192x192 and 512x512) should:### Maskable Icons## Design Guidelines- `icon-512x512.png` - 512x512 pixels (must be maskable)- `icon-384x384.png` - 384x384 pixels- `icon-192x192.png` - 192x192 pixels (must be maskable)- `icon-152x152.png` - 152x152 pixels- `icon-144x144.png` - 144x144 pixels- `icon-128x128.png` - 128x128 pixels- `icon-96x96.png` - 96x96 pixels- `icon-72x72.png` - 72x72 pixelsCreate PNG icons with the MoneyBrain logo/brand in these sizes:## Required IconsThis directory should contain the MoneyBrain PWA icons in the following sizes:
This directory contains the Progressive Web App icons for MoneyBrain.

## Required Icons

The following icon sizes are required for a complete PWA implementation:

- 72x72 pixels
- 96x96 pixels
- 128x128 pixels
- 144x144 pixels
- 152x152 pixels
- 192x192 pixels (required for Android)
- 384x384 pixels
- 512x512 pixels (required for splash screens)

## Generating Icons

### Option 1: Use an Online Tool

1. **PWA Asset Generator** (recommended)
   - Visit: https://www.pwabuilder.com/imageGenerator
   - Upload a 512x512 PNG logo
   - Download all generated icons
   - Place them in this directory

2. **RealFaviconGenerator**
   - Visit: https://realfavicongenerator.net/
   - Upload your logo
   - Configure settings for different platforms
   - Download and extract icons

### Option 2: Manual Generation

If you have a high-resolution logo (SVG or 1024x1024 PNG):

**Using ImageMagick:**
```bash
# Install ImageMagick first
magick convert logo.png -resize 72x72 icon-72x72.png
magick convert logo.png -resize 96x96 icon-96x96.png
magick convert logo.png -resize 128x128 icon-128x128.png
magick convert logo.png -resize 144x144 icon-144x144.png
magick convert logo.png -resize 152x152 icon-152x152.png
magick convert logo.png -resize 192x192 icon-192x192.png
magick convert logo.png -resize 384x384 icon-384x384.png
magick convert logo.png -resize 512x512 icon-512x512.png
```

**Using Online Batch Resizer:**
- https://bulkresizephotos.com/

### Option 3: Design Tool Export

If using design tools like Figma, Sketch, or Adobe XD:
- Create artboards for each size
- Export each as PNG with 2x resolution
- Name files according to the pattern: `icon-[size].png`

## Icon Design Guidelines

### Maskable Icons
For Android adaptive icons, your 192x192 and 512x512 icons should be "maskable":
- Keep important content in the center 80% of the image
- Use a solid background color
- Avoid transparency in the safe zone

### Apple Touch Icons
The 192x192 icon serves as the Apple Touch Icon:
- iOS adds rounded corners automatically
- Don't pre-round corners
- Avoid text smaller than 40x40 pixels

### Splash Screen
The 512x512 icon is used for splash screens:
- Keep it simple and recognizable
- Works well on both light and dark backgrounds
- Consider a square or circular design

## Current Status

⚠️ **Placeholder icons needed** - The current icons are SVG placeholders.
Replace them with proper PNG icons following the guidelines above.

## Color Scheme

MoneyBrain's brand colors:
- Primary: #6366f1 (Indigo)
- Background: #0f172a (Dark Slate)
- Success: #1DB954 (Green)

Make sure your icons use these colors for consistency.

## Testing Your Icons

After adding icons:
1. Clear browser cache
2. Uninstall any existing PWA installation
3. Reload the app
4. Check browser DevTools > Application > Manifest
5. Verify all icons load correctly
6. Test installation on mobile device

## Screenshots

Place app screenshots in `/wwwroot/screenshots/`:
- `dashboard-desktop.png` - 1280x720 (wide format)
- `dashboard-mobile.png` - 750x1334 (narrow format)

These appear in the install dialog on supported browsers.
