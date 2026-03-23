using FixedLogic;
using Systems.Core;

namespace Systems.Input
{
    /// <summary>
    /// A buffer of input information for a single combatant.
    /// </summary>
    public class InputBuffer
    {
        private readonly TickInput[] _buffer;
        private int _currentIndex = -1;
        public readonly int Size;

        public InputBuffer(int capacity = TickManager.TickRate * 2) // Default to 2 seconds worth of input
        {
            Size = capacity;
            _buffer = new TickInput[capacity];
        }
        
        public TickInput[] GetBuffer() => _buffer;

        public void Write(TickInput data)
        {
            _currentIndex = (_currentIndex + 1) % Size;
            _buffer[_currentIndex] = data;
        }
        
        public TickInput GetFrame(int ticksAgo)
        {
            if (ticksAgo >= Size || _currentIndex == -1) return default;
            
            int index = (_currentIndex - ticksAgo + Size) % Size;
            return _buffer[index];
        }
    }
}
