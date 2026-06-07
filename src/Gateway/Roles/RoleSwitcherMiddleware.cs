using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace RestaurantDelivery.Gateway.Roles;

/// <summary>
/// The role switcher (ADR-002, task_14.2). Reads the <c>X-Demo-Role</c> header, resolves the matching
/// pre-seeded <see cref="DemoIdentity"/>, and attaches it to the request as a <see cref="ClaimsPrincipal"/>
/// (no real authentication). The resolved identity is also stored in <see cref="HttpContext.Items"/> so the
/// gateway can surface it, and the user id is forwarded to upstream services as <c>X-Demo-User-Id</c> so the
/// BFF — not the client — supplies the acting identity. A missing or unknown role leaves the request
/// anonymous (it does not fail): unauthenticated demo browsing is allowed.
/// </summary>
public sealed class RoleSwitcherMiddleware(RequestDelegate next)
{
    /// <summary>The key under which the resolved <see cref="DemoIdentity"/> is stored on the request.</summary>
    public const string IdentityItemKey = "DemoIdentity";

    /// <summary>The header carrying the resolved demo user id to upstream services.</summary>
    public const string ForwardedUserIdHeader = "X-Demo-User-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var role = context.Request.Headers[DemoIdentities.HeaderName].ToString();
        var identity = DemoIdentities.Resolve(role);

        if (identity is not null)
        {
            context.Items[IdentityItemKey] = identity;
            context.User = BuildPrincipal(identity);

            // Forward the acting identity to upstream services so the demo role, not the client body,
            // determines who is acting. YARP copies request headers to the proxied request.
            context.Request.Headers[ForwardedUserIdHeader] = identity.UserId.ToString();
            context.Request.Headers[DemoIdentities.HeaderName] = identity.Role;
        }

        await next(context);
    }

    /// <summary>Builds a non-authenticating claims principal carrying the demo role and identity.</summary>
    private static ClaimsPrincipal BuildPrincipal(DemoIdentity identity)
    {
        // AuthenticationType is null on purpose: IsAuthenticated stays false (ADR-002 — no real auth),
        // while the role/name/id claims are still available to the gateway and to anything that inspects User.
        var claimsIdentity = new ClaimsIdentity(authenticationType: null);
        claimsIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, identity.UserId.ToString()));
        claimsIdentity.AddClaim(new Claim(ClaimTypes.Name, identity.DisplayName));
        claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, identity.Role));
        return new ClaimsPrincipal(claimsIdentity);
    }
}
