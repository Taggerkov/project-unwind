using Systems.Common;

namespace Systems.Input
{
    /// <summary>
    /// A read-only view over an <see cref="InputBuffer"/> that converts world-space directions
    /// to character space on read. Callers receive inputs relative to the character's facing
    /// without needing to know which side of the screen they are on.
    /// </summary>
    public readonly struct CharacterInputView : IInputView
    {
        /// <summary>The underlying world-space buffer being wrapped.</summary>
        private readonly InputBuffer _buffer;

        /// <summary>The character's current facing direction, used to flip horizontal inputs on read.</summary>
        private readonly EFacingDirection _currentFacing;

        /// <summary>Number of frames stored in the underlying buffer.</summary>
        public int Size => _buffer.Size;

        /// <summary>
        /// Wraps an existing buffer with the given character facing for direction conversion.
        /// </summary>
        /// <param name="buffer">The world-space input buffer to read from.</param>
        /// <param name="currentFacing">The character's current facing; Left causes horizontal inputs to be flipped.</param>
        public CharacterInputView(InputBuffer buffer, EFacingDirection currentFacing)
        {
            _buffer = buffer;
            _currentFacing = currentFacing;
        }

        /// <summary>
        /// Returns the input frame from <paramref name="ticksAgo"/> ticks in the past,
        /// flipped to character space when the character faces left.
        /// </summary>
        /// <param name="ticksAgo">How many ticks back to read; 0 is the current frame.</param>
        /// <returns>The character-space <see cref="TickInput"/> for the requested frame.</returns>
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