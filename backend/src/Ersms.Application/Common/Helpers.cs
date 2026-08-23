using System.Text.Json;

namespace Ersms.Application.Common;

public static class JsonAudit
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string? Serialize(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, Options);
}
