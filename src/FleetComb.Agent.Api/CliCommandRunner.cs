using FleetComb.Agent.Application.Enrollment.Commands;
using FleetComb.Agent.Application.Enrollment.Queries;
using MediatR;

namespace FleetComb.Agent.Api;

public static class CliCommandRunner
{
    public static async Task<bool> TryRunAsync(string[] args, IServiceProvider services)
    {
        var command = args.FirstOrDefault();
        if (command?.Equals("enroll", StringComparison.OrdinalIgnoreCase) == true)
        {
            await EnrollAsync(
                args.Skip(1).ToArray(),
                services.GetRequiredService<IMediator>());
            return true;
        }
        if (command?.Equals("local-token", StringComparison.OrdinalIgnoreCase) == true)
        {
            Console.WriteLine(await services.GetRequiredService<IMediator>()
                .Send(new GetLocalApiToken.Query(), CancellationToken.None));
            return true;
        }
        return false;
    }

    private static async Task EnrollAsync(
        string[] arguments,
        IMediator mediator)
    {
        var server = Value(arguments, "--server");
        var code = Value(arguments, "--code");
        if (!Uri.TryCreate(server, UriKind.Absolute, out var serverUrl) ||
            string.IsNullOrWhiteSpace(code))
            throw new ArgumentException(
                "Usage: FleetComb.Agent enroll --server https://fleetcomb.example --code FC1-...");
        var result = await mediator.Send(
            new EnrollAgent.Command(serverUrl, code), CancellationToken.None);
        Console.WriteLine($"FleetComb Agent enrolled for Asset {result.AssetId}.");
        Console.WriteLine($"Identity saved under: {result.DataDirectory}");
        Console.WriteLine("Local API bearer token:");
        Console.WriteLine(result.LocalApiToken);
        Console.WriteLine(
            "Start the Agent and open its Web UI to create the local administrator.");
    }

    private static string Value(string[] arguments, string name)
    {
        var index = Array.FindIndex(
            arguments,
            value => value.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < arguments.Length
            ? arguments[index + 1]
            : string.Empty;
    }
}
