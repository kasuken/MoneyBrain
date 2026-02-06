// MoneyBrain PWA JavaScript Integration
window.moneybrainPwa = {
    deferredPrompt: null,
    dotNetRef: null,
    isInstalled: false,

    initialize: function (dotNetReference) {
        this.dotNetRef = dotNetReference;
        this.checkInstallation();
        this.registerServiceWorker();
        this.setupBeforeInstallPrompt();
        this.setupMobileInstallPrompt();
        this.setupAppInstalled();
        this.setupOnlineOfflineHandlers();
        this.checkForUpdates();
    },

    checkInstallation: function () {
        // Check if app is in standalone mode (already installed)
        if (window.matchMedia('(display-mode: standalone)').matches ||
            window.navigator.standalone === true) {
            this.isInstalled = true;
            console.log('[PWA] App is running in standalone mode');
        }
    },

    detectMobileDevice: function () {
        const userAgent = navigator.userAgent || navigator.vendor || window.opera;
        const isIOS = /iPad|iPhone|iPod/.test(userAgent) && !window.MSStream;
        const isAndroid = /Android/.test(userAgent);
        const isMobile = isIOS || isAndroid || /Mobile|webOS|BlackBerry|IEMobile|Opera Mini/i.test(userAgent);
        
        return {
            isIOS: isIOS,
            isAndroid: isAndroid,
            isMobile: isMobile
        };
    },

    setupMobileInstallPrompt: function () {
        const device = this.detectMobileDevice();
        
        // iOS doesn't support beforeinstallprompt, show instructions proactively
        if (device.isIOS && !this.isInstalled) {
            console.log('[PWA] iOS detected - will show native install instructions');
            
            // Check if user has previously dismissed the prompt
            const dismissedDate = localStorage.getItem('pwa-install-dismissed');
            if (dismissedDate) {
                const daysSinceDismissed = (Date.now() - parseInt(dismissedDate)) / (1000 * 60 * 60 * 24);
                if (daysSinceDismissed < 7) {
                    return; // Don't show for 7 days after dismissal
                }
            }
            
            setTimeout(() => {
                if (this.dotNetRef && !this.isInstalled) {
                    this.dotNetRef.invokeMethodAsync('ShowIosInstallPrompt');
                }
            }, 5000); // 5 seconds delay for iOS
        } else if (device.isAndroid && !this.isInstalled) {
            // Android - use shorter delay, beforeinstallprompt will override if available
            console.log('[PWA] Android detected - using mobile-optimized timing');
        } else if (device.isMobile && !this.isInstalled) {
            // Other mobile devices
            console.log('[PWA] Other mobile device detected');
        }
    },

    registerServiceWorker: function () {
        if ('serviceWorker' in navigator) {
            navigator.serviceWorker.register('/service-worker.js', { scope: '/' })
                .then((registration) => {
                    console.log('[PWA] Service Worker registered:', registration.scope);

                    // Check for updates every hour
                    setInterval(() => {
                        registration.update();
                    }, 60 * 60 * 1000);

                    // Handle service worker updates
                    registration.addEventListener('updatefound', () => {
                        const newWorker = registration.installing;
                        newWorker.addEventListener('statechange', () => {
                            if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                                console.log('[PWA] New version available');
                                this.showUpdateNotification();
                            }
                        });
                    });
                })
                .catch((error) => {
                    console.error('[PWA] Service Worker registration failed:', error);
                });
        }
    },

    setupBeforeInstallPrompt: function () {
        window.addEventListener('beforeinstallprompt', (e) => {
            console.log('[PWA] beforeinstallprompt event fired');
            e.preventDefault();
            this.deferredPrompt = e;

            // Check if user has previously dismissed the prompt
            const dismissedDate = localStorage.getItem('pwa-install-dismissed');
            if (dismissedDate) {
                const daysSinceDismissed = (Date.now() - parseInt(dismissedDate)) / (1000 * 60 * 60 * 24);
                if (daysSinceDismissed < 7) {
                    return; // Don't show for 7 days after dismissal
                }
            }

            // Show the install prompt with mobile-optimized timing
            const device = this.detectMobileDevice();
            const delay = device.isMobile ? 5000 : 3000; // 5s for mobile, 3s for desktop
            
            setTimeout(() => {
                if (this.dotNetRef && !this.isInstalled) {
                    this.dotNetRef.invokeMethodAsync('ShowInstallPrompt');
                }
            }, delay);
        });
    },

    setupAppInstalled: function () {
        window.addEventListener('appinstalled', () => {
            console.log('[PWA] App installed successfully');
            this.isInstalled = true;
            this.deferredPrompt = null;
            
            // Track installation
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('HideInstallPrompt');
            }

            // Show success message
            this.showToast('MoneyBrain has been installed!', 'success');
        });
    },

    install: async function () {
        if (!this.deferredPrompt) {
            console.log('[PWA] No deferred prompt available');
            this.showNativeInstallInstructions();
            return;
        }

        try {
            this.deferredPrompt.prompt();
            const { outcome } = await this.deferredPrompt.userChoice;
            console.log(`[PWA] User response to install prompt: ${outcome}`);
            
            if (outcome === 'accepted') {
                this.isInstalled = true;
            }
            
            this.deferredPrompt = null;
        } catch (error) {
            console.error('[PWA] Install prompt error:', error);
        }
    },

    dismiss: function () {
        localStorage.setItem('pwa-install-dismissed', Date.now().toString());
        console.log('[PWA] Install prompt dismissed');
    },

    showNativeInstallInstructions: function () {
        const isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent) && !window.MSStream;
        const isAndroid = /Android/.test(navigator.userAgent);

        let message = 'To install MoneyBrain:\n\n';

        if (isIOS) {
            message += '1. Tap the Share button in Safari\n';
            message += '2. Select "Add to Home Screen"\n';
            message += '3. Tap "Add" to confirm';
        } else if (isAndroid) {
            message += '1. Tap the menu button in your browser\n';
            message += '2. Select "Add to Home screen"\n';
            message += '3. Follow the prompts';
        } else {
            message += '1. Click the install icon in your browser\'s address bar\n';
            message += '2. Or use your browser\'s menu and select "Install MoneyBrain"';
        }

        alert(message);
    },

    setupOnlineOfflineHandlers: function () {
        window.addEventListener('online', () => {
            console.log('[PWA] Connection restored');
            this.showToast('You are back online', 'info');
        });

        window.addEventListener('offline', () => {
            console.log('[PWA] Connection lost');
            this.showToast('You are offline - changes will sync when reconnected', 'warning');
        });
    },

    checkForUpdates: function () {
        if ('serviceWorker' in navigator) {
            navigator.serviceWorker.ready.then((registration) => {
                registration.update();
            });
        }
    },

    showUpdateNotification: function () {
        if (confirm('A new version of MoneyBrain is available. Reload to update?')) {
            if ('serviceWorker' in navigator) {
                navigator.serviceWorker.getRegistration().then((registration) => {
                    if (registration && registration.waiting) {
                        registration.waiting.postMessage({ type: 'SKIP_WAITING' });
                        window.location.reload();
                    }
                });
            }
        }
    },

    showToast: function (message, type = 'info') {
        // This would integrate with MudBlazor's Snackbar
        // For now, using console
        console.log(`[PWA Toast - ${type}] ${message}`);
    },

    // Cache management
    clearCache: async function () {
        if ('serviceWorker' in navigator) {
            const registration = await navigator.serviceWorker.getRegistration();
            if (registration && registration.active) {
                registration.active.postMessage({ type: 'CLEAR_CACHE' });
            }
        }
        console.log('[PWA] Cache clear requested');
    },

    // Get cache size
    getCacheSize: async function () {
        if ('storage' in navigator && 'estimate' in navigator.storage) {
            const estimate = await navigator.storage.estimate();
            const usage = estimate.usage || 0;
            const quota = estimate.quota || 0;
            const percentUsed = (usage / quota * 100).toFixed(2);
            
            return {
                usage: this.formatBytes(usage),
                quota: this.formatBytes(quota),
                percentUsed: percentUsed
            };
        }
        return null;
    },

    formatBytes: function (bytes) {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const sizes = ['Bytes', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
    },

    // Share API support
    share: async function (title, text, url) {
        if (navigator.share) {
            try {
                await navigator.share({ title, text, url });
                console.log('[PWA] Shared successfully');
                return true;
            } catch (error) {
                console.log('[PWA] Share cancelled or failed:', error);
                return false;
            }
        } else {
            console.log('[PWA] Web Share API not supported');
            return false;
        }
    },

    // Add to home screen badge (for supported browsers)
    setBadge: function (count) {
        if ('setAppBadge' in navigator) {
            navigator.setAppBadge(count)
                .then(() => console.log('[PWA] Badge set to:', count))
                .catch((error) => console.error('[PWA] Badge error:', error));
        }
    },

    clearBadge: function () {
        if ('clearAppBadge' in navigator) {
            navigator.clearAppBadge()
                .then(() => console.log('[PWA] Badge cleared'))
                .catch((error) => console.error('[PWA] Badge clear error:', error));
        }
    }
};

// Initialize on load
console.log('[PWA] MoneyBrain PWA script loaded');
