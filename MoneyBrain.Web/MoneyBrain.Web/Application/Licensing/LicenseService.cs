using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;
using Stripe;
using Stripe.Checkout;

namespace MoneyBrain.Web.Application.Licensing;

/// <summary>
/// Service for managing user licenses with Stripe integration.
/// </summary>
public class LicenseService : ILicenseService
{
    private readonly ApplicationDbContext _context;
    private readonly StripeSettings _stripeSettings;
    private readonly LicensingSettings _licensingSettings;
    private readonly ILogger<LicenseService> _logger;

    public LicenseService(
        ApplicationDbContext context,
        IOptions<StripeSettings> stripeSettings,
        IOptions<LicensingSettings> licensingSettings,
        ILogger<LicenseService> logger)
    {
        _context = context;
        _stripeSettings = stripeSettings.Value;
        _licensingSettings = licensingSettings.Value;
        _logger = logger;

        // Configure Stripe API key
        if (!string.IsNullOrEmpty(_stripeSettings.SecretKey))
        {
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;
        }
    }

    public async Task<LicenseInfo?> GetLicenseAsync(string userId, CancellationToken cancellationToken = default)
    {
        // If licensing is disabled, return an active license        
        if (!_licensingSettings.Enabled)
        {
            return new LicenseInfo
            {
                UserId = userId,
                Status = LicenseStatus.Active,
                PlanName = "Self-Hosted"
            };
        }

        var license = await _context.UserLicenses
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.UserId == userId, cancellationToken);

        if (license == null)
        {
            return null;
        }

        return MapToLicenseInfo(license);
    }

    public async Task<bool> HasValidLicenseAsync(string userId, CancellationToken cancellationToken = default)
    {
        // If licensing is disabled, everyone has access
        if (!_licensingSettings.Enabled)
        {
            return true;
        }

        var license = await _context.UserLicenses
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.UserId == userId, cancellationToken);

        // If we have a valid license in the database, return true
        if (license != null && IsLicenseValid(license))
        {
            return true;
        }

        // License is invalid or doesn't exist - check with Stripe directly
        return await ValidateLicenseAsync(userId, cancellationToken);
    }

    public async Task<bool> ValidateLicenseAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!_licensingSettings.Enabled)
        {
            return true;
        }

        if (string.IsNullOrEmpty(_stripeSettings.SecretKey))
        {
            // Can't validate without Stripe configured
            _logger.LogWarning("Stripe secret key not configured, cannot validate license for user {UserId}", userId);
            return false;
        }

        var license = await _context.UserLicenses
            .FirstOrDefaultAsync(l => l.UserId == userId, cancellationToken);

        // If we have an existing subscription ID, validate it directly
        if (!string.IsNullOrEmpty(license?.StripeSubscriptionId))
        {
            try
            {
                var subscriptionService = new SubscriptionService();
                var subscription = await subscriptionService.GetAsync(license.StripeSubscriptionId, cancellationToken: cancellationToken);

                UpdateLicenseFromSubscription(license, subscription);
                license.LastValidatedAt = DateTime.UtcNow;
                license.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync(cancellationToken);
                return IsLicenseValid(license);
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Failed to validate subscription {SubscriptionId} for user {UserId}, will try email lookup",
                    license.StripeSubscriptionId, userId);
            }
        }

        // Try to find the customer by email in Stripe
        return await ValidateLicenseByEmailAsync(userId, license, cancellationToken);
    }

    /// <summary>
    /// Validates license by looking up the user's email in Stripe and checking for active subscriptions.
    /// </summary>
    private async Task<bool> ValidateLicenseByEmailAsync(string userId, UserLicense? existingLicense, CancellationToken cancellationToken)
    {
        // Get the user's email from Identity
        var user = await _context.Users.FindAsync(new object?[] { userId }, cancellationToken);
        if (string.IsNullOrEmpty(user?.Email))
        {
            _logger.LogWarning("Cannot validate license for user {UserId}: no email found", userId);
            return false;
        }

        try
        {
            var customerId = existingLicense?.StripeCustomerId;

            // If we don't have a customer ID, try to find one by email
            if (string.IsNullOrEmpty(customerId))
            {
                var customerService = new CustomerService();
                var customers = await customerService.ListAsync(new CustomerListOptions
                {
                    Email = user.Email,
                    Limit = 1
                }, cancellationToken: cancellationToken);
                customerId = customers.Data.FirstOrDefault()?.Id;
            }

            if (string.IsNullOrEmpty(customerId))
            {
                _logger.LogDebug("No Stripe customer found for email {Email}", user.Email);
                return false;
            }

            // Get active or trialing subscriptions for this customer
            var subscriptionService = new SubscriptionService();
            Subscription? activeSubscription = null;

            // Check for active subscriptions first
            var activeSubscriptions = await subscriptionService.ListAsync(new SubscriptionListOptions
            {
                Customer = customerId,
                Status = "active",
                Limit = 1
            }, cancellationToken: cancellationToken);
            activeSubscription = activeSubscriptions.Data.FirstOrDefault();

            // If no active, check for trialing
            if (activeSubscription == null)
            {
                var trialingSubscriptions = await subscriptionService.ListAsync(new SubscriptionListOptions
                {
                    Customer = customerId,
                    Status = "trialing",
                    Limit = 1
                }, cancellationToken: cancellationToken);
                activeSubscription = trialingSubscriptions.Data.FirstOrDefault();
            }

            // If still no subscription, check for past_due (grace period)
            if (activeSubscription == null)
            {
                var pastDueSubscriptions = await subscriptionService.ListAsync(new SubscriptionListOptions
                {
                    Customer = customerId,
                    Status = "past_due",
                    Limit = 1
                }, cancellationToken: cancellationToken);
                activeSubscription = pastDueSubscriptions.Data.FirstOrDefault();
            }

            if (activeSubscription == null)
            {
                // No valid subscription found - update existing license to expired if present
                if (existingLicense != null)
                {
                    existingLicense.Status = LicenseStatus.Expired;
                    existingLicense.StripeCustomerId = customerId;
                    existingLicense.LastValidatedAt = DateTime.UtcNow;
                    existingLicense.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);
                }
                _logger.LogDebug("No active subscription found for customer {CustomerId}", customerId);
                return false;
            }

            // We found a valid subscription - create or update the license record
            var license = existingLicense;
            if (license == null)
            {
                license = new UserLicense
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.UserLicenses.Add(license);
            }

            license.StripeCustomerId = customerId;
            license.StripeSubscriptionId = activeSubscription.Id;
            UpdateLicenseFromSubscription(license, activeSubscription);
            license.LastValidatedAt = DateTime.UtcNow;
            license.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Validated license from Stripe for user {UserId}, subscription {SubscriptionId}", 
                userId, activeSubscription.Id);

            return IsLicenseValid(license);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Failed to validate license from Stripe for user {UserId}", userId);
            // Return the existing license status if we have one, otherwise false
            return existingLicense != null && IsLicenseValid(existingLicense);
        }
    }

    public async Task<string> CreateCheckoutSessionAsync(string userId, string userEmail, string priceId, string successUrl, string cancelUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_stripeSettings.SecretKey))
        {
            throw new InvalidOperationException("Stripe is not configured.");
        }

        var license = await _context.UserLicenses
            .FirstOrDefaultAsync(l => l.UserId == userId, cancellationToken);

        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = ["card"],
            Mode = "subscription",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            CustomerEmail = string.IsNullOrEmpty(license?.StripeCustomerId) ? userEmail : null,
            Customer = license?.StripeCustomerId,
            ClientReferenceId = userId,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = priceId,
                    Quantity = 1
                }
            ],
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    { "userId", userId }
                }
            }
        };

        var sessionService = new SessionService();
        var session = await sessionService.CreateAsync(options, cancellationToken: cancellationToken);

        return session.Url;
    }

    public async Task<string?> CreateCustomerPortalSessionAsync(string userId, string returnUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_stripeSettings.SecretKey))
        {
            return null;
        }

        var license = await _context.UserLicenses
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.UserId == userId, cancellationToken);

        if (string.IsNullOrEmpty(license?.StripeCustomerId))
        {
            return null;
        }

        var options = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = license.StripeCustomerId,
            ReturnUrl = returnUrl
        };

        var portalService = new Stripe.BillingPortal.SessionService();
        var session = await portalService.CreateAsync(options, cancellationToken: cancellationToken);

        return session.Url;
    }

    public async Task HandleWebhookAsync(string payload, string signature, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_stripeSettings.WebhookSecret))
        {
            throw new InvalidOperationException("Stripe webhook secret is not configured");
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signature, _stripeSettings.WebhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Failed to construct Stripe event from webhook");
            throw;
        }

        _logger.LogInformation("Processing Stripe webhook event: {EventType}", stripeEvent.Type);

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                await HandleCheckoutSessionCompletedAsync(stripeEvent, cancellationToken);
                break;

            case "customer.subscription.updated":
            case "customer.subscription.deleted":
                await HandleSubscriptionUpdatedAsync(stripeEvent, cancellationToken);
                break;

            case "invoice.paid":
                await HandleInvoicePaidAsync(stripeEvent, cancellationToken);
                break;

            case "invoice.payment_failed":
                await HandlePaymentFailedAsync(stripeEvent, cancellationToken);
                break;
        }
    }

    private async Task HandleCheckoutSessionCompletedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var session = stripeEvent.Data.Object as Session;
        if (session == null) return;

        var userId = session.ClientReferenceId;
        if (string.IsNullOrEmpty(userId)) return;

        var license = await _context.UserLicenses
            .FirstOrDefaultAsync(l => l.UserId == userId, cancellationToken);

        if (license == null)
        {
            license = new UserLicense
            {
                UserId = userId
            };
            _context.UserLicenses.Add(license);
        }

        license.StripeCustomerId = session.CustomerId;
        license.StripeSubscriptionId = session.SubscriptionId;
        license.Status = LicenseStatus.Active;
        license.SubscriptionStartedAt = DateTime.UtcNow;
        license.UpdatedAt = DateTime.UtcNow;
        license.LastValidatedAt = DateTime.UtcNow;

        // Fetch subscription details to get plan name and end date
        if (!string.IsNullOrEmpty(session.SubscriptionId))
        {
            var subscriptionService = new SubscriptionService();
            var subscription = await subscriptionService.GetAsync(session.SubscriptionId, cancellationToken: cancellationToken);
            UpdateLicenseFromSubscription(license, subscription);
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Activated license for user {UserId}", userId);
    }

    private async Task HandleSubscriptionUpdatedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription == null) return;

        var userId = subscription.Metadata.TryGetValue("userId", out var id) ? id : null;
        if (string.IsNullOrEmpty(userId))
        {
            // Try to find by customer ID
            var license = await _context.UserLicenses
                .FirstOrDefaultAsync(l => l.StripeSubscriptionId == subscription.Id, cancellationToken);
                
            if (license != null)
            {
                UpdateLicenseFromSubscription(license, subscription);
                license.UpdatedAt = DateTime.UtcNow;
                license.LastValidatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        var userLicense = await _context.UserLicenses
            .FirstOrDefaultAsync(l => l.UserId == userId, cancellationToken);

        if (userLicense != null)
        {
            UpdateLicenseFromSubscription(userLicense, subscription);
            userLicense.UpdatedAt = DateTime.UtcNow;
            userLicense.LastValidatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Extracts the invoice and its linked <see cref="UserLicense"/> from a Stripe event.
    /// Returns <c>null</c> if the event payload is not an invoice or has no subscription ID.
    /// </summary>
    private async Task<(Invoice invoice, UserLicense? license)?> TryGetInvoiceLicenseAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Invoice invoice || string.IsNullOrEmpty(invoice.SubscriptionId))
            return null;

        var license = await _context.UserLicenses
            .FirstOrDefaultAsync(l => l.StripeSubscriptionId == invoice.SubscriptionId, cancellationToken);

        return (invoice, license);
    }

    private async Task HandleInvoicePaidAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var result = await TryGetInvoiceLicenseAsync(stripeEvent, cancellationToken);
        if (result == null) return;

        var (invoice, license) = result.Value;

        if (license != null && license.Status != LicenseStatus.Active)
        {
            license.Status = LicenseStatus.Active;
            license.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Reactivated license for subscription {SubscriptionId}", invoice.SubscriptionId);
        }
    }

    private async Task HandlePaymentFailedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var result = await TryGetInvoiceLicenseAsync(stripeEvent, cancellationToken);
        if (result == null) return;

        var (invoice, license) = result.Value;

        if (license != null)
        {
            // Don't immediately expire - mark for grace period
            _logger.LogWarning("Payment failed for subscription {SubscriptionId}, user {UserId}",
                invoice.SubscriptionId, license.UserId);
        }
    }

    private void UpdateLicenseFromSubscription(UserLicense license, Subscription subscription)
    {
        license.Status = subscription.Status switch
        {
            "active" => LicenseStatus.Active,
            "trialing" => LicenseStatus.Trial,
            "canceled" => LicenseStatus.Cancelled,
            "past_due" => LicenseStatus.Active, // Keep active during grace period
            "unpaid" => LicenseStatus.Expired,
            _ => LicenseStatus.Invalid
        };

        // CurrentPeriodEnd is always set for active subscriptions
        if (subscription.CurrentPeriodEnd != default)
        {
            license.SubscriptionEndsAt = subscription.CurrentPeriodEnd;
        }

        // TrialEnd is nullable
        if (subscription.TrialEnd.HasValue)
        {
            license.TrialEndsAt = subscription.TrialEnd.Value;
        }

        // Get plan name from the first item
        var firstItem = subscription.Items?.Data?.FirstOrDefault();
        if (firstItem?.Price?.Nickname != null)
        {
            license.PlanName = firstItem.Price.Nickname;
        }
        else if (firstItem?.Price?.Recurring?.Interval != null)
        {
            license.PlanName = firstItem.Price.Recurring.Interval switch
            {
                "month" => "Monthly",
                "year" => "Yearly",
                _ => "Subscription"
            };
        }
    }

    private bool IsLicenseValid(UserLicense license)
    {
        if (license.Status == LicenseStatus.Active || license.Status == LicenseStatus.Trial)
        {
            // Check if not expired
            if (license.Status == LicenseStatus.Trial && license.TrialEndsAt.HasValue)
            {
                return license.TrialEndsAt.Value > DateTime.UtcNow;
            }

            if (license.SubscriptionEndsAt.HasValue)
            {
                // Include grace period
                return license.SubscriptionEndsAt.Value.AddDays(_licensingSettings.GracePeriodDays) > DateTime.UtcNow;
            }

            return true;
        }

        // Check grace period for expired licenses
        if (license.Status == LicenseStatus.Expired && license.SubscriptionEndsAt.HasValue)
        {
            return license.SubscriptionEndsAt.Value.AddDays(_licensingSettings.GracePeriodDays) > DateTime.UtcNow;
        }

        return false;
    }

    private static LicenseInfo MapToLicenseInfo(UserLicense license)
    {
        return new LicenseInfo
        {
            UserId = license.UserId,
            Status = license.Status,
            StripeCustomerId = license.StripeCustomerId,
            StripeSubscriptionId = license.StripeSubscriptionId,
            PlanName = license.PlanName,
            ExpiresAt = license.SubscriptionEndsAt,
            TrialEndsAt = license.TrialEndsAt
        };
    }
}
