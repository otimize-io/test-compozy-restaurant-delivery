using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RestaurantDelivery.Gateway.Roles;

namespace E2E.Tests.Gateway;

/// <summary>
/// Unit tests for the role switcher (task_14.2, ADR-002): the <c>X-Demo-Role</c> header selects a pre-seeded
/// identity, which the middleware attaches to the request and forwards upstream — with no real authentication.
/// </summary>
public class RoleSwitcherMiddlewareTests
{
    private static async Task<HttpContext> InvokeAsync(string? role)
    {
        var context = new DefaultHttpContext();
        if (role is not null)
        {
            context.Request.Headers[DemoIdentities.HeaderName] = role;
        }

        var middleware = new RoleSwitcherMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);
        return context;
    }

    [Fact]
    public async Task Restaurant_role_attaches_the_seeded_restaurant_identity()
    {
        var context = await InvokeAsync("restaurant");

        var identity = Assert.IsType<DemoIdentity>(context.Items[RoleSwitcherMiddleware.IdentityItemKey]);
        Assert.Equal(DemoIdentities.Restaurant, identity.Role);
        Assert.Equal(DemoIdentities.Resolve("restaurant")!.UserId, identity.UserId);

        // The principal carries the role/name/id claims (but stays unauthenticated — no real auth in V1).
        Assert.Equal("restaurant", context.User.FindFirstValue(ClaimTypes.Role));
        Assert.Equal(identity.UserId.ToString(), context.User.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.False(context.User.Identity!.IsAuthenticated);

        // The acting identity is forwarded to upstream services so the BFF, not the client, supplies it.
        Assert.Equal(identity.UserId.ToString(), context.Request.Headers[RoleSwitcherMiddleware.ForwardedUserIdHeader]);
    }

    [Theory]
    [InlineData("consumer")]
    [InlineData("Consumer")]
    [InlineData("DRIVER")]
    public async Task Role_resolution_is_case_insensitive(string role)
    {
        var context = await InvokeAsync(role);
        var identity = Assert.IsType<DemoIdentity>(context.Items[RoleSwitcherMiddleware.IdentityItemKey]);
        Assert.Equal(role.ToLowerInvariant(), identity.Role);
    }

    [Fact]
    public async Task A_missing_role_leaves_the_request_anonymous_without_failing()
    {
        var context = await InvokeAsync(role: null);

        Assert.False(context.Items.ContainsKey(RoleSwitcherMiddleware.IdentityItemKey));
        Assert.False(context.Request.Headers.ContainsKey(RoleSwitcherMiddleware.ForwardedUserIdHeader));
    }

    [Fact]
    public async Task An_unknown_role_leaves_the_request_anonymous_without_failing()
    {
        var context = await InvokeAsync("manager");

        Assert.False(context.Items.ContainsKey(RoleSwitcherMiddleware.IdentityItemKey));
    }

    [Fact]
    public void The_three_demo_identities_are_exposed_for_the_switcher()
    {
        var roles = DemoIdentities.All.Select(i => i.Role).ToArray();
        Assert.Equal([DemoIdentities.Consumer, DemoIdentities.Restaurant, DemoIdentities.Driver], roles);
        Assert.All(DemoIdentities.All, i => Assert.NotEqual(Guid.Empty, i.UserId));
    }
}
