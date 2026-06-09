using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimuladorTCP.Models;
using SimuladorTCP.Services;

namespace SimuladorTCP.UI;

public partial class FrameEditorView : UserControl
{
    public FrameManager? FrameManager { get; set; }

    public event EventHandler<SimuladorTCP.Models.Frame>? SendFrameClicked;
    public event EventHandler<List<SimuladorTCP.Models.Frame>>? SendAllActiveClicked;
    public event EventHandler? ClearLogClicked;

    private CancellationTokenSource? _loopCts;
    private SimuladorTCP.Models.Frame? _selectedFrame;

    public FrameEditorView()
    {
        InitializeComponent();
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        if (FrameManager != null)
        {
            dgvFrames.ItemsSource = FrameManager.Frames;
            FrameManager.Frames.ListChanged += (s, ev) => dgvFrames.Items.Refresh();
        }
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        FrameManager?.AddFrame(new SimuladorTCP.Models.Frame { Name = "Nueva", Format = "HEX", Content = "", DelayMs = 0, IsActive = true });
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (dgvFrames.SelectedItem is SimuladorTCP.Models.Frame frame)
            FrameManager?.RemoveFrame(frame);
    }

    private void BtnSendCell_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SimuladorTCP.Models.Frame frame)
            SendFrameClicked?.Invoke(this, frame);
    }

    private void BtnSendSelected_Click(object sender, RoutedEventArgs e)
    {
        if (dgvFrames.SelectedItem is SimuladorTCP.Models.Frame frame)
            SendFrameClicked?.Invoke(this, frame);
    }

    private void BtnSendAllActive_Click(object sender, RoutedEventArgs e)
    {
        if (FrameManager != null)
            SendAllActiveClicked?.Invoke(this, FrameManager.GetActiveFrames());
    }

    private async void BtnSendLoop_Click(object sender, RoutedEventArgs e)
    {
        if (FrameManager == null) return;
        var frames = FrameManager.GetActiveFrames();
        if (frames.Count == 0)
        {
            lblStatus.Text = "No hay tramas activas";
            lblStatus.Foreground = (Brush)FindResource("ErrorBrush");
            return;
        }

        _loopCts = new CancellationTokenSource();
        btnSendLoop.IsEnabled = false;
        btnStopLoop.IsEnabled = true;
        lblStatus.Text = "Bucle en ejecucion...";
        lblStatus.Foreground = (Brush)FindResource("WarningBrush");

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
        catch (OperationCanceledException) { }
        finally
        {
            btnSendLoop.IsEnabled = true;
            btnStopLoop.IsEnabled = false;
            lblStatus.Text = "Bucle detenido";
            lblStatus.Foreground = (Brush)FindResource("TextSecondaryBrush");
        }
    }

    private void BtnStopLoop_Click(object sender, RoutedEventArgs e)
    {
        _loopCts?.Cancel();
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        ClearLogClicked?.Invoke(this, EventArgs.Empty);
    }

    private void DgvFrames_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        // Commit changes
    }

    private void DgvFrames_CurrentCellChanged(object sender, EventArgs e)
    {
        _selectedFrame = dgvFrames.SelectedItem as SimuladorTCP.Models.Frame;
    }

    public void SetStatus(string text)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => SetStatus(text)); return; }
        lblStatus.Text = text;
    }
}
