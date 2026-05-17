using Systems.Common;

namespace Systems.Input
{
    public readonly struct CharacterInputView : IInputView
    {
        private readonly InputBuffer _buffer;
        private readonly EFacingDirection _currentFacing;

        public int Size => _buffer.Size;

        public CharacterInputView(InputBuffer buffer, EFacingDirection currentFacing)
        {
            _buffer = buffer;
            _currentFacing = currentFacing;
        }

        public TickInput GetFrame(int ticksAgo)
        {
            var frame = _buffer.GetFrame(ticksAgo);
            // Raw buffer is always world-space. Flip to character-space when facing left.
            // No stored-facing comparison needed — world→character conversion depends only
            // on current facing, not on what facing was recorded at write time.
            if (_currentFacing == EFacingDirection.Left)
                frame = frame.WithFlippedHorizontal();
            return frame;
        }
    }
}