namespace SimuladorTCP.UI;

/// <summary>
/// Pestaña de TCP Client con layout compacto tipo Hercules.
/// </summary>
public partial class TcpClientTab : UserControl
{
    private TextBox txtIp = null!;
    private NumericUpDown nudPort = null!;
    private Button btnConnect = null!;
    private Button btnPing = null!;
    private Label lblStatus = null!;
    private RadioButton rdoAscii = null!;
    private RadioButton rdoHex = null!;
    private ComboBox cboTerminator = null!;
    private TextBox txtSend = null!;
    private Button btnSend = null!;
    private TextBox txtLog = null!;

    public event EventHandler? ConnectClicked;
    public event EventHandler? DisconnectClicked;
    public event EventHandler? PingClicked;
    public event EventHandler<string>? SendManualClicked; // devuelve texto a enviar

    public string IpAddress => txtIp.Text;
    public int Port => (int)nudPort.Value;
    public bool IsHexMode => rdoHex.Checked;
    public string SelectedTerminator => cboTerminator.SelectedItem?.ToString() ?? "Ninguno";
    public string ManualText => txtSend.Text;

    public TcpClientTab()
    {
        InitializeLayout();
    }

    private void InitializeLayout()
    {
        this.Dock = DockStyle.Fill;

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 260,
            IsSplitterFixed = false,
            BackColor = SystemColors.Control
        };

        // --- Panel izquierdo: Configuración ---
        var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };

        var gbConfig = new GroupBox
        {
            Text = " Configuración TCP Client ",
            Dock = DockStyle.Top,
            Height = 160
        };

        var lblIp = new Label { Text = "IP del PLC:", Left = 10, Top = 24, Width = 70 };
        txtIp = new TextBox { Left = 85, Top = 22, Width = 150, Text = "192.168.1.100" };

        var lblPort = new Label { Text = "Puerto:", Left = 10, Top = 54, Width = 70 };
        nudPort = new NumericUpDown { Left = 85, Top = 52, Width = 80, Minimum = 1, Maximum = 65535, Value = 502 };

        btnConnect = new Button { Left = 10, Top = 88, Width = 110, Height = 28, Text = "Conectar" };
        btnConnect.Click += (s, e) =>
        {
            if (btnConnect.Text == "Conectar")
                ConnectClicked?.Invoke(this, EventArgs.Empty);
            else
                DisconnectClicked?.Invoke(this, EventArgs.Empty);
        };

        btnPing = new Button { Left = 125, Top = 88, Width = 110, Height = 28, Text = "Ping" };
        btnPing.Click += (s, e) => PingClicked?.Invoke(this, EventArgs.Empty);

        lblStatus = new Label { Left = 10, Top = 124, Width = 220, Text = "Estado: Desconectado", ForeColor = Color.Red };

        gbConfig.Controls.AddRange(new Control[] { lblIp, txtIp, lblPort, nudPort, btnConnect, btnPing, lblStatus });

        var gbSend = new GroupBox
        {
            Text = " Envío Manual ",
            Dock = DockStyle.Top,
            Top = gbConfig.Bottom + 6,
            Height = 180,
            Padding = new Padding(6)
        };

        rdoAscii = new RadioButton { Text = "ASCII", Left = 10, Top = 24, Width = 70, Checked = false };
        rdoHex = new RadioButton { Text = "HEX", Left = 90, Top = 24, Width = 70, Checked = true };

        var lblTerm = new Label { Text = "Terminador:", Left = 10, Top = 52, Width = 70 };
        cboTerminator = new ComboBox { Left = 85, Top = 50, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        cboTerminator.Items.AddRange(new object[] { "Ninguno", "CR", "LF", "CRLF", "STXETX" });
        cboTerminator.SelectedIndex = 0;

        var lblData = new Label { Text = "Datos:", Left = 10, Top = 82, Width = 70 };
        txtSend = new TextBox { Left = 10, Top = 102, Width = 230, Height = 40, Multiline = true, ScrollBars = ScrollBars.Vertical };

        btnSend = new Button { Left = 10, Top = 146, Width = 230, Height = 26, Text = "Enviar Manual" };
        btnSend.Click += (s, e) => SendManualClicked?.Invoke(this, txtSend.Text);

        gbSend.Controls.AddRange(new Control[] { rdoAscii, rdoHex, lblTerm, cboTerminator, lblData, txtSend, btnSend });

        leftPanel.Controls.Add(gbSend);
        leftPanel.Controls.Add(gbConfig);

        // --- Panel derecho: Log ---
        var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
        txtLog = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 10),
            BackColor = Color.WhiteSmoke,
            ForeColor = Color.Black
        };
        rightPanel.Controls.Add(txtLog);

        split.Panel1.Controls.Add(leftPanel);
        split.Panel2.Controls.Add(rightPanel);
        this.Controls.Add(split);
    }

    public void SetConnectedState(bool connected)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action<bool>(SetConnectedState), connected);
            return;
        }

        btnConnect.Text = connected ? "Desconectar" : "Conectar";
        lblStatus.Text = connected ? "Estado: Conectado" : "Estado: Desconectado";
        lblStatus.ForeColor = connected ? Color.Green : Color.Red;
        txtIp.Enabled = !connected;
        nudPort.Enabled = !connected;
    }

    public void AppendLog(string text)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action<string>(AppendLog), text);
            return;
        }

        txtLog.AppendText(text + Environment.NewLine);
        // Auto-scroll al final
        txtLog.SelectionStart = txtLog.Text.Length;
        txtLog.ScrollToCaret();
    }

    public void SetStatus(string message, Color color)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action<string, Color>(SetStatus), message, color);
            return;
        }
        lblStatus.Text = $"Estado: {message}";
        lblStatus.ForeColor = color;
    }

    public void LoadSettings(string ip, int port)
    {
        txtIp.Text = ip;
        nudPort.Value = port;
    }

    public void ClearLog()
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(ClearLog));
            return;
        }
        txtLog.Clear();
    }
}
