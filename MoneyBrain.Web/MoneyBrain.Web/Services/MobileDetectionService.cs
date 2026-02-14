using Microsoft.JSInterop;

namespace MoneyBrain.Web.Services;

public interface IMobileDetectionService
{
    bool IsMobile { get; }
    bool IsInitialized { get; }
    event Action<bool>? OnViewportChanged;
    Task InitializeAsync();
}

public class MobileDetectionService : IMobileDetectionService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private DotNetObjectReference<MobileDetectionService>? _dotNetRef;
    private bool _isMobile;
    private bool _isInitialized;
    private bool _isDisposed;

    public bool IsMobile => _isMobile;
    public bool IsInitialized => _isInitialized;
    public event Action<bool>? OnViewportChanged;

    public MobileDetectionService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync()
    {
        // Prevent multiple initializations or initialization after disposal
        if (_isInitialized || _isDisposed)
            return;
            
        _dotNetRef = DotNetObjectReference.Create(this);
        
        try
        {
            await _jsRuntime.InvokeVoidAsync("window.moneybrainMobile.initialize", _dotNetRef);
            _isInitialized = true;
        }
        catch
        {
            // If JS call fails, dispose the reference to avoid leaks
            _dotNetRef?.Dispose();
            _dotNetRef = null;
            throw;
        }
    }

    [JSInvokable]
    public void NotifyViewportChanged(bool isMobile)
    {
        // Ignore if disposed
        if (_isDisposed)
            return;
            
        if (_isMobile != isMobile)
        {
            _isMobile = isMobile;
            OnViewportChanged?.Invoke(isMobile);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;
            
        _isDisposed = true;
        
        // Clean up JavaScript event listeners
        try
        {
            await _jsRuntime.InvokeVoidAsync("window.moneybrainMobile.dispose");
        }
        catch
        {
            // Ignore errors during disposal (circuit might already be closed)
        }
        
        _dotNetRef?.Dispose();
        _dotNetRef = null;
    }
}
