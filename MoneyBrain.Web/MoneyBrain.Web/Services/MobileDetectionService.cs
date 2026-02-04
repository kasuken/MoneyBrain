using Microsoft.JSInterop;

namespace MoneyBrain.Web.Services;

public interface IMobileDetectionService
{
    bool IsMobile { get; }
    event Action<bool>? OnViewportChanged;
    Task InitializeAsync();
}

public class MobileDetectionService : IMobileDetectionService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private DotNetObjectReference<MobileDetectionService>? _dotNetRef;
    private bool _isMobile;

    public bool IsMobile => _isMobile;
    public event Action<bool>? OnViewportChanged;

    public MobileDetectionService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync()
    {
        _dotNetRef = DotNetObjectReference.Create(this);
        await _jsRuntime.InvokeVoidAsync("window.moneybrainMobile.initialize", _dotNetRef);
    }

    [JSInvokable]
    public void NotifyViewportChanged(bool isMobile)
    {
        if (_isMobile != isMobile)
        {
            _isMobile = isMobile;
            OnViewportChanged?.Invoke(isMobile);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dotNetRef?.Dispose();
    }
}
