using Systems.Common;
using Systems.Core;

namespace Systems.Input
{
    /// <summary>
    /// A buffer of input information for a single combatant.
    /// </summary>
    public class InputBuffer : IInputView
    {
        private readonly TickInput[] _buffer;
        private readonly EFacingDirection[] _facingAtWrite;
        private int _currentIndex = -1;
        public readonly int Size;
        int IInputView.Size => Size;

        public InputBuffer(int capacity = TickManager.TickRate * 2) // Default to 2 seconds worth of input
        {
            Size = capacity;
            _buffer = new TickInput[capacity];
            _facingAtWrite = new EFacingDirection[capacity];
        }

        public TickInput[] GetBuffer() => _buffer;

        // Store facing per frame in a parallel array inside InputBuffer
        public void Write(TickInput data)
        {
            _currentIndex = (_currentIndex + 1) % Size;
            _buffer[_currentIndex] = data;
            _facingAtWrite[_currentIndex] =
                EFacingDirection.Right;
        }

        public TickInput GetFrame(int ticksAgo)
        {
            if (ticksAgo >= Size || _currentIndex == -1) return default;
            int index = (_currentIndex - ticksAgo + Size) % Size;
            return _buffer[index];
        }
    }
}