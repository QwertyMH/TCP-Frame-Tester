using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SimuladorTCP.Services;

namespace SimuladorTCP.UI;

public partial class TcpClientView : UserControl
{
    public event EventHandler? ConnectClicked;
    public event EventHandler? DisconnectClicked;
    public event EventHandler? PingClicked;
    public event EventHandler<string>? SendManualClicked;

    public string IpAddress => txtIp.Text;
    public int Port => int.TryParse(txtPort.Text, out var p) ? p : 502;
    public bool IsHexMode => rdoHex.IsChecked == true;
    public string SelectedTerminator => (cboTerminator.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Ninguno";
    public string ManualText => txtSend.Text;

    private bool _isPaused = false;
    private string _currentFilter = "All";
    private readonly List<LogEntryInternal> _allEntries = new();

    public TcpClientView()
    {
        InitializeComponent();
        SetFilter("All");
    }

    private void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        if (btnConnect.Content?.ToString() == "Conectar")
            ConnectClicked?.Invoke(this, EventArgs.Empty);
        else
            DisconnectClicked?.Invoke(this, EventArgs.Empty);
    }

    private void BtnPing_Click(object sender, RoutedEventArgs e)
    {
        PingClicked?.Invoke(this, EventArgs.Empty);
    }

    private void BtnSend_Click(object sender, RoutedEventArgs e)
    {
        SendManualClicked?.Invoke(this, txtSend.Text);
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

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn == null) return;
        SetFilter(btn.Name switch { "btnFilterAll" => "All", "btnFilterTx" => "TX", "btnFilterRx" => "RX", "btnFilterErrors" => "Errors", _ => "All" });
    }

    private void SetFilter(string filter)
    {
        _currentFilter = filter;
        ResetFilterStyle(btnFilterAll);
        ResetFilterStyle(btnFilterTx);
        ResetFilterStyle(btnFilterRx);
        ResetFilterStyle(btnFilterErrors);

        Button? active = filter switch { "All" => btnFilterAll, "TX" => btnFilterTx, "RX" => btnFilterRx, "Errors" => btnFilterErrors, _ => null };
        if (active != null) ActivateFilterStyle(active);
        RefreshLog();
    }

    private void ResetFilterStyle(Button btn)
    {
        btn.Background = (Brush)FindResource("SurfaceBrush");
        btn.Foreground = (Brush)FindResource("TextSecondaryBrush");
        btn.BorderBrush = (Brush)FindResource("BorderBrush");
    }

    private void ActivateFilterStyle(Button btn)
    {
        var color = btn.Name switch
        {
            "btnFilterAll" => (Brush)FindResource("AccentBrush"),
            "btnFilterTx" => (Brush)FindResource("TxBrush"),
            "btnFilterRx" => (Brush)FindResource("RxBrush"),
            "btnFilterErrors" => (Brush)FindResource("ErrorLogBrush"),
            _ => (Brush)FindResource("AccentBrush")
        };
        btn.Background = color;
        btn.Foreground = Brushes.White;
        btn.BorderBrush = color;
    }

    public void AppendLog(string text)
    {
        if (Dispatcher.CheckAccess())
        {
            if (_isPaused) return;
            var entry = ParseLogEntry(text);
            if (entry != null)
            {
                _allEntries.Add(entry);
                AddEntryToGrid(entry);
            }
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
        if (!ShouldShowEntry(entry)) return;
        var brush = entry.Direction switch { "TX" => (Brush)FindResource("TxBrush"), "RX" => (Brush)FindResource("RxBrush"), "ERROR" => (Brush)FindResource("ErrorLogBrush"), _ => (Brush)FindResource("TextPrimaryBrush") };
        entry.DirectionColor = brush;
        dgLog.Items.Add(entry);
        if (dgLog.Items.Count > 0)
            dgLog.ScrollIntoView(dgLog.Items[dgLog.Items.Count - 1]);
    }

    private bool ShouldShowEntry(LogEntryInternal entry) => _currentFilter switch { "TX" => entry.Direction == "TX", "RX" => entry.Direction == "RX", "Errors" => entry.Direction == "ERROR", _ => true };

    private void RefreshLog()
    {
        dgLog.Items.Clear();
        foreach (var e in _allEntries) AddEntryToGrid(e);
    }

    public void SetConnectedState(bool connected)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => SetConnectedState(connected)); return; }
        btnConnect.Content = connected ? "Desconectar" : "Conectar";
        btnConnect.Style = connected ? (Style)FindResource("DangerButton") : (Style)FindResource("PrimaryButton");
        lblStatus.Text = connected ? "Conectado" : "Desconectado";
        lblStatus.Foreground = connected ? (Brush)FindResource("SuccessBrush") : (Brush)FindResource("ErrorBrush");
        ledStatus.Fill = connected ? (Brush)FindResource("SuccessBrush") : (Brush)FindResource("ErrorBrush");
        txtIp.IsEnabled = !connected;
        txtPort.IsEnabled = !connected;
    }

    public void SetStatus(string message, Brush color)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => SetStatus(message, color)); return; }
        lblStatus.Text = message;
        lblStatus.Foreground = color;
    }

    public void LoadSettings(string ip, int port)
    {
        txtIp.Text = ip;
        txtPort.Text = port.ToString();
    }

    public void ClearLog()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(ClearLog); return; }
        dgLog.Items.Clear(); _allEntries.Clear();
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
