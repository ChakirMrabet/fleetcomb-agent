namespace FleetComb.Agent.Application.Abstractions;

public interface IAgentIdentityProvider
{
    (string PublicKey, string PrivateKey) Create();
    string CreateLocalApiToken();
}
