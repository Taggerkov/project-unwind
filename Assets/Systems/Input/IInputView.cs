namespace Systems.Input
{
    /// <summary>
    /// Read-only view over a frame buffer. Needed to allow systems to be facing-agnostic.
    /// </summary>
    public interface IInputView
    {
        TickInput GetFrame(int ticksAgo);
        int Size { get; }
    }
}