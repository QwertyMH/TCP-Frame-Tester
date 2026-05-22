using System.Net.NetworkInformation;
using System.Text;
using SimuladorTCP.Models;
using SimuladorTCP.Services;

namespace SimuladorTCP.UI;

/// <summary>
/// Ventana principal. Orquesta UI, servicios TCP y editor de tramas.
/// Estilo compacto inspirado en Hercules SETUP utility.
/// </summary>
public partial class MainForm : Form
{
    private readonly TcpClientService _clientService = new();
    private readonly TcpServerService _serverService = new();
    private readonly FrameManager _frameManager = new();
    private FrameEditor _frameEditor = null!;
    private TcpClientTab _clientTab = null!;
    private TcpServerTab _serverTab = null!;
    private TabControl _tabControl = null!;
    private AppSettings _settings = new();

    public MainForm()
    {
        this.Text = "Simulador TCP/IP - Pruebas PLC";
        this.Size = new Size(1100, 750);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = SystemColors.Control;

        InitializeMenu();
        InitializeLayout();
        WireEvents();
        LoadDefaultSettings();
    }

    private void InitializeMenu()
    {
        var menu = new MenuStrip();
        var archivo = new ToolStripMenuItem("Archivo");
        var guardar = new ToolStripMenuItem("Guardar configuración", null, OnSaveConfig);
        guardar.ShortcutKeys = Keys.Control | Keys.G;
        var cargar = new ToolStripMenuItem("Cargar configuración", null, OnLoadConfig);
        cargar.ShortcutKeys = Keys.Control | Keys.C;
        var salir = new ToolStripMenuItem("Salir", null, (s, e) => Application.Exit());
        archivo.DropDownItems.AddRange(new ToolStripItem[] { guardar, cargar, new ToolStripSeparator(), salir });
        menu.Items.Add(archivo);
        this.MainMenuStrip = menu;
        this.Controls.Add(menu);
    }

    private void InitializeLayout()
    {
        // Split horizontal: arriba tabs, abajo editor de tramas
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 350,
            IsSplitterFixed = false
        };

        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Appearance = TabAppearance.Normal
        };

        _clientTab = new TcpClientTab();
        _serverTab = new TcpServerTab();

        var tabClient = new TabPage("TCP Client") { BackColor = SystemColors.Control };
        tabClient.Controls.Add(_clientTab);

        var tabServer = new TabPage("TCP Server") { BackColor = SystemColors.Control };
        tabServer.Controls.Add(_serverTab);

        var tabAbout = new TabPage("Acerca de") { BackColor = SystemColors.Control };
        var lblAbout = new Label
        {
            Text = "Simulador TCP/IP para pruebas con PLCs\n\nVersión: 1.0.0\nDesarrollado en C# / WinForms",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 12, FontStyle.Regular)
        };
        tabAbout.Controls.Add(lblAbout);

        _tabControl.TabPages.Add(tabClient);
        _tabControl.TabPages.Add(tabServer);
        _tabControl.TabPages.Add(tabAbout);

        split.Panel1.Controls.Add(_tabControl);

        _frameEditor = new FrameEditor(_frameManager);
        split.Panel2.Controls.Add(_frameEditor);

        this.Controls.Add(split);
    }

    private void WireEvents()
    {
        // --- Client Events ---
        _clientTab.ConnectClicked += async (s, e) =>
        {
            await _clientService.ConnectAsync(_clientTab.IpAddress, _clientTab.Port);
        };
        _clientTab.DisconnectClicked += (s, e) => _clientService.Disconnect();
        _clientTab.PingClicked += async (s, e) => await DoPingAsync(_clientTab.IpAddress);
        _clientTab.SendManualClicked += async (s, text) =>
        {
            var data = ParseManualText(text, _clientTab.IsHexMode, _clientTab.SelectedTerminator);
            if (data != null)
            {
                await _clientService.SendAsync(data);
                LogClientTx(data);
            }
        };

        _clientService.Connected += (s, e) =>
        {
            _clientTab.SetConnectedState(true);
            _clientTab.AppendLog("[INFO] Conectado al servidor.");
        };
        _clientService.Disconnected += (s, e) =>
        {
            _clientTab.SetConnectedState(false);
            _clientTab.AppendLog("[INFO] Desconectado.");
        };
        _clientService.DataReceived += (s, data) => LogClientRx(data);
        _clientService.ErrorOccurred += (s, msg) => _clientTab.AppendLog($"[ERROR] {msg}");

        // --- Server Events ---
        _serverTab.StartClicked += async (s, e) => await _serverService.StartAsync(_serverTab.Port);
        _serverTab.StopClicked += (s, e) => _serverService.Stop();
        _serverTab.SendToAllClicked += async (s, text) =>
        {
            var data = ParseManualText(text, _serverTab.IsHexMode, _serverTab.SelectedTerminator);
            if (data != null)
            {
                await _serverService.SendToAllAsync(data);
                LogServerTx(data, null);
            }
        };
        _serverTab.SendToSelectedClicked += async (s, args) =>
        {
            var data = ParseManualText(args.Text, _serverTab.IsHexMode, _serverTab.SelectedTerminator);
            if (data != null)
            {
                await _serverService.SendToClientAsync(args.ClientId, data);
                var endpoint = _serverService.ConnectedClients.TryGetValue(args.ClientId, out var ep) ? ep : null;
                LogServerTx(data, endpoint);
            }
        };
        _serverTab.DisconnectClientClicked += (s, clientId) => _serverService.DisconnectClient(clientId);

        _serverService.Started += (s, e) => _serverTab.SetListeningState(true);
        _serverService.ClientConnected += (s, id) =>
        {
            _serverTab.AppendLog($"[INFO] Cliente conectado: {id}");
            RefreshServerClientList();
        };
        _serverService.ClientDisconnected += (s, id) =>
        {
            _serverTab.AppendLog($"[INFO] Cliente desconectado: {id}");
            RefreshServerClientList();
        };
        _serverService.DataReceived += (s, args) => LogServerRx(args.ClientId, args.Data);
        _serverService.ErrorOccurred += (s, msg) => _serverTab.AppendLog($"[ERROR] {msg}");
        _serverService.Stopped += (s, e) =>
        {
            _serverTab.SetListeningState(false);
            _serverTab.UpdateClientList(new Dictionary<Guid, string>());
        };

        // --- Frame Editor Events ---
        _frameEditor.SendFrameClicked += async (s, frame) => await SendFrameAsync(frame);
        _frameEditor.SendAllActiveClicked += async (s, frames) =>
        {
            foreach (var frame in frames)
            {
                await SendFrameAsync(frame);
                if (frame.DelayMs > 0)
                    await Task.Delay(frame.DelayMs);
            }
        };
        _frameEditor.ClearLogClicked += (s, e) =>
        {
            if (_tabControl.SelectedTab?.Text == "TCP Client")
                ClearLog(_clientTab);
            else if (_tabControl.SelectedTab?.Text == "TCP Server")
                ClearLog(_serverTab);
        };

        this.FormClosing += async (s, e) =>
        {
            _clientService.Dispose();
            _serverService.Dispose();
            await SaveAutoSettingsAsync();
        };
    }

    // --- Helpers de envío y log ---

    private async Task SendFrameAsync(Frame frame)
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

        var activeTab = _tabControl.SelectedTab?.Text;
        if (activeTab == "TCP Client" && _clientService.IsConnected)
        {
            await _clientService.SendAsync(data);
            LogClientTx(data);
        }
        else if (activeTab == "TCP Server" && _serverService.IsListening)
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
            {
                payload = DataConverter.ParseHexString(text);
            }
            else
            {
                payload = Encoding.ASCII.GetBytes(text);
            }

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
            ShowError($"Formato inválido: {ex.Message}");
            return null;
        }
    }

    private void LogClientTx(byte[] data)
    {
        var entry = new LogEntry { Direction = "TX", RawData = data };
        _clientTab.AppendLog(DataConverter.FormatLogEntry(entry));
    }

    private void LogClientRx(byte[] data)
    {
        var entry = new LogEntry { Direction = "RX", RawData = data };
        _clientTab.AppendLog(DataConverter.FormatLogEntry(entry));
    }

    private void LogServerTx(byte[] data, string? endpoint)
    {
        var entry = new LogEntry { Direction = "TX", RawData = data, RemoteEndPoint = endpoint };
        _serverTab.AppendLog(DataConverter.FormatLogEntry(entry));
    }

    private void LogServerRx(Guid clientId, byte[] data)
    {
        var endpoint = _serverService.ConnectedClients.TryGetValue(clientId, out var ep) ? ep : clientId.ToString();
        var entry = new LogEntry { Direction = "RX", RawData = data, RemoteEndPoint = endpoint };
        _serverTab.AppendLog(DataConverter.FormatLogEntry(entry));
    }

    private void RefreshServerClientList()
    {
        _serverTab.UpdateClientList(_serverService.ConnectedClients);
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
            _clientTab.AppendLog(msg);
        }
        catch (Exception ex)
        {
            _clientTab.AppendLog($"[ERROR] Ping: {ex.Message}");
        }
    }

    private void ClearLog(Control tab)
    {
        if (tab is TcpClientTab ct) ct.ClearLog();
        else if (tab is TcpServerTab st) st.ClearLog();
    }

    // --- Configuración JSON ---

    private void LoadDefaultSettings()
    {
        // Intentar cargar desde un archivo por defecto en la carpeta de la app
        var defaultPath = Path.Combine(Application.StartupPath, "default_config.json");
        if (File.Exists(defaultPath))
        {
            _ = LoadConfigAsync(defaultPath);
        }
    }

    private async void OnSaveConfig(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = "config_plc.json"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            await SaveConfigAsync(dlg.FileName);
        }
    }

    private async void OnLoadConfig(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            await LoadConfigAsync(dlg.FileName);
        }
    }

    private async Task SaveConfigAsync(string path)
    {
        try
        {
            var settings = new AppSettings
            {
                LastClientIp = _clientTab.IpAddress,
                LastClientPort = _clientTab.Port,
                LastServerPort = _serverTab.Port,
                LastSendFormat = _clientTab.IsHexMode ? "HEX" : "ASCII",
                LastTerminator = _clientTab.SelectedTerminator,
                Frames = _frameManager.Frames.ToList(),
                MainWindowWidth = this.Width,
                MainWindowHeight = this.Height
            };
            await JsonConfigService.SaveAsync(path, settings);
            MessageBox.Show("Configuración guardada correctamente.", "Guardar", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            _clientTab.LoadSettings(settings.LastClientIp, settings.LastClientPort);
            _serverTab.LoadSettings(settings.LastServerPort);
            _frameManager.LoadFrames(settings.Frames);
            this.Size = new Size(settings.MainWindowWidth, settings.MainWindowHeight);
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
            var path = Path.Combine(Application.StartupPath, "default_config.json");
            var settings = new AppSettings
            {
                LastClientIp = _clientTab.IpAddress,
                LastClientPort = _clientTab.Port,
                LastServerPort = _serverTab.Port,
                Frames = _frameManager.Frames.ToList(),
                MainWindowWidth = this.Width,
                MainWindowHeight = this.Height
            };
            await JsonConfigService.SaveAsync(path, settings);
        }
        catch { /* ignorar errores al guardar automáticamente */ }
    }

    private void ShowError(string message)
    {
        MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
