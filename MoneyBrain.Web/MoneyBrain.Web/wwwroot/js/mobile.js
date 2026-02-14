// MoneyBrain Mobile Detection & Utilities
window.moneybrainMobile = {
    // Mobile breakpoint (matches Bootstrap/MudBlazor md breakpoint)
    MOBILE_BREAKPOINT: 768,
    
    // Store reference for cleanup
    _dotNetReference: null,
    _resizeHandler: null,
    _resizeTimeout: null,

    // Check if current viewport is mobile
    isMobile: function() {
        return window.innerWidth < this.MOBILE_BREAKPOINT;
    },

    // Initialize mobile detection with callback to Blazor
    initialize: function(dotNetReference) {
        // Clean up any previous instance first
        this.dispose();
        
        this._dotNetReference = dotNetReference;
        
        const self = this;
        this._resizeHandler = function() {
            clearTimeout(self._resizeTimeout);
            self._resizeTimeout = setTimeout(function() {
                if (self._dotNetReference) {
                    const isMobile = self.isMobile();
                    self._dotNetReference.invokeMethodAsync('NotifyViewportChanged', isMobile)
                        .catch(function(err) {
                            // Reference was disposed, clean up
                            console.warn('Mobile detection reference disposed, cleaning up');
                            self.dispose();
                        });
                }
            }, 150);
        };

        // Listen for resize events
        window.addEventListener('resize', this._resizeHandler);
        
        // Initial check
        if (this._dotNetReference) {
            const isMobile = this.isMobile();
            this._dotNetReference.invokeMethodAsync('NotifyViewportChanged', isMobile)
                .catch(function(err) {
                    console.warn('Initial mobile detection failed', err);
                });
        }
    },
    
    // Dispose/cleanup resources
    dispose: function() {
        if (this._resizeHandler) {
            window.removeEventListener('resize', this._resizeHandler);
            this._resizeHandler = null;
        }
        clearTimeout(this._resizeTimeout);
        this._resizeTimeout = null;
        this._dotNetReference = null;
    },

    // Scroll to top (useful for mobile navigation)
    scrollToTop: function() {
        window.scrollTo({ top: 0, behavior: 'smooth' });
    },

    // Get safe area insets for devices with notches
    getSafeAreaInsets: function() {
        const style = getComputedStyle(document.documentElement);
        return {
            top: parseInt(style.getPropertyValue('env(safe-area-inset-top)') || '0'),
            right: parseInt(style.getPropertyValue('env(safe-area-inset-right)') || '0'),
            bottom: parseInt(style.getPropertyValue('env(safe-area-inset-bottom)') || '0'),
            left: parseInt(style.getPropertyValue('env(safe-area-inset-left)') || '0')
        };
    },

    // Prevent pull-to-refresh on mobile (if needed)
    disablePullToRefresh: function() {
        let touchStartY = 0;
        document.addEventListener('touchstart', (e) => {
            touchStartY = e.touches[0].clientY;
        }, { passive: false });

        document.addEventListener('touchmove', (e) => {
            const touchY = e.touches[0].clientY;
            const touchDelta = touchY - touchStartY;
            
            if (touchDelta > 0 && window.scrollY === 0) {
                e.preventDefault();
            }
        }, { passive: false });
    }
};
