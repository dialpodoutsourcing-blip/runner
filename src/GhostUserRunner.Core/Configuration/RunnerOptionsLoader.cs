using System.Text.Json;

namespace GhostUserRunner.Core.Configuration;

public static class RunnerOptionsLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip };
    public static RunnerOptions Load(string json) => JsonSerializer.Deserialize<RunnerOptions>(json, JsonOptions) ?? throw new InvalidDataException("Configuration is empty or invalid.");
    public static RunnerOptions LoadFile(string path) => Load(File.ReadAllText(path));
}
