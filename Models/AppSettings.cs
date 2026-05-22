namespace SimuladorTCP.Models;

/// <summary>
/// Configuración global de la aplicación, serializable a JSON.
/// </summary>
public class AppSettings
{
    public string LastClientIp { get; set; } = "192.168.1.100";
    public int LastClientPort { get; set; } = 502;
    public int LastServerPort { get; set; } = 502;
    public string LastSendFormat { get; set; } = "HEX"; // ASCII o HEX
    public string LastTerminator { get; set; } = "Ninguno"; // Ninguno, CR, LF, CRLF, STXETX
    public List<Frame> Frames { get; set; } = new();
    public int MainWindowWidth { get; set; } = 1100;
    public int MainWindowHeight { get; set; } = 750;
}
