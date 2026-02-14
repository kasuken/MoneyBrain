// MoneyBrain Mobile Detection & Utilities
window.moneybrainMobile = {
    // Mobile breakpoint (matches Bootstrap/MudBlazor md breakpoint)
    MOBILE_BREAKPOINT: 768,
    
    // Store reference for cleanup
    _dotNetReference: null,
    _resizeHandler: null,
    _resizeTimeout: null,
    _initialized: false,

    // Check if device is mobile based on user agent
    isMobileDevice: function() {
        var userAgent = navigator.userAgent || navigator.vendor || window.opera;
        
        // Check for iOS devices (iPhone, iPad, iPod)
        if (/iPad|iPhone|iPod/.test(userAgent) && !window.MSStream) {
            return true;
        }
        
        // Check for Android devices
        if (/Android/.test(userAgent)) {
            return true;
        }
        
        // Check for other mobile devices
        if (/Mobile|webOS|BlackBerry|IEMobile|Opera Mini/i.test(userAgent)) {
            return true;
        }
        
        // Check for touch capability as additional signal
        if ('ontouchstart' in window || navigator.maxTouchPoints > 0) {
            // Only consider it mobile if also has small screen
            if (window.innerWidth < this.MOBILE_BREAKPOINT) {
                return true;
            }
        }
        
        return false;
    },

    // Check if current viewport is mobile sized
    isMobileViewport: function() {
        return window.innerWidth < this.MOBILE_BREAKPOINT;
    },
    
    // Combined check - true if device is mobile OR viewport is mobile-sized
    isMobile: function() {
        var isDevice = this.isMobileDevice();
        var isViewport = this.isMobileViewport();
        console.log('[Mobile] Check - isDevice:', isDevice, 'isViewport:', isViewport, 'width:', window.innerWidth);
        return isDevice || isViewport;
    },

    // Initialize mobile detection with callback to Blazor
    initialize: function(dotNetReference) {
        console.log('[Mobile] Initializing...');
        
        // Clean up any previous instance first
        this.dispose();
        
        this._dotNetReference = dotNetReference;
        this._initialized = true;
        
        var self = this;
        
        // Create resize handler
        this._resizeHandler = function() {
            clearTimeout(self._resizeTimeout);
            self._resizeTimeout = setTimeout(function() {
                self._notifyChange();
            }, 100);
        };

        // Listen for resize events
        window.addEventListener('resize', this._resizeHandler);
        
        // Also listen for orientation changes (important for mobile)
        window.addEventListener('orientationchange', this._resizeHandler);
        
        // Initial notification
        this._notifyChange();
        
        console.log('[Mobile] Initialized - UA:', navigator.userAgent.substring(0, 50) + '...');
    },
    
    // Notify .NET of current state
    _notifyChange: function() {
        if (this._dotNetReference && this._initialized) {
            var isMobile = this.isMobile();
            console.log('[Mobile] Notifying .NET - isMobile:', isMobile);
            this._dotNetReference.invokeMethodAsync('NotifyViewportChanged', isMobile)
                .catch(function(err) {
                    console.warn('[Mobile] Notify failed:', err.message);
                });
        }
    },
    
    // Dispose/cleanup resources
    dispose: function() {
        if (!this._initialized) return;
        
        this._initialized = false;
        
        if (this._resizeHandler) {
            window.removeEventListener('resize', this._resizeHandler);
            window.removeEventListener('orientationchange', this._resizeHandler);
            this._resizeHandler = null;
        }
        
        clearTimeout(this._resizeTimeout);
        this._resizeTimeout = null;
        this._dotNetReference = null;
        
        console.log('[Mobile] Disposed');
    },

    // Scroll to top (useful for mobile navigation)
    scrollToTop: function() {
        window.scrollTo({ top: 0, behavior: 'smooth' });
    }
};

console.log('[Mobile] Script loaded');
