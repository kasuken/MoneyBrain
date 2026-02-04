// MoneyBrain Mobile Detection & Utilities
window.moneybrainMobile = {
    // Mobile breakpoint (matches Bootstrap/MudBlazor md breakpoint)
    MOBILE_BREAKPOINT: 768,

    // Check if current viewport is mobile
    isMobile: function() {
        return window.innerWidth < this.MOBILE_BREAKPOINT;
    },

    // Initialize mobile detection with callback to Blazor
    initialize: function(dotNetReference) {
        const notifyViewportChange = () => {
            const isMobile = this.isMobile();
            dotNetReference.invokeMethodAsync('NotifyViewportChanged', isMobile);
        };

        // Initial check
        notifyViewportChange();

        // Listen for resize events (debounced)
        let resizeTimeout;
        window.addEventListener('resize', () => {
            clearTimeout(resizeTimeout);
            resizeTimeout = setTimeout(notifyViewportChange, 150);
        });
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
