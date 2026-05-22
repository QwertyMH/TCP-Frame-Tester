namespace SimuladorTCP.UI;

/// <summary>
/// Pestaña de TCP Server con log, lista de clientes y controles de envío manual.
/// </summary>
public partial class TcpServerTab : UserControl
{
    private NumericUpDown nudPort = null!;
    private Button btnListen = null!;
    private Label lblStatus = null!;
    private Label lblClientCount = null!;
    private ListBox lstClients = null!;
    private Button btnSendAll = null!;
    private Button btnSendSelected = null!;
    private Button btnDisconnectClient = null!;
    private TextBox txtLog = null!;
    private TextBox txtSend = null!;
    private RadioButton rdoAscii = null!;
    private RadioButton rdoHex = null!;
    private ComboBox cboTerminator = null!;
    private readonly Dictionary<string, Guid> _clientMap = new();

    public event EventHandler? StartClicked;
    public event EventHandler? StopClicked;
    public event EventHandler<string>? SendToAllClicked; // texto a enviar
    public event EventHandler<(Guid ClientId, string Text)>? SendToSelectedClicked;
    public event EventHandler<Guid>? DisconnectClientClicked;

    public int Port => (int)nudPort.Value;
    public Guid? SelectedClientId => GetSelectedClientId();
    public bool IsHexMode => rdoHex.Checked;
    public string SelectedTerminator => cboTerminator.SelectedItem?.ToString() ?? "Ninguno";
    public string ManualText => txtSend.Text;

    public TcpServerTab()
    {
        InitializeLayout();
    }

    private void InitializeLayout()
    {
        this.Dock = DockStyle.Fill;

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 260
        };

        // --- Izquierda ---
        var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };

        // Configuración
        var gbConfig = new GroupBox
        {
            Text = " Configuración TCP Server ",
            Dock = DockStyle.Top,
            Height = 110
        };

        var lblPort = new Label { Text = "Puerto local:", Left = 10, Top = 24, Width = 80 };
        nudPort = new NumericUpDown { Left = 95, Top = 22, Width = 80, Minimum = 1, Maximum = 65535, Value = 502 };

        btnListen = new Button { Left = 10, Top = 54, Width = 110, Height = 28, Text = "Escuchar" };
        btnListen.Click += (s, e) =>
        {
            if (btnListen.Text == "Escuchar")
                StartClicked?.Invoke(this, EventArgs.Empty);
            else
                StopClicked?.Invoke(this, EventArgs.Empty);
        };

        lblStatus = new Label { Left = 125, Top = 58, Width = 120, Text = "Detenido", ForeColor = Color.Red };
        lblClientCount = new Label { Left = 10, Top = 86, Width = 220, Text = "Clientes: 0" };

        gbConfig.Controls.AddRange(new Control[] { lblPort, nudPort, btnListen, lblStatus, lblClientCount });

        // Envío Manual
        var gbSend = new GroupBox
        {
            Text = " Envío Manual ",
            Dock = DockStyle.Bottom,
            Height = 170
        };

        rdoAscii = new RadioButton { Text = "ASCII", Left = 10, Top = 20, Width = 70, Checked = false };
        rdoHex = new RadioButton { Text = "HEX", Left = 90, Top = 20, Width = 70, Checked = true };

        var lblTerm = new Label { Text = "Terminador:", Left = 10, Top = 48, Width = 70 };
        cboTerminator = new ComboBox { Left = 85, Top = 46, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        cboTerminator.Items.AddRange(new object[] { "Ninguno", "CR", "LF", "CRLF", "STXETX" });
        cboTerminator.SelectedIndex = 0;

        txtSend = new TextBox { Left = 10, Top = 76, Width = 230, Height = 40, Multiline = true, ScrollBars = ScrollBars.Vertical };

        var btnSendManual = new Button { Left = 10, Top = 120, Width = 230, Height = 24, Text = "Enviar manual a seleccionado" };
        btnSendManual.Click += (s, e) =>
        {
            if (SelectedClientId.HasValue)
                SendToSelectedClicked?.Invoke(this, (SelectedClientId.Value, txtSend.Text));
        };

        gbSend.Controls.AddRange(new Control[] { rdoAscii, rdoHex, lblTerm, cboTerminator, txtSend, btnSendManual });

        // Clientes
        var gbClients = new GroupBox
        {
            Text = " Clientes Conectados ",
            Dock = DockStyle.Fill,
            Padding = new Padding(6)
        };

        lstClients = new ListBox
        {
            Dock = DockStyle.Top,
            Height = 120,
            Font = new Font("Consolas", 9)
        };

        var pnlButtons = new Panel { Dock = DockStyle.Bottom, Height = 90 };
        btnSendSelected = new Button { Left = 10, Top = 4, Width = 220, Height = 24, Text = "Enviar a seleccionado" };
        btnSendSelected.Click += (s, e) =>
        {
            if (SelectedClientId.HasValue)
                SendToSelectedClicked?.Invoke(this, (SelectedClientId.Value, txtSend.Text));
        };

        btnSendAll = new Button { Left = 10, Top = 32, Width = 220, Height = 24, Text = "Enviar a todos" };
        btnSendAll.Click += (s, e) => SendToAllClicked?.Invoke(this, txtSend.Text);

        btnDisconnectClient = new Button { Left = 10, Top = 60, Width = 220, Height = 24, Text = "Desconectar cliente" };
        btnDisconnectClient.Click += (s, e) =>
        {
            if (SelectedClientId.HasValue)
                DisconnectClientClicked?.Invoke(this, SelectedClientId.Value);
        };

        pnlButtons.Controls.AddRange(new Control[] { btnSendSelected, btnSendAll, btnDisconnectClient });
        gbClients.Controls.Add(pnlButtons);
        gbClients.Controls.Add(lstClients);

        leftPanel.Controls.Add(gbClients);
        leftPanel.Controls.Add(gbSend);
        leftPanel.Controls.Add(gbConfig);

        // --- Derecha: Log ---
        var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
        txtLog = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 10),
            BackColor = Color.WhiteSmoke
        };
        rightPanel.Controls.Add(txtLog);

        split.Panel1.Controls.Add(leftPanel);
        split.Panel2.Controls.Add(rightPanel);
        this.Controls.Add(split);
    }

    private Guid? GetSelectedClientId()
    {
        if (lstClients.SelectedItem == null) return null;
        var text = lstClients.SelectedItem.ToString();
        return text != null && _clientMap.TryGetValue(text, out var id) ? id : null;
    }

    public void SetListeningState(bool listening)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action<bool>(SetListeningState), listening);
            return;
        }

        btnListen.Text = listening ? "Detener" : "Escuchar";
        lblStatus.Text = listening ? "Escuchando" : "Detenido";
        lblStatus.ForeColor = listening ? Color.Green : Color.Red;
        nudPort.Enabled = !listening;
    }

    public void UpdateClientList(IReadOnlyDictionary<Guid, string> clients)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action<IReadOnlyDictionary<Guid, string>>(UpdateClientList), clients);
            return;
        }

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
        if (this.InvokeRequired)
        {
            this.Invoke(new Action<string>(AppendLog), text);
            return;
        }

        txtLog.AppendText(text + Environment.NewLine);
        txtLog.SelectionStart = txtLog.Text.Length;
        txtLog.ScrollToCaret();
    }

    public void LoadSettings(int port)
    {
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
