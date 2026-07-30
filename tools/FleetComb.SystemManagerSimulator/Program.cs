using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

var token = Value(args, "--token");
var baseUrl = Value(args, "--base-url");
var applicationValue = Value(args, "--application");
var version = Value(args, "--version");
var simulateUpdateValue = Value(args, "--simulate-update");
var watch = args.Contains("--watch", StringComparer.OrdinalIgnoreCase);
if (string.IsNullOrWhiteSpace(token))
    throw new ArgumentException(
        "Usage: simulator --token TOKEN [--base-url URL] " +
        "[--application GUID --version VERSION] [--simulate-update GUID] [--watch]");

using var client = new HttpClient
{
    BaseAddress = new Uri(
        string.IsNullOrWhiteSpace(baseUrl) ? "http://127.0.0.1:5137" : baseUrl)
};
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

using var stopping = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    stopping.Cancel();
};

await RegisterAsync(client, stopping.Token);
var heartbeat = SendHeartbeatsAsync(client, stopping.Token);

try
{
    await PrintDesiredStateAsync(client, stopping.Token);

    if (!string.IsNullOrWhiteSpace(applicationValue) &&
        Guid.TryParse(applicationValue, out var applicationId) &&
        !string.IsNullOrWhiteSpace(version))
    {
        using var response = await client.PostAsJsonAsync(
            "/local/v1/applications/report",
            new { applicationId, softwareReleaseId = (Guid?)null, version },
            stopping.Token);
        await EnsureSuccessAsync(response, "report installed software", stopping.Token);
        Console.WriteLine($"Reported Application {applicationId} version {version}.");
    }

    if (!string.IsNullOrWhiteSpace(simulateUpdateValue))
    {
        if (!Guid.TryParse(simulateUpdateValue, out var updateApplicationId))
            throw new ArgumentException("--simulate-update must be an Application GUID.");
        await SimulateUpdateAsync(client, updateApplicationId, stopping.Token);
    }

    do
    {
        await PrintStatusAsync(client, stopping.Token);
        if (watch) await Task.Delay(TimeSpan.FromSeconds(2), stopping.Token);
    } while (watch && !stopping.IsCancellationRequested);
}
catch (OperationCanceledException) when (stopping.IsCancellationRequested)
{
    // Ctrl+C is a normal simulator shutdown.
}
finally
{
    stopping.Cancel();
    try { await heartbeat; }
    catch (OperationCanceledException) { }
}

static async Task RegisterAsync(HttpClient client, CancellationToken token)
{
    using var response = await client.PostAsJsonAsync(
        "/local/v1/adapter/register",
        new
        {
            name = "FleetComb System Manager Simulator",
            version = "0.1.0",
            capabilities = new[]
            {
                "application-inventory",
                "adapter-installation",
                "update-progress"
            }
        },
        token);
    await EnsureSuccessAsync(response, "register the simulator", token);
    Console.WriteLine("System Manager simulator connected.");
}

static async Task SendHeartbeatsAsync(HttpClient client, CancellationToken token)
{
    while (!token.IsCancellationRequested)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), token);
        using var response = await client.PostAsync(
            "/local/v1/adapter/heartbeat", content: null, token);
        await EnsureSuccessAsync(response, "send the adapter heartbeat", token);
    }
}

static async Task PrintDesiredStateAsync(HttpClient client, CancellationToken token)
{
    using var response = await client.GetAsync("/local/v1/desired-state", token);
    await EnsureSuccessAsync(response, "read desired state", token);
    Console.WriteLine("Desired FleetComb configuration:");
    Console.WriteLine(Pretty(await response.Content.ReadAsStringAsync(token)));
}

static async Task SimulateUpdateAsync(
    HttpClient client, Guid applicationId, CancellationToken token)
{
    Console.WriteLine($"Requesting update for Application {applicationId}...");
    using var start = await client.PostAsync(
        $"/local/v1/applications/{applicationId}/install", content: null, token);
    await EnsureSuccessAsync(start, "request the update", token);
    var status = await start.Content.ReadAsStringAsync(token);
    Console.WriteLine(Pretty(status));

    using var document = JsonDocument.Parse(status);
    var updateState = document.RootElement.GetProperty("state").GetString();
    if (!string.Equals(updateState, "AwaitingAdapter", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine(
            "The Agent handled this installer directly; no adapter completion is required.");
        return;
    }

    Console.WriteLine("Simulating the customer's specialized installer...");
    await Task.Delay(TimeSpan.FromSeconds(1), token);
    using var complete = await client.PostAsJsonAsync(
        $"/local/v1/applications/{applicationId}/install-completion",
        new
        {
            succeeded = true,
            message = "Simulator installed and verified the release."
        },
        token);
    await EnsureSuccessAsync(complete, "complete the adapter installation", token);
    Console.WriteLine("Adapter-managed installation completed:");
    Console.WriteLine(Pretty(await complete.Content.ReadAsStringAsync(token)));
}

static async Task PrintStatusAsync(HttpClient client, CancellationToken token)
{
    using var response = await client.GetAsync("/local/v1/status", token);
    await EnsureSuccessAsync(response, "read Agent status", token);
    Console.WriteLine("Current Agent status:");
    Console.WriteLine(Pretty(await response.Content.ReadAsStringAsync(token)));
}

static async Task EnsureSuccessAsync(
    HttpResponseMessage response, string operation, CancellationToken token)
{
    if (response.IsSuccessStatusCode) return;
    var details = await response.Content.ReadAsStringAsync(token);
    throw new HttpRequestException(
        $"Could not {operation}: {(int)response.StatusCode} ({response.StatusCode}). {details}",
        inner: null, response.StatusCode);
}

static string Pretty(string json)
{
    using var document = JsonDocument.Parse(json);
    return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
}

static string Value(string[] arguments, string name)
{
    var index = Array.FindIndex(
        arguments, value => value.Equals(name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : string.Empty;
}
