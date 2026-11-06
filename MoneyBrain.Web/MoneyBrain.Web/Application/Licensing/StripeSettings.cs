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
    /// Stripe Price ID for the MoneyBrain Cloud monthly subscription.
    /// </summary>
    public string CloudMonthlyPriceId { get; set; } = string.Empty;

    /// <summary>
    /// Stripe Price ID for the MoneyBrain Cloud annual subscription.
    /// </summary>
    public string CloudAnnualPriceId { get; set; } = string.Empty;
}
