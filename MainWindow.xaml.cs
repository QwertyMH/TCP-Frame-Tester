using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SimuladorTCP.Models;
using SimuladorTCP.Services;
using SimuladorTCP.UI;

namespace SimuladorTCP;

public partial class MainWindow : Window
{
    private readonly TcpClientService _clientService = new();
    private readonly TcpServerService _serverService = new();
    private readonly FrameManager _frameManager = new();
    private AppSettings _settings = new();

    public MainWindow()
    {
        InitializeComponent();

        FrameEditor.FrameManager = _frameManager;
        WireEvents();
        LoadDefaultSettings();
    }

    private void WireEvents()
    {
        // --- Client Events ---
        ClientView.ConnectClicked += async (s, e) =>
        {
            await _clientService.ConnectAsync(ClientView.IpAddress, ClientView.Port);
        };
        ClientView.DisconnectClicked += (s, e) => _clientService.Disconnect();
        ClientView.PingClicked += async (s, e) => await DoPingAsync(ClientView.IpAddress);
        ClientView.SendManualClicked += async (s, text) =>
        {
            var data = ParseManualText(text, ClientView.IsHexMode, ClientView.SelectedTerminator);
            if (data != null)
            {
                await _clientService.SendAsync(data);
                LogClientTx(data);
            }
        };

        _clientService.Connected += (s, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                ClientView.SetConnectedState(true);
                ClientView.AppendLog("[INFO] Conectado al servidor.");
            });
        };
        _clientService.Disconnected += (s, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                ClientView.SetConnectedState(false);
                ClientView.AppendLog("[INFO] Desconectado.");
            });
        };
        _clientService.DataReceived += (s, data) =>
        {
            Dispatcher.Invoke(() => LogClientRx(data));
        };
        _clientService.ErrorOccurred += (s, msg) =>
        {
            Dispatcher.Invoke(() => ClientView.AppendLog($"[ERROR] {msg}"));
        };

        // --- Server Events ---
        ServerView.StartClicked += async (s, e) => await _serverService.StartAsync(ServerView.Port);
        ServerView.StopClicked += (s, e) => _serverService.Stop();
        ServerView.SendToAllClicked += async (s, text) =>
        {
            var data = ParseManualText(text, ServerView.IsHexMode, ServerView.SelectedTerminator);
            if (data != null)
            {
                await _serverService.SendToAllAsync(data);
                LogServerTx(data, null);
            }
        };
        ServerView.SendToSelectedClicked += async (s, args) =>
        {
            var data = ParseManualText(args.Text, ServerView.IsHexMode, ServerView.SelectedTerminator);
            if (data != null)
            {
                await _serverService.SendToClientAsync(args.ClientId, data);
                var endpoint = _serverService.ConnectedClients.TryGetValue(args.ClientId, out var ep) ? ep : null;
                LogServerTx(data, endpoint);
            }
        };
        ServerView.DisconnectClientClicked += (s, clientId) => _serverService.DisconnectClient(clientId);

        _serverService.Started += (s, e) =>
        {
            Dispatcher.Invoke(() => ServerView.SetListeningState(true));
        };
        _serverService.ClientConnected += (s, id) =>
        {
            Dispatcher.Invoke(() =>
            {
                ServerView.AppendLog($"[INFO] Cliente conectado: {id}");
                RefreshServerClientList();
            });
        };
        _serverService.ClientDisconnected += (s, id) =>
        {
            Dispatcher.Invoke(() =>
            {
                ServerView.AppendLog($"[INFO] Cliente desconectado: {id}");
                RefreshServerClientList();
            });
        };
        _serverService.DataReceived += (s, args) =>
        {
            Dispatcher.Invoke(() => LogServerRx(args.ClientId, args.Data));
        };
        _serverService.ErrorOccurred += (s, msg) =>
        {
            Dispatcher.Invoke(() => ServerView.AppendLog($"[ERROR] {msg}"));
        };
        _serverService.Stopped += (s, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                ServerView.SetListeningState(false);
                ServerView.UpdateClientList(new Dictionary<Guid, string>());
            });
        };

        // --- Frame Editor Events ---
        FrameEditor.SendFrameClicked += async (s, frame) => await SendFrameAsync(frame);
        FrameEditor.SendAllActiveClicked += async (s, frames) =>
        {
            foreach (var frame in frames)
            {
                await SendFrameAsync(frame);
                if (frame.DelayMs > 0)
                    await Task.Delay(frame.DelayMs);
            }
        };
        FrameEditor.ClearLogClicked += (s, e) =>
        {
            var tab = ((TabControl)ClientView.Parent).SelectedItem as TabItem;
            if (tab?.Header?.ToString() == "TCP Client")
                ClientView.ClearLog();
            else if (tab?.Header?.ToString() == "TCP Server")
                ServerView.ClearLog();
        };

        this.Closing += async (s, e) =>
        {
            _clientService.Dispose();
            _serverService.Dispose();
            await SaveAutoSettingsAsync();
        };
    }

    // --- Helpers ---

    private async Task SendFrameAsync(SimuladorTCP.Models.Frame frame)
    {
        byte[]? data = null;
        try
        {
            data = frame.Format.ToUpperInvariant() switch
            {
                "HEX" => DataConverter.ParseHexString(frame.Content),
                "ASCII" => Encoding.ASCII.GetBytes(frame.Content),
                _ => Encoding.ASCII.GetBytes(frame.Content)
            };
        }
        catch (FormatException ex)
        {
            ShowError($"Trama '{frame.Name}': {ex.Message}");
            return;
        }

        if (data == null) return;

        var tab = ((TabControl)ClientView.Parent).SelectedItem as TabItem;
        if (tab?.Header?.ToString() == "TCP Client" && _clientService.IsConnected)
        {
            await _clientService.SendAsync(data);
            LogClientTx(data);
        }
        else if (tab?.Header?.ToString() == "TCP Server" && _serverService.IsListening)
        {
            await _serverService.SendToAllAsync(data);
            LogServerTx(data, null);
        }
    }

    private byte[]? ParseManualText(string text, bool isHex, string terminator)
    {
        try
        {
            byte[] payload;
            if (isHex)
                payload = DataConverter.ParseHexString(text);
            else
                payload = Encoding.ASCII.GetBytes(text);

            var term = DataConverter.GetTerminatorBytes(terminator);
            if (term.Length > 0)
            {
                var combined = new byte[payload.Length + term.Length];
                Buffer.BlockCopy(payload, 0, combined, 0, payload.Length);
                Buffer.BlockCopy(term, 0, combined, payload.Length, term.Length);
                payload = combined;
            }
            return payload;
        }
        catch (FormatException ex)
        {
            ShowError($"Formato invalido: {ex.Message}");
            return null;
        }
    }

    private void LogClientTx(byte[] data)
    {
        var entry = new LogEntry { Direction = "TX", RawData = data };
        ClientView.AppendLog(DataConverter.FormatLogEntry(entry));
    }

    private void LogClientRx(byte[] data)
    {
        var entry = new LogEntry { Direction = "RX", RawData = data };
        ClientView.AppendLog(DataConverter.FormatLogEntry(entry));
    }

    private void LogServerTx(byte[] data, string? endpoint)
    {
        var entry = new LogEntry { Direction = "TX", RawData = data, RemoteEndPoint = endpoint };
        ServerView.AppendLog(DataConverter.FormatLogEntry(entry));
    }

    private void LogServerRx(Guid clientId, byte[] data)
    {
        var endpoint = _serverService.ConnectedClients.TryGetValue(clientId, out var ep) ? ep : clientId.ToString();
        var entry = new LogEntry { Direction = "RX", RawData = data, RemoteEndPoint = endpoint };
        ServerView.AppendLog(DataConverter.FormatLogEntry(entry));
    }

    private void RefreshServerClientList()
    {
        ServerView.UpdateClientList(_serverService.ConnectedClients);
    }

    private async Task DoPingAsync(string ip)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ip, 2000);
            var msg = reply.Status == IPStatus.Success
                ? $"[INFO] Ping a {ip}: {reply.RoundtripTime} ms"
                : $"[INFO] Ping a {ip}: {reply.Status}";
            ClientView.AppendLog(msg);
        }
        catch (Exception ex)
        {
            ClientView.AppendLog($"[ERROR] Ping: {ex.Message}");
        }
    }

    // --- Config ---

    private void LoadDefaultSettings()
    {
        var defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "default_config.json");
        if (File.Exists(defaultPath))
            _ = LoadConfigAsync(defaultPath);
    }

    private async void OnSaveConfig(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = "config_plc.json"
        };
        if (dlg.ShowDialog() == true)
            await SaveConfigAsync(dlg.FileName);
    }

    private async void OnLoadConfig(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
            await LoadConfigAsync(dlg.FileName);
    }

    private void OnExit(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private async Task SaveConfigAsync(string path)
    {
        try
        {
            var settings = new AppSettings
            {
                LastClientIp = ClientView.IpAddress,
                LastClientPort = ClientView.Port,
                LastServerPort = ServerView.Port,
                LastSendFormat = ClientView.IsHexMode ? "HEX" : "ASCII",
                LastTerminator = ClientView.SelectedTerminator,
                Frames = _frameManager.Frames.ToList(),
                MainWindowWidth = (int)this.Width,
                MainWindowHeight = (int)this.Height
            };
            await JsonConfigService.SaveAsync(path, settings);
            MessageBox.Show("Configuracion guardada correctamente.", "Guardar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError($"Error al guardar: {ex.Message}");
        }
    }

    private async Task LoadConfigAsync(string path)
    {
        try
        {
            var settings = await JsonConfigService.LoadAsync(path);
            _settings = settings;
            ClientView.LoadSettings(settings.LastClientIp, settings.LastClientPort);
            ServerView.LoadSettings(settings.LastServerPort);
            _frameManager.LoadFrames(settings.Frames);
            this.Width = settings.MainWindowWidth;
            this.Height = settings.MainWindowHeight;
        }
        catch (Exception ex)
        {
            ShowError($"Error al cargar: {ex.Message}");
        }
    }

    private async Task SaveAutoSettingsAsync()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "default_config.json");
            var settings = new AppSettings
            {
                LastClientIp = ClientView.IpAddress,
                LastClientPort = ClientView.Port,
                LastServerPort = ServerView.Port,
                Frames = _frameManager.Frames.ToList(),
                MainWindowWidth = (int)this.Width,
                MainWindowHeight = (int)this.Height
            };
            await JsonConfigService.SaveAsync(path, settings);
        }
        catch { }
    }

    private void ShowError(string message)
    {
        MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
