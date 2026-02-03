namespace MoneyBrain.Web.Application.Licensing;

/// <summary>
/// Configuration settings for Stripe integration.
/// </summary>
public class StripeSettings
{
    /// <summary>
    /// Stripe secret API key.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Stripe publishable API key (for client-side).
    /// </summary>
    public string PublishableKey { get; set; } = string.Empty;

    /// <summary>
    /// Stripe webhook signing secret.
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>
    /// Valid Stripe product IDs for this application.
    /// </summary>
    public List<string> ProductIds { get; set; } = [];
}
