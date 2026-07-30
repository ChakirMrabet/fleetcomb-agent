using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using FleetComb.Agent.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FleetComb.Agent.Api.Authentication;

public sealed class LocalApiAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IAgentRegistrationStore registrations)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var registration = await registrations.LoadAsync(Context.RequestAborted);
        var supplied = Request.Headers.Authorization.ToString();
        var expected = registration is null ? "" : $"Bearer {registration.LocalApiToken}";
        if (string.IsNullOrWhiteSpace(expected) || !FixedTimeEquals(supplied, expected))
            return AuthenticateResult.Fail("The local API bearer token is missing or invalid.");

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, registration!.InstallationId.ToString())],
            Scheme.Name);
        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    private static bool FixedTimeEquals(string supplied, string expected)
    {
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return suppliedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
    }
}
