using System.Net;
using System.Net.Sockets;
using SimuladorTCP.Models;

namespace SimuladorTCP.Services;

/// <summary>
/// Servicio para modo TCP Client: conexión, envío y recepción asíncrona.
/// </summary>
public class TcpClientService : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private readonly object _lock = new();

    public bool IsConnected => _client?.Connected ?? false;

    public event EventHandler? Connected;
    public event EventHandler? Disconnected;
    public event EventHandler<byte[]>? DataReceived;
    public event EventHandler<string>? ErrorOccurred;

    public async Task ConnectAsync(string ip, int port)
    {
        Disconnect(); // limpiar estado previo

        try
        {
            _cts = new CancellationTokenSource();
            _client = new TcpClient();
            await _client.ConnectAsync(ip, port);
            _stream = _client.GetStream();

            Connected?.Invoke(this, EventArgs.Empty);

            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error de conexión: {ex.Message}");
            Disconnect();
        }
    }

    public void Disconnect()
    {
        lock (_lock)
        {
            try
            {
                _cts?.Cancel();
                _stream?.Close();
                _client?.Close();
            }
            catch { /* ignorar errores al cerrar */ }
            finally
            {
                _stream = null;
                _client = null;
                _cts = null;
            }
        }
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public async Task SendAsync(byte[] data)
    {
        if (_stream == null || !_client?.Connected == true)
        {
            ErrorOccurred?.Invoke(this, "No hay conexión activa.");
            return;
        }

        try
        {
            await _stream.WriteAsync(data.AsMemory(0, data.Length));
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error al enviar: {ex.Message}");
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        try
        {
            var buffer = new byte[4096];
            while (_stream != null && !token.IsCancellationRequested)
            {
                int read = await _stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
                if (read == 0)
                {
                    // El servidor cerró la conexión
                    break;
                }

                var data = new byte[read];
                Buffer.BlockCopy(buffer, 0, data, 0, read);
                DataReceived?.Invoke(this, data);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelación esperada
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error de lectura: {ex.Message}");
        }
        finally
        {
            // Si salimos del bucle, la conexión se perdió
            Disconnect();
        }
    }

    public void Dispose()
    {
        Disconnect();
    }
}
