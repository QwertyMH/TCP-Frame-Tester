using System.ComponentModel;

namespace SimuladorTCP.Models;

/// <summary>
/// Entrada individual del monitor de comunicación (log).
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Direction { get; set; } = "TX"; // TX o RX
    public byte[] RawData { get; set; } = Array.Empty<byte>();
    public string? RemoteEndPoint { get; set; }
}
