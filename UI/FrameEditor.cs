using SimuladorTCP.Models;
using SimuladorTCP.Services;

namespace SimuladorTCP.UI;

/// <summary>
/// Control de edición de tramas con DataGridView y botones de envío masivo.
/// </summary>
public partial class FrameEditor : UserControl
{
    private DataGridView dgvFrames = null!;
    private BindingSource bsFrames = null!;
    private Button btnAdd = null!;
    private Button btnDelete = null!;
    private Button btnSendSelected = null!;
    private Button btnSendAllActive = null!;
    private Button btnSendLoop = null!;
    private Button btnStopLoop = null!;
    private Button btnClearLog = null!;
    private Label lblStatus = null!;

    private CancellationTokenSource? _loopCts;

    public FrameManager FrameManager { get; }

    public event EventHandler<Frame>? SendFrameClicked; // Envía una trama individual
    public event EventHandler<List<Frame>>? SendAllActiveClicked; // Envía todas las activas
    public event EventHandler? ClearLogClicked;

    public FrameEditor(FrameManager frameManager)
    {
        FrameManager = frameManager;
        InitializeLayout();
        BindData();
    }

    private void InitializeLayout()
    {
        this.Dock = DockStyle.Fill;
        this.Padding = new Padding(6);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        // DataGridView
        dgvFrames = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.WhiteSmoke,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Segoe UI", 9)
        };

        dgvFrames.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "Number",
            HeaderText = "Nº",
            Width = 40,
            ReadOnly = true
        });
        dgvFrames.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "Name",
            HeaderText = "Nombre",
            Width = 160
        });
        dgvFrames.Columns.Add(new DataGridViewComboBoxColumn
        {
            DataPropertyName = "Format",
            HeaderText = "Formato",
            Width = 70,
            DataSource = new[] { "ASCII", "HEX" }
        });
        dgvFrames.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "Content",
            HeaderText = "Contenido",
            Width = 220,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        dgvFrames.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "DelayMs",
            HeaderText = "Retardo (ms)",
            Width = 90
        });
        dgvFrames.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = "IsActive",
            HeaderText = "Activa",
            Width = 60
        });

        var btnSendCol = new DataGridViewButtonColumn
        {
            HeaderText = "Acción",
            Text = "Enviar",
            UseColumnTextForButtonValue = true,
            Width = 70
        };
        dgvFrames.Columns.Add(btnSendCol);

        dgvFrames.CellContentClick += DgvFrames_CellContentClick;

        // Barra de botones superior (agregar/eliminar)
        var topBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Height = 36,
            FlowDirection = FlowDirection.LeftToRight
        };
        btnAdd = new Button { Text = "Agregar trama", Width = 120, Height = 28 };
        btnAdd.Click += (s, e) =>
        {
            FrameManager.AddFrame(new Frame { Name = "Nueva", Format = "HEX", Content = "", DelayMs = 0, IsActive = true });
        };

        btnDelete = new Button { Text = "Eliminar seleccionada", Width = 140, Height = 28 };
        btnDelete.Click += (s, e) =>
        {
            if (dgvFrames.CurrentRow?.DataBoundItem is Frame frame)
                FrameManager.RemoveFrame(frame);
        };

        topBar.Controls.AddRange(new Control[] { btnAdd, btnDelete });

        // Barra de botones inferior (envío masivo)
        var bottomBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Height = 36,
            FlowDirection = FlowDirection.LeftToRight
        };

        btnSendSelected = new Button { Text = "Enviar seleccionada", Width = 130, Height = 28 };
        btnSendSelected.Click += (s, e) =>
        {
            if (dgvFrames.CurrentRow?.DataBoundItem is Frame frame)
                SendFrameClicked?.Invoke(this, frame);
        };

        btnSendAllActive = new Button { Text = "Enviar todas activas", Width = 140, Height = 28 };
        btnSendAllActive.Click += (s, e) =>
        {
            SendAllActiveClicked?.Invoke(this, FrameManager.GetActiveFrames());
        };

        btnSendLoop = new Button { Text = "Enviar en bucle", Width = 120, Height = 28 };
        btnSendLoop.Click += async (s, e) => await StartLoopAsync();

        btnStopLoop = new Button { Text = "Detener bucle", Width = 110, Height = 28, Enabled = false };
        btnStopLoop.Click += (s, e) => StopLoop();

        btnClearLog = new Button { Text = "Limpiar log", Width = 100, Height = 28 };
        btnClearLog.Click += (s, e) => ClearLogClicked?.Invoke(this, EventArgs.Empty);

        lblStatus = new Label { Text = "Listo", Width = 200, Height = 28, TextAlign = ContentAlignment.MiddleLeft };

        bottomBar.Controls.AddRange(new Control[] { btnSendSelected, btnSendAllActive, btnSendLoop, btnStopLoop, btnClearLog, lblStatus });

        table.Controls.Add(dgvFrames, 0, 0);
        table.Controls.Add(topBar, 0, 1);
        table.Controls.Add(bottomBar, 0, 2);
        table.SetRowSpan(dgvFrames, 1);

        this.Controls.Add(table);
    }

    private void BindData()
    {
        bsFrames = new BindingSource { DataSource = FrameManager.Frames };
        dgvFrames.DataSource = bsFrames;
    }

    private void DgvFrames_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.ColumnIndex < 0 || e.RowIndex < 0) return;
        if (dgvFrames.Columns[e.ColumnIndex] is DataGridViewButtonColumn)
        {
            if (dgvFrames.Rows[e.RowIndex].DataBoundItem is Frame frame)
            {
                SendFrameClicked?.Invoke(this, frame);
            }
        }
    }

    private async Task StartLoopAsync()
    {
        var frames = FrameManager.GetActiveFrames();
        if (frames.Count == 0)
        {
            lblStatus.Text = "No hay tramas activas";
            return;
        }

        _loopCts = new CancellationTokenSource();
        btnSendLoop.Enabled = false;
        btnStopLoop.Enabled = true;
        lblStatus.Text = "Bucle en ejecución...";

        try
        {
            while (!_loopCts.Token.IsCancellationRequested)
            {
                foreach (var frame in frames)
                {
                    if (_loopCts.Token.IsCancellationRequested) break;
                    SendFrameClicked?.Invoke(this, frame);
                    await Task.Delay(frame.DelayMs, _loopCts.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // esperado
        }
        finally
        {
            btnSendLoop.Enabled = true;
            btnStopLoop.Enabled = false;
            lblStatus.Text = "Bucle detenido";
        }
    }

    private void StopLoop()
    {
        _loopCts?.Cancel();
    }

    public void SetStatus(string text)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action<string>(SetStatus), text);
            return;
        }
        lblStatus.Text = text;
    }
}
