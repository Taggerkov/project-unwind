using System;

namespace Systems.Input
{
    /// <summary>
    /// Numpad-notation direction. The nine values map to a standard joystick/dpad:
    ///
    ///   7 8 9
    ///   4 5 6
    ///   1 2 3
    ///
    /// 5 = neutral (no directional input).
    /// </summary>
    public enum EDirectionInput
    {
        None = 0,
        Input5 = 0,
        Input1 = 1,
        Input2 = 2,
        Input3 = 3,
        Input4 = 4,
        Input6 = 6,
        Input7 = 7,
        Input8 = 8,
        Input9 = 9,
    }

    /// <summary>
    /// Flags representing one or more action buttons pressed simultaneously.
    /// Maps directly to the action entries in EInputType.
    ///
    /// Using [Flags] means combinations are free:
    ///   EButtonInput.Medium | EButtonInput.Heavy  →  MH (grab input)
    ///   EButtonInput.Input6 | EButtonInput.Ability →  6AB (forward + ability)
    /// </summary>
    [Flags]
    public enum EButtonInput
    {
        None = 0,
        Light = 1 << 0, // EInputType.InputLightAttack
        Medium = 1 << 1, // EInputType.InputMediumAttack
        Heavy = 1 << 2, // EInputType.InputHeavyAttack
        Unique = 1 << 3, // EInputType.InputUniqueAttack
        Guard = 1 << 4, // EInputType.InputGuard
        Ability = 1 << 5, // EInputType.InputAbility
    }

    /// <summary>
    /// The directional sequence component of a move's input requirement.
    /// Combined with EButtonInput to form a complete MoveInputDescriptor.
    ///
    /// Disallow variants invert the usual check: the condition is satisfied when the
    /// player is NOT holding any direction in the specified group. Use them as
    /// constraints inside a multi-condition MoveInputEntry (AND clause).
    /// </summary>
    public enum EMotionInput
    {
        // ── No motion — just press the button (or truly neutral when button-less) ─────
        None,

        // ── Held directions ───────────────────────────────────────────────────────────
        Held4, // ← (back)
        Held6, // → (forward)
        Held2, // ↓ (down)
        Held8, // ↑ (up)

        HeldAnyBack,    // ← or ↙ or ↖ (any back direction)
        HeldAnyForward, // → or ↘ or ↗ (any forward direction)
        HeldAnyDown,    // ↓ or ↙ or ↘ (any down direction)
        HeldAnyUp,      // ↑ or ↖ or ↗ (any up direction)

        // ── Disallow (NOT) directions ─────────────────────────────────────────────────
        // These resolve to TRUE when the player is NOT holding any direction in the group.
        // Analogous to BBScript's INPUT_DISALLOW_* inputs.
        DisallowAnyBack,    // true when 4, 1, 7 are NOT held
        DisallowAnyForward, // true when 6, 3, 9 are NOT held
        DisallowAnyDown,    // true when 2, 1, 3 are NOT held
        DisallowAnyUp,      // true when 8, 7, 9 are NOT held

        // ── Charged directions ────────────────────────────────────────────────────────
        Charge46, // ←, hold, then → (charge forward)
        Charge64, // →, hold, then ← (charge back)
        Charge28, // ↓, hold, then ↑ (charge up)
        Charge82, // ↑, hold, then ↓ (charge down)

        // ── Quarter circles ───────────────────────────────────────────────────────────
        QCF, // 236  (quarter circle forward)
        QCB, // 214  (quarter circle back)

        // ── Dragon punch / reverse dragon punch ───────────────────────────────────────
        DP,  // 623  (dragon punch / shoryuken)
        RDP, // 421  (reverse dragon punch)

        // ── Half circles ──────────────────────────────────────────────────────────────
        HCF, // 41236 (half circle forward)
        HCB, // 63214 (half circle back)

        // ── Full circle ───────────────────────────────────────────────────────────────
        FC, // 360  (full circle / 41236974 approximation)

        // ── Double taps ───────────────────────────────────────────────────────────────
        DoubleTap4, // ←← (double back — common for backdash)
        DoubleTap6, // →→ (double forward — common for dash)
        DoubleTap2, // ↓↓ (double down)
        DoubleTap8, // ↑↑ (double up)
    }
}