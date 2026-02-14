// MoneyBrain PWA JavaScript Integration - Installation Only
window.moneybrainPwa = {
    deferredPrompt: null,
    dotNetRef: null,
    isInstalled: false,

    initialize: function (dotNetReference) {
        this.dotNetRef = dotNetReference;
        
        // Check installation status
        this.checkInstallation();
        
        // Only set up install prompt handlers (no service worker or caching)
        this.setupBeforeInstallPrompt();
        this.setupMobileInstallPrompt();
        this.setupAppInstalled();
        
        console.log('[PWA] Install prompt initialized');
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
        
        // Only show on mobile devices
        if (!device.isMobile || this.isInstalled) {
            return;
        }
        
        // iOS doesn't support beforeinstallprompt, show instructions proactively
        if (device.isIOS) {
            console.log('[PWA] iOS detected - will show native install instructions');
            
            // Check if user has previously dismissed the prompt
            const dismissedDate = localStorage.getItem('pwa-install-dismissed');
            if (dismissedDate) {
                const daysSinceDismissed = (Date.now() - parseInt(dismissedDate)) / (1000 * 60 * 60 * 24);
                if (daysSinceDismissed < 7) {
                    return; // Don't show for 7 days after dismissal
                }
            }
            
            // Show prompt after a short delay to ensure Blazor is ready
            setTimeout(() => {
                if (this.dotNetRef && !this.isInstalled) {
                    this.dotNetRef.invokeMethodAsync('ShowIosInstallPrompt')
                        .catch(error => console.error('[PWA] iOS prompt error:', error));
                }
            }, 2000);
        }
    },

    setupBeforeInstallPrompt: function () {
        const device = this.detectMobileDevice();
        
        // Only show on mobile devices
        if (!device.isMobile) {
            return;
        }
        
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

            // Show install prompt after a delay
            setTimeout(() => {
                if (this.dotNetRef && !this.isInstalled) {
                    this.dotNetRef.invokeMethodAsync('ShowInstallPrompt')
                        .catch(error => console.error('[PWA] Install prompt error:', error));
                }
            }, 3000);
        });
    },

    setupAppInstalled: function () {
        window.addEventListener('appinstalled', () => {
            console.log('[PWA] App installed successfully');
            this.isInstalled = true;
            this.deferredPrompt = null;
            
            // Hide the prompt
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('HideInstallPrompt')
                    .catch(error => console.error('[PWA] Hide prompt error:', error));
            }
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
        const device = this.detectMobileDevice();

        let message = 'To install MoneyBrain:\n\n';

        if (device.isIOS) {
            message += '1. Tap the Share button in Safari\n';
            message += '2. Select "Add to Home Screen"\n';
            message += '3. Tap "Add" to confirm';
        } else if (device.isAndroid) {
            message += '1. Tap the menu button in your browser\n';
            message += '2. Select "Add to Home screen"\n';
            message += '3. Follow the prompts';
        } else {
            message += '1. Click the install icon in your browser\'s address bar\n';
            message += '2. Or use your browser\'s menu and select "Install MoneyBrain"';
        }

        alert(message);
    },
    
    dispose: function() {
        this.dotNetRef = null;
        this.deferredPrompt = null;
        console.log('[PWA] Disposed');
    }
};

console.log('[PWA] MoneyBrain PWA install script loaded');
