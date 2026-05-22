using System.ComponentModel;
using SimuladorTCP.Models;

namespace SimuladorTCP.Services;

/// <summary>
/// Gestor de la lista de tramas con BindingList para enlazar con DataGridView.
/// </summary>
public class FrameManager
{
    public BindingList<Frame> Frames { get; } = new();

    public event EventHandler? FramesChanged;

    public FrameManager()
    {
        Frames.ListChanged += (s, e) => FramesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddFrame(Frame frame)
    {
        frame.Number = GetNextNumber();
        Frames.Add(frame);
    }

    public void RemoveFrame(Frame frame)
    {
        Frames.Remove(frame);
        RenumberFrames();
    }

    public void ClearFrames()
    {
        Frames.Clear();
    }

    public void LoadFrames(IEnumerable<Frame> frames)
    {
        Frames.Clear();
        foreach (var f in frames.OrderBy(f => f.Number))
        {
            Frames.Add(f);
        }
        RenumberFrames();
    }

    public List<Frame> GetActiveFrames()
    {
        return Frames.Where(f => f.IsActive).OrderBy(f => f.Number).ToList();
    }

    public Frame? GetFrameByNumber(int number)
    {
        return Frames.FirstOrDefault(f => f.Number == number);
    }

    private int GetNextNumber()
    {
        if (Frames.Count == 0) return 1;
        return Frames.Max(f => f.Number) + 1;
    }

    private void RenumberFrames()
    {
        int i = 1;
        foreach (var frame in Frames.OrderBy(f => f.Number))
        {
            frame.Number = i++;
        }
    }
}
