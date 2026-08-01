using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Application.Adapters;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FleetComb.Agent.Api.Authentication;

public sealed class LocalApiAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IAgentRegistrationStore registrations,
    ILocalAdapterStore adapters)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var supplied = BearerToken();
        if (string.IsNullOrWhiteSpace(supplied))
            return AuthenticateResult.NoResult();

        var registration = await registrations.LoadAsync(Context.RequestAborted);
        if (registration is null)
            return AuthenticateResult.Fail("The local API bearer token is missing or invalid.");

        var claims = new List<Claim>();
        if (FixedTimeEquals(supplied, registration.LocalApiToken))
        {
            claims.Add(new Claim(
                ClaimTypes.NameIdentifier, registration.InstallationId.ToString()));
            claims.Add(new Claim("credential_type", "bootstrap"));
            claims.AddRange(LocalAdapterScopes.All.Select(scope => new Claim("scope", scope)));
        }
        else
        {
            var hash = Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(supplied)));
            var adapter = await adapters.FindByTokenHashAsync(hash, Context.RequestAborted);
            if (adapter is null)
                return AuthenticateResult.Fail(
                    "The local API bearer token is missing or invalid.");
            claims.Add(new Claim(ClaimTypes.NameIdentifier, adapter.Id.ToString()));
            claims.Add(new Claim("adapter_id", adapter.Id.ToString()));
            claims.Add(new Claim("adapter_name", adapter.Name));
            claims.Add(new Claim("credential_type", "adapter"));
            claims.AddRange(adapter.Scopes.Select(scope => new Claim("scope", scope)));
        }
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    private string BearerToken()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return authorization[7..].Trim();
        return Request.Query.TryGetValue("access_token", out var query)
            ? query.ToString()
            : string.Empty;
    }

    private static bool FixedTimeEquals(string supplied, string expected)
    {
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return suppliedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
    }
}
