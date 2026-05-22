using SimuladorTCP.Models;

namespace SimuladorTCP.Services;

/// <summary>
/// Utilidades de conversión entre ASCII, HEX y bytes.
/// </summary>
public static class DataConverter
{
    /// <summary>
    /// Convierte una cadena HEX (bytes separados por espacios) a arreglo de bytes.
    /// Lanza FormatException si el formato es inválido.
    /// </summary>
    public static byte[] ParseHexString(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return Array.Empty<byte>();

        var parts = hex.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var bytes = new List<byte>(parts.Length);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length != 2)
                throw new FormatException($"Valor HEX inválido: '{trimmed}'. Debe tener 2 caracteres.");

            if (!byte.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber, null, out var b))
                throw new FormatException($"Valor HEX inválido: '{trimmed}'. No es un byte válido.");

            bytes.Add(b);
        }

        return bytes.ToArray();
    }

    /// <summary>
    /// Convierte un arreglo de bytes a cadena HEX con espacios.
    /// </summary>
    public static string BytesToHexString(byte[] data)
    {
        if (data == null || data.Length == 0) return string.Empty;
        return string.Join(" ", data.Select(b => b.ToString("X2")));
    }

    /// <summary>
    /// Convierte bytes a ASCII, reemplazando caracteres no imprimibles por '.' .
    /// </summary>
    public static string BytesToAsciiString(byte[] data)
    {
        if (data == null || data.Length == 0) return string.Empty;
        return new string(data.Select(b => b >= 0x20 && b <= 0x7E ? (char)b : '.').ToArray());
    }

    /// <summary>
    /// Obtiene el terminador seleccionado como bytes.
    /// </summary>
    public static byte[] GetTerminatorBytes(string terminator)
    {
        return terminator switch
        {
            "CR" => new byte[] { 0x0D },
            "LF" => new byte[] { 0x0A },
            "CRLF" => new byte[] { 0x0D, 0x0A },
            "STXETX" => new byte[] { 0x03 }, // ETX al final; STX se asume que el usuario lo incluye en el contenido si usa HEX
            _ => Array.Empty<byte>(),
        };
    }

    /// <summary>
    /// Formatea una entrada de log a texto legible.
    /// </summary>
    public static string FormatLogEntry(LogEntry entry)
    {
        var time = entry.Timestamp.ToString("HH:mm:ss.fff");
        var hex = BytesToHexString(entry.RawData);
        var ascii = BytesToAsciiString(entry.RawData);
        var remote = string.IsNullOrEmpty(entry.RemoteEndPoint) ? "" : $" [{entry.RemoteEndPoint}]";

        if (!string.IsNullOrEmpty(ascii) && ascii.Any(c => c != '.'))
            return $"[{time}] {entry.Direction}{remote} HEX: {hex}  ASCII: {ascii}";

        return $"[{time}] {entry.Direction}{remote} HEX: {hex}";
    }
}
