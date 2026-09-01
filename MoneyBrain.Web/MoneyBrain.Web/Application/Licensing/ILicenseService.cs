namespace MoneyBrain.Web.Application.Licensing;

/// <summary>
/// Service for managing user licenses and Stripe subscription integration.
/// </summary>
public interface ILicenseService
{
    /// <summary>
    /// Gets the current license information for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>License information, or null if no license exists.</returns>
    Task<LicenseInfo?> GetLicenseAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has a valid license (active subscription or trial).
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if the user has a valid license.</returns>
    Task<bool> HasValidLicenseAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the license against Stripe and updates the local cache.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if the license is valid after validation.</returns>
    Task<bool> ValidateLicenseAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Stripe Checkout session for subscription purchase.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="userEmail">The user's email address.</param>
    /// <param name="priceId">The Stripe price ID.</param>
    /// <param name="successUrl">URL to redirect to on success.</param>
    /// <param name="cancelUrl">URL to redirect to on cancellation.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The Stripe Checkout session URL.</returns>
    Task<string> CreateCheckoutSessionAsync(string userId, string userEmail, string priceId, string successUrl, string cancelUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Stripe Customer Portal session for subscription management.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="returnUrl">URL to redirect to after portal session.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The Stripe Customer Portal URL.</returns>
    Task<string?> CreateCustomerPortalSessionAsync(string userId, string returnUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles Stripe webhook events for subscription lifecycle.
    /// </summary>
    /// <param name="payload">The raw webhook payload.</param>
    /// <param name="signature">The Stripe signature header.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task HandleWebhookAsync(string payload, string signature, CancellationToken cancellationToken = default);
}
