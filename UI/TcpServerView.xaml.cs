using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SimuladorTCP.Services;

namespace SimuladorTCP.UI;

public partial class TcpServerView : UserControl
{
    public event EventHandler? StartClicked;
    public event EventHandler? StopClicked;
    public event EventHandler<string>? SendToAllClicked;
    public event EventHandler<(Guid ClientId, string Text)>? SendToSelectedClicked;
    public event EventHandler<Guid>? DisconnectClientClicked;

    public int Port => int.TryParse(txtPort.Text, out var p) ? p : 502;
    public Guid? SelectedClientId => GetSelectedClientId();
    public bool IsHexMode => rdoHex.IsChecked == true;
    public string SelectedTerminator => (cboTerminator.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Ninguno";
    public string ManualText => txtSend.Text;

    private readonly Dictionary<string, Guid> _clientMap = new();
    private bool _isPaused = false;
    private readonly List<LogEntryInternal> _allEntries = new();

    public TcpServerView()
    {
        InitializeComponent();
    }

    private void BtnListen_Click(object sender, RoutedEventArgs e)
    {
        if (btnListen.Content?.ToString() == "Escuchar")
            StartClicked?.Invoke(this, EventArgs.Empty);
        else
            StopClicked?.Invoke(this, EventArgs.Empty);
    }

    private void BtnSendSelected_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedClientId.HasValue)
            SendToSelectedClicked?.Invoke(this, (SelectedClientId.Value, txtSend.Text));
    }

    private void BtnSendAll_Click(object sender, RoutedEventArgs e)
    {
        SendToAllClicked?.Invoke(this, txtSend.Text);
    }

    private void BtnDisconnectClient_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedClientId.HasValue)
            DisconnectClientClicked?.Invoke(this, SelectedClientId.Value);
    }

    private void BtnPause_Click(object sender, RoutedEventArgs e)
    {
        _isPaused = !_isPaused;
        btnPause.Content = _isPaused ? "Reanudar" : "Pausar";
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        ClearLog();
    }

    public void SetListeningState(bool listening)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => SetListeningState(listening)); return; }
        btnListen.Content = listening ? "Detener" : "Escuchar";
        btnListen.Style = listening ? (Style)FindResource("DangerButton") : (Style)FindResource("PrimaryButton");
        lblStatus.Text = listening ? "Escuchando" : "Detenido";
        lblStatus.Foreground = listening ? (Brush)FindResource("SuccessBrush") : (Brush)FindResource("ErrorBrush");
        ledStatus.Fill = listening ? (Brush)FindResource("SuccessBrush") : (Brush)FindResource("ErrorBrush");
        txtPort.IsEnabled = !listening;
    }

    public void UpdateClientList(IReadOnlyDictionary<Guid, string> clients)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => UpdateClientList(clients)); return; }
        _clientMap.Clear();
        lstClients.Items.Clear();
        foreach (var kvp in clients)
        {
            _clientMap[kvp.Value] = kvp.Key;
            lstClients.Items.Add(kvp.Value);
        }
        lblClientCount.Text = $"Clientes: {clients.Count}";
    }

    public void AppendLog(string text)
    {
        if (Dispatcher.CheckAccess())
        {
            if (_isPaused) return;
            var entry = ParseLogEntry(text);
            if (entry != null) { _allEntries.Add(entry); AddEntryToGrid(entry); }
        }
        else
        {
            Dispatcher.Invoke(() => AppendLog(text));
        }
    }

    private LogEntryInternal? ParseLogEntry(string text)
    {
        try
        {
            var entry = new LogEntryInternal();
            if (text.Contains("[ERROR]")) { entry.Direction = "ERROR"; entry.Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"); entry.HexData = text; return entry; }
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return null;
            entry.Timestamp = parts[0].Trim('[', ']');
            entry.Direction = parts[1];
            if (parts.Length > 3 && parts[2] == "HEX:")
            {
                entry.HexData = string.Join(" ", parts.Skip(3));
                var bytes = DataConverter.ParseHexString(entry.HexData);
                entry.ByteCount = bytes.Length;
                entry.AsciiData = DataConverter.BytesToAsciiString(bytes);
            }
            else { entry.HexData = string.Join(" ", parts.Skip(2)); entry.AsciiData = entry.HexData; entry.ByteCount = entry.AsciiData.Length; }
            return entry;
        }
        catch { return new LogEntryInternal { Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"), Direction = "INFO", HexData = text, AsciiData = text, ByteCount = 0 }; }
    }

    private void AddEntryToGrid(LogEntryInternal entry)
    {
        var brush = entry.Direction switch { "TX" => (Brush)FindResource("TxBrush"), "RX" => (Brush)FindResource("RxBrush"), "ERROR" => (Brush)FindResource("ErrorLogBrush"), _ => (Brush)FindResource("TextPrimaryBrush") };
        entry.DirectionColor = brush;
        dgLog.Items.Add(entry);
        if (dgLog.Items.Count > 0)
            dgLog.ScrollIntoView(dgLog.Items[dgLog.Items.Count - 1]);
    }

    public void LoadSettings(int port)
    {
        txtPort.Text = port.ToString();
    }

    public void ClearLog()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(ClearLog); return; }
        dgLog.Items.Clear(); _allEntries.Clear();
    }

    private Guid? GetSelectedClientId()
    {
        if (lstClients.SelectedItem == null) return null;
        var text = lstClients.SelectedItem.ToString();
        return text != null && _clientMap.TryGetValue(text, out var id) ? id : null;
    }

    public class LogEntryInternal
    {
        public string Timestamp { get; set; } = "";
        public string Direction { get; set; } = "TX";
        public int ByteCount { get; set; } = 0;
        public string HexData { get; set; } = "";
        public string AsciiData { get; set; } = "";
        public Brush DirectionColor { get; set; } = Brushes.White;
    }
}
