using System.IO;
using System.Text.Json;
using SimuladorTCP.Models;

namespace SimuladorTCP.Services;

/// <summary>
/// Servicio de persistencia de configuración en JSON.
/// </summary>
public static class JsonConfigService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task SaveAsync(string path, AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, Options);
        await File.WriteAllTextAsync(path, json);
    }

    public static async Task<AppSettings> LoadAsync(string path)
    {
        if (!File.Exists(path))
            return new AppSettings();

        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
    }
}
