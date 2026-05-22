namespace SimuladorTCP.Models;

/// <summary>
/// Representa una trama configurable para envío automatizado.
/// </summary>
public class Frame
{
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Format { get; set; } = "HEX"; // ASCII o HEX
    public string Content { get; set; } = string.Empty;
    public int DelayMs { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}
