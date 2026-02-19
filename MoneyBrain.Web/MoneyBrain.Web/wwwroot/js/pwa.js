// MoneyBrain PWA JavaScript Integration - Installation Only
window.moneybrainPwa = {
    deferredPrompt: null,
    dotNetRef: null,
    isInstalled: false,

    initialize: function (dotNetReference) {
        console.log('[PWA] 🚀 Initializing PWA install prompt system');
        this.dotNetRef = dotNetReference;
        console.log('[PWA] ✅ .NET reference registered');
        
        // Check installation status
        this.checkInstallation();
        
        // Only set up install prompt handlers (no service worker or caching)
        this.setupBeforeInstallPrompt();
        this.setupMobileInstallPrompt();
        this.setupAppInstalled();
        
        console.log('[PWA] ✅ Install prompt initialized');
        console.log('[PWA] 💡 Manual commands available:');
        console.log('[PWA]    - window.moneybrainPwa.forceShowPrompt()  // Force show popup');
        console.log('[PWA]    - window.moneybrainPwa.clearDismissal()   // Clear 7-day block');
        console.log('[PWA]    - window.moneybrainPwa.getDebugInfo()     // Show debug info');
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
        
        console.log('[PWA] setupMobileInstallPrompt called');
        console.log('[PWA] Device detection:', {
            isMobile: device.isMobile,
            isIOS: device.isIOS,
            isAndroid: device.isAndroid,
            isInstalled: this.isInstalled,
            userAgent: navigator.userAgent
        });
        
        // Only show on mobile devices
        if (!device.isMobile) {
            console.log('[PWA] Not a mobile device - popup will not show');
            return;
        }
        
        if (this.isInstalled) {
            console.log('[PWA] App already installed - popup will not show');
            return;
        }
        
        // iOS doesn't support beforeinstallprompt, show instructions proactively
        if (device.isIOS) {
            console.log('[PWA] iOS detected - will show native install instructions');
            
            // Check if user has previously dismissed the prompt
            const dismissedDate = localStorage.getItem('pwa-install-dismissed');
            if (dismissedDate) {
                const daysSinceDismissed = (Date.now() - parseInt(dismissedDate)) / (1000 * 60 * 60 * 24);
                console.log('[PWA] Dismissal check:', {
                    dismissedDate: new Date(parseInt(dismissedDate)).toLocaleString(),
                    daysSinceDismissed: Math.floor(daysSinceDismissed),
                    willShow: daysSinceDismissed >= 7
                });
                
                if (daysSinceDismissed < 7) {
                    console.log('[PWA] Popup dismissed recently - will show again in', Math.ceil(7 - daysSinceDismissed), 'days');
                    return; // Don't show for 7 days after dismissal
                }
            } else {
                console.log('[PWA] No dismissal recorded - popup can show');
            }
            
            // Show prompt after a short delay to ensure Blazor is ready
            console.log('[PWA] Setting 2 second timeout to show iOS prompt...');
            setTimeout(() => {
                console.log('[PWA] Timeout triggered - checking conditions...');
                console.log('[PWA] dotNetRef exists:', !!this.dotNetRef);
                console.log('[PWA] isInstalled:', this.isInstalled);
                
                if (this.dotNetRef && !this.isInstalled) {
                    console.log('[PWA] ✅ Calling ShowIosInstallPrompt via .NET interop');
                    this.dotNetRef.invokeMethodAsync('ShowIosInstallPrompt')
                        .then(() => console.log('[PWA] ✅ ShowIosInstallPrompt called successfully'))
                        .catch(error => console.error('[PWA] ❌ iOS prompt error:', error));
                } else {
                    console.log('[PWA] ❌ Cannot show prompt - dotNetRef:', !!this.dotNetRef, 'isInstalled:', this.isInstalled);
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
    },
    
    // Manual trigger for testing - call from console: window.moneybrainPwa.forceShowPrompt()
    forceShowPrompt: function() {
        console.log('[PWA] 🔧 Manual trigger called');
        const device = this.detectMobileDevice();
        
        if (!this.dotNetRef) {
            console.error('[PWA] ❌ dotNetRef not initialized - component may not be loaded yet');
            return;
        }
        
        if (device.isIOS) {
            console.log('[PWA] 🔧 Manually showing iOS prompt');
            this.dotNetRef.invokeMethodAsync('ShowIosInstallPrompt')
                .then(() => console.log('[PWA] ✅ iOS prompt shown'))
                .catch(error => console.error('[PWA] ❌ Error:', error));
        } else if (device.isAndroid) {
            console.log('[PWA] 🔧 Manually showing Android prompt');
            this.dotNetRef.invokeMethodAsync('ShowInstallPrompt')
                .then(() => console.log('[PWA] ✅ Android prompt shown'))
                .catch(error => console.error('[PWA] ❌ Error:', error));
        } else {
            console.log('[PWA] 🔧 Not a mobile device - showing install instructions anyway');
            alert('PWA Install Test Mode\n\nDevice detection:\n- iOS: ' + device.isIOS + '\n- Android: ' + device.isAndroid + '\n- Mobile: ' + device.isMobile);
        }
    },
    
    // Clear dismissal flag - call from console: window.moneybrainPwa.clearDismissal()
    clearDismissal: function() {
        localStorage.removeItem('pwa-install-dismissed');
        console.log('[PWA] 🔧 Dismissal flag cleared - popup will show on next page load');
    },
    
    // Get debug info - call from console: window.moneybrainPwa.getDebugInfo()
    getDebugInfo: function() {
        const device = this.detectMobileDevice();
        const dismissedDate = localStorage.getItem('pwa-install-dismissed');
        const daysSince = dismissedDate ? (Date.now() - parseInt(dismissedDate)) / (1000 * 60 * 60 * 24) : null;
        
        const info = {
            isInstalled: this.isInstalled,
            dotNetRefExists: !!this.dotNetRef,
            deferredPromptExists: !!this.deferredPrompt,
            device: device,
            userAgent: navigator.userAgent,
            dismissal: dismissedDate ? {
                date: new Date(parseInt(dismissedDate)).toLocaleString(),
                daysAgo: Math.floor(daysSince),
                canShow: daysSince >= 7
            } : 'Not dismissed',
            standalone: window.matchMedia('(display-mode: standalone)').matches
        };
        
        console.log('[PWA] 🔍 Debug Info:', info);
        return info;
    }
};

console.log('[PWA] MoneyBrain PWA install script loaded');
