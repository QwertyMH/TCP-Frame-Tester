using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using SimuladorTCP.Models;

namespace SimuladorTCP.Services;

/// <summary>
/// Servicio para modo TCP Server: escucha, múltiples clientes, envío y recepción.
/// </summary>
public class TcpServerService : IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<Guid, ClientSession> _clients = new();
    private readonly object _lock = new();

    public bool IsListening => _listener != null;
    public int ClientCount => _clients.Count;
    public IReadOnlyDictionary<Guid, string> ConnectedClients =>
        _clients.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.EndPointText);

    public event EventHandler? Started;
    public event EventHandler<Guid>? ClientConnected;
    public event EventHandler<Guid>? ClientDisconnected;
    public event EventHandler<(Guid ClientId, byte[] Data)>? DataReceived;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler? Stopped;

    public async Task StartAsync(int port)
    {
        Stop(); // limpiar previo

        try
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            Started?.Invoke(this, EventArgs.Empty);

            _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error al iniciar servidor: {ex.Message}");
            Stop();
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
            }
            catch { }
            finally
            {
                _listener = null;
                _cts = null;
            }
        }

        // Cerrar todos los clientes
        foreach (var kvp in _clients)
        {
            kvp.Value.Close();
        }
        _clients.Clear();

        Stopped?.Invoke(this, EventArgs.Empty);
    }

    public async Task SendToClientAsync(Guid clientId, byte[] data)
    {
        if (_clients.TryGetValue(clientId, out var session))
        {
            await session.SendAsync(data);
        }
        else
        {
            ErrorOccurred?.Invoke(this, $"Cliente {clientId} no encontrado.");
        }
    }

    public async Task SendToAllAsync(byte[] data)
    {
        foreach (var kvp in _clients)
        {
            await kvp.Value.SendAsync(data);
        }
    }

    public void DisconnectClient(Guid clientId)
    {
        if (_clients.TryRemove(clientId, out var session))
        {
            session.Close();
            ClientDisconnected?.Invoke(this, clientId);
        }
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        try
        {
            while (_listener != null && !token.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(token);
                var session = new ClientSession(client);
                _clients[session.Id] = session;

                ClientConnected?.Invoke(this, session.Id);

                _ = Task.Run(() => HandleClientAsync(session, token), token);
            }
        }
        catch (OperationCanceledException)
        {
            // Esperado
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error en bucle de aceptación: {ex.Message}");
        }
    }

    private async Task HandleClientAsync(ClientSession session, CancellationToken token)
    {
        try
        {
            var buffer = new byte[4096];
            while (!token.IsCancellationRequested && session.IsConnected)
            {
                int read = await session.Stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
                if (read == 0) break;

                var data = new byte[read];
                Buffer.BlockCopy(buffer, 0, data, 0, read);
                DataReceived?.Invoke(this, (session.Id, data));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error cliente {session.EndPointText}: {ex.Message}");
        }
        finally
        {
            if (_clients.TryRemove(session.Id, out _))
            {
                session.Close();
                ClientDisconnected?.Invoke(this, session.Id);
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }

    /// <summary>
    /// Sesión interna de un cliente conectado al servidor.
    /// </summary>
    private class ClientSession
    {
        public Guid Id { get; } = Guid.NewGuid();
        public TcpClient Client { get; }
        public NetworkStream Stream { get; }
        public string EndPointText { get; }
        public bool IsConnected => Client.Connected;

        public ClientSession(TcpClient client)
        {
            Client = client;
            Stream = client.GetStream();
            EndPointText = client.Client.RemoteEndPoint?.ToString() ?? "Desconocido";
        }

        public async Task SendAsync(byte[] data)
        {
            if (Client.Connected)
            {
                await Stream.WriteAsync(data.AsMemory(0, data.Length));
            }
        }

        public void Close()
        {
            try { Stream.Close(); } catch { }
            try { Client.Close(); } catch { }
        }
    }
}
