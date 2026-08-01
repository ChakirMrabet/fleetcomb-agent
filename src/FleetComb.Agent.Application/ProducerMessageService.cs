using System.Text.Json;
using System.Text.Json.Nodes;
using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Application;

public sealed class ProducerMessageService(IProducerMessageStore messages)
{
    public async Task<ProducerMessage> SubmitAsync(
        Guid adapterId, string kind, string schema, string severity, JsonElement payload,
        CancellationToken token)
    {
        if (adapterId == Guid.Empty)
            throw new UnauthorizedAccessException("A scoped adapter credential is required.");
        var payloadJson = payload.GetRawText();
        if (payloadJson.Length > 64 * 1024)
            throw new ArgumentException("Telemetry payloads cannot exceed 64 KiB.");
        var sanitized = kind == "log" ? RedactLogPayload(payloadJson) : payloadJson;
        var existing = await messages.LoadPendingAsync(1, token);
        var sequence = existing.Count == 0 ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() :
            Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), existing.Max(x => x.Sequence) + 1);
        var message = new ProducerMessage(
            Guid.NewGuid(), adapterId, sequence, kind, schema.Trim(), severity.Trim(),
            sanitized, DateTimeOffset.UtcNow, 0, null);
        await messages.AppendAsync(message, token);
        return message;
    }

    private static string RedactLogPayload(string payloadJson)
    {
        var root = JsonNode.Parse(payloadJson)
            ?? throw new ArgumentException("The log payload must contain JSON.");
        Redact(root);
        return root.ToJsonString();
    }

    private static void Redact(JsonNode node)
    {
        if (node is JsonObject value)
        {
            foreach (var property in value.ToArray())
            {
                if (IsSecretName(property.Key)) value[property.Key] = "[REDACTED]";
                else if (property.Value is not null) Redact(property.Value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
                if (child is not null) Redact(child);
        }
    }

    private static bool IsSecretName(string name) =>
        name.Contains("password", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("token", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("authorization", StringComparison.OrdinalIgnoreCase);
}
