using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

var token = Value(args, "--token");
var applicationValue = Value(args, "--application");
var version = Value(args, "--version");
var watch = args.Contains("--watch", StringComparer.OrdinalIgnoreCase);
if (string.IsNullOrWhiteSpace(token))
    throw new ArgumentException(
        "Usage: simulator --token TOKEN [--application GUID --version VERSION] [--watch]");

using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5137") };
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var desired = await client.GetStringAsync("/local/v1/desired-state");
Console.WriteLine("Desired FleetComb configuration:");
Console.WriteLine(Pretty(desired));

if (!string.IsNullOrWhiteSpace(applicationValue) &&
    Guid.TryParse(applicationValue, out var applicationId) &&
    !string.IsNullOrWhiteSpace(version))
{
    using var response = await client.PostAsJsonAsync(
        "/local/v1/applications/report",
        new { applicationId, softwareReleaseId = (Guid?)null, version });
    response.EnsureSuccessStatusCode();
    Console.WriteLine($"Reported Application {applicationId} version {version}.");
}

do
{
    var update = await client.GetStringAsync("/local/v1/updates/current");
    Console.WriteLine("Current update:");
    Console.WriteLine(Pretty(update));
    if (watch) await Task.Delay(TimeSpan.FromSeconds(2));
} while (watch);

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
