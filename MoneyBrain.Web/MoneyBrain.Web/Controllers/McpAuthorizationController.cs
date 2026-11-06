using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using MoneyBrain.Web.Application.Mcp;
using MoneyBrain.Web.Data;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MoneyBrain.Web.Controllers;

/// <summary>Identity-backed explicit OAuth consent and per-user connected-client revocation.</summary>
[EnableRateLimiting(McpHosting.Policy)]
public sealed class McpAuthorizationController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signInManager,
    IOpenIddictApplicationManager applications,
    IOpenIddictAuthorizationManager authorizations,
    IOpenIddictTokenManager tokens,
    IOptions<McpSettings> settings) : Controller
{
    /// <summary>Displays explicit consent after the protocol stack validates the request.</summary>
    [HttpGet("~/connect/authorize")]
    [AllowAnonymous]
    public async Task<IActionResult> AuthorizeClient(CancellationToken cancellationToken)
    {
        if (!settings.Value.Enabled)
            return NotFound();
        var request = HttpContext.GetOpenIddictServerRequest() ??
            throw new InvalidOperationException("The OAuth request was not processed by OpenIddict.");
        var login = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!login.Succeeded || login.Principal is null)
        {
            if (request.HasPromptValue(PromptValues.None))
                return OAuthError(Errors.LoginRequired);
            return Challenge(new AuthenticationProperties { RedirectUri = Request.GetEncodedPathAndQuery() },
                IdentityConstants.ApplicationScheme);
        }
        if (request.HasPromptValue(PromptValues.None))
            return OAuthError(Errors.ConsentRequired);

        var user = await users.GetUserAsync(login.Principal);
        if (user is null || !await signInManager.CanSignInAsync(user) || await users.IsLockedOutAsync(user))
            return OAuthError(Errors.AccessDenied);

        var application = await applications.FindByClientIdAsync(request.ClientId ?? "", cancellationToken);
        if (application is null)
            return OAuthError(Errors.InvalidClient);
        var name = await applications.GetDisplayNameAsync(application, cancellationToken) ?? request.ClientId ?? "";
        var parameters = Request.Query
            .SelectMany(pair => pair.Value.Select(value => new KeyValuePair<string, string>(pair.Key, value ?? "")))
            .ToArray();
        return View("Consent", new McpConsentViewModel(name, request.GetScopes().ToArray(), parameters));
    }

    /// <summary>Records only the scopes shown in the validated consent request.</summary>
    [HttpPost("~/connect/authorize")]
    [ValidateAntiForgeryToken]
    [AllowAnonymous]
    public async Task<IActionResult> Consent([FromForm] string decision, CancellationToken cancellationToken)
    {
        if (!settings.Value.Enabled)
            return NotFound();
        var request = HttpContext.GetOpenIddictServerRequest() ??
            throw new InvalidOperationException("The OAuth request was not processed by OpenIddict.");
        var login = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!login.Succeeded || login.Principal is null || decision != "accept")
            return OAuthError(Errors.AccessDenied);
        var user = await users.GetUserAsync(login.Principal);
        if (user is null || !await signInManager.CanSignInAsync(user) || await users.IsLockedOutAsync(user))
            return OAuthError(Errors.AccessDenied);
        var application = await applications.FindByClientIdAsync(request.ClientId ?? "", cancellationToken);
        if (application is null)
            return OAuthError(Errors.InvalidClient);
        var applicationId = await applications.GetIdAsync(application, cancellationToken);
        if (applicationId is null)
            return OAuthError(Errors.InvalidClient);

        var identity = new ClaimsIdentity("MoneyBrain.OAuth");
        identity.SetClaim(Claims.Subject, user.Id);
        identity.SetScopes(request.GetScopes());
        identity.SetResources(settings.Value.Resource);
        var principal = new ClaimsPrincipal(identity);
        object? authorization = null;
        var requestedScopes = request.GetScopes().ToHashSet(StringComparer.Ordinal);
        await foreach (var existing in authorizations.FindAsync(
            subject: user.Id, client: applicationId, status: Statuses.Valid,
            type: AuthorizationTypes.Permanent, scopes: null, cancellationToken))
        {
            if (requestedScopes.SetEquals(await authorizations.GetScopesAsync(existing, cancellationToken)))
            {
                authorization = existing;
                break;
            }
        }
        authorization ??= await authorizations.CreateAsync(
            principal, user.Id, applicationId, AuthorizationTypes.Permanent, request.GetScopes(), cancellationToken);
        principal.SetAuthorizationId(await authorizations.GetIdAsync(authorization, cancellationToken));
        // No names, email, financial data or identity cookies are copied into access tokens.
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>Exchanges a validated, single-use authorization code for a short-lived access token.</summary>
    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    [AllowAnonymous]
    public async Task<IActionResult> Exchange(CancellationToken cancellationToken)
    {
        if (!settings.Value.Enabled)
            return NotFound();
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request?.IsAuthorizationCodeGrantType() != true)
            return OAuthError(Errors.UnsupportedGrantType);
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal is null)
            return OAuthError(Errors.InvalidGrant);
        var userId = result.Principal.GetClaim(Claims.Subject);
        var user = userId is null ? null : await users.FindByIdAsync(userId);
        if (user is null || !await signInManager.CanSignInAsync(user) || await users.IsLockedOutAsync(user))
            return OAuthError(Errors.InvalidGrant);
        result.Principal.SetResources(settings.Value.Resource);
        return SignIn(result.Principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>Lists this user's valid grants, without exposing other users' authorizations.</summary>
    [HttpGet("~/Account/Manage/ConnectedClients")]
    [Authorize(AuthenticationSchemes = "Identity.Application")]
    public async Task<IActionResult> ConnectedClients(CancellationToken cancellationToken)
    {
        if (!settings.Value.Enabled)
            return NotFound();
        var user = await users.GetUserAsync(User);
        if (user is null)
            return Challenge(IdentityConstants.ApplicationScheme);
        var grants = new List<McpGrantViewModel>();
        var storedGrants = new List<object>();
        await foreach (var grant in authorizations.FindAsync(
            subject: user.Id, client: null, status: Statuses.Valid,
            type: AuthorizationTypes.Permanent, scopes: [McpAccess.ReadScope], cancellationToken))
        {
            storedGrants.Add(grant);
            if (storedGrants.Count == 100)
                break;
        }
        // Close the reader before querying related applications; deployments need not enable MARS.
        foreach (var grant in storedGrants)
        {
            var applicationId = await authorizations.GetApplicationIdAsync(grant, cancellationToken);
            var application = applicationId is null ? null : await applications.FindByIdAsync(applicationId, cancellationToken);
            var id = await authorizations.GetIdAsync(grant, cancellationToken);
            if (id is null)
                continue;
            grants.Add(new(id, application is null ? "" : await applications.GetDisplayNameAsync(application, cancellationToken) ?? "",
                (await authorizations.GetScopesAsync(grant, cancellationToken)).ToArray(),
                await authorizations.GetCreationDateAsync(grant, cancellationToken)));
        }
        return View("ConnectedClients", grants);
    }

    /// <summary>Revokes a grant and all outstanding codes/tokens after an ownership check.</summary>
    [HttpPost("~/Account/Manage/ConnectedClients/Revoke")]
    [ValidateAntiForgeryToken]
    [Authorize(AuthenticationSchemes = "Identity.Application")]
    public async Task<IActionResult> Revoke([FromForm] string grantId, CancellationToken cancellationToken)
    {
        if (!settings.Value.Enabled)
            return NotFound();
        var user = await users.GetUserAsync(User);
        if (user is null)
            return Challenge(IdentityConstants.ApplicationScheme);
        if (string.IsNullOrWhiteSpace(grantId) || grantId.Length > 100)
            return BadRequest();
        var grant = await authorizations.FindByIdAsync(grantId, cancellationToken);
        if (grant is null || await authorizations.GetSubjectAsync(grant, cancellationToken) != user.Id)
            return NotFound();
        if (!await authorizations.TryRevokeAsync(grant, cancellationToken))
            return Conflict();
        await tokens.RevokeByAuthorizationIdAsync(grantId, cancellationToken);
        return Redirect("/Account/Manage/ConnectedClients");
    }

    /// <summary>Revokes all of the current user's MCP grants, including those outside the displayed page.</summary>
    [HttpPost("~/Account/Manage/ConnectedClients/RevokeAll")]
    [ValidateAntiForgeryToken]
    [Authorize(AuthenticationSchemes = "Identity.Application")]
    public async Task<IActionResult> RevokeAll(CancellationToken cancellationToken)
    {
        if (!settings.Value.Enabled)
            return NotFound();
        var user = await users.GetUserAsync(User);
        if (user is null)
            return Challenge(IdentityConstants.ApplicationScheme);

        while (true)
        {
            var batch = new List<object>();
            await foreach (var grant in authorizations.FindAsync(
                subject: user.Id, client: null, status: Statuses.Valid, type: AuthorizationTypes.Permanent,
                scopes: [McpAccess.ReadScope], cancellationToken))
            {
                batch.Add(grant);
                if (batch.Count == 100)
                    break;
            }
            if (batch.Count == 0)
                return Redirect("/Account/Manage/ConnectedClients");

            // Bounded batches close the reader before updates and do not require SQL Server MARS.
            foreach (var grant in batch)
            {
                var id = await authorizations.GetIdAsync(grant, cancellationToken);
                if (id is null || !await authorizations.TryRevokeAsync(grant, cancellationToken))
                    return Conflict();
                await tokens.RevokeByAuthorizationIdAsync(id, cancellationToken);
            }
        }
    }

    private ForbidResult OAuthError(string error) => Forbid(new AuthenticationProperties(new Dictionary<string, string?>
    {
        [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The authorization request could not be completed."
    }), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
}
