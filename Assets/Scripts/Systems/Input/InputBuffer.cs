using FixedLogic;

namespace Systems.Input
{
    public class InputBuffer
    {
        private readonly FrameInput[] _buffer;
        private int _currentIndex = -1;
        private readonly int _size;

        public InputBuffer(int capacity = TickManager.TickRate * 2) // Default to 2 seconds worth of input
        {
            _size = capacity;
            _buffer = new FrameInput[capacity];
        }

        public void Write(FrameInput frame)
        {
            _currentIndex = (_currentIndex + 1) % _size;
            _buffer[_currentIndex] = frame;
        }
        
        public FrameInput GetFrame(int framesAgo)
        {
            if (framesAgo >= _size || _currentIndex == -1) return default;
            
            int index = (_currentIndex - framesAgo + _size) % _size;
            return _buffer[index];
        }
    }
}
