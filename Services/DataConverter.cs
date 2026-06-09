using SimuladorTCP.Models;

namespace SimuladorTCP.Services;

/// <summary>
/// Utilidades de conversión entre ASCII, HEX y bytes.
/// </summary>
public static class DataConverter
{
    /// <summary>
    /// Convierte una cadena HEX a arreglo de bytes.
    /// Acepta bytes separados por espacios (ej. "0A 0B 0C") o contiguos (ej. "0A0B0C").
    /// Lanza FormatException si el formato es inválido.
    /// </summary>
    public static byte[] ParseHexString(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return Array.Empty<byte>();

        // Eliminar todos los espacios y guiones para permitir "0A 0B 0C" o "0A-0B-0C" o "0A0B0C"
        var cleaned = hex.Replace(" ", "").Replace("-", "");

        if (cleaned.Length % 2 != 0)
            throw new FormatException("Cadena HEX inválida: la longitud debe ser par (cada byte tiene 2 caracteres hex).");

        var bytes = new List<byte>(cleaned.Length / 2);

        for (int i = 0; i < cleaned.Length; i += 2)
        {
            var byteStr = cleaned.Substring(i, 2);
            if (!byte.TryParse(byteStr, System.Globalization.NumberStyles.HexNumber, null, out var b))
                throw new FormatException($"Valor HEX inválido: '{byteStr}'. No es un byte válido.");

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
