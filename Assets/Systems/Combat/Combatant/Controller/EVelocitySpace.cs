namespace Systems.Combat.Combatant.Controller
{
    public enum EVelocitySpace
    {
        /// <summary>
        /// X axis means "forward" (away from opponent = negative, towards = positive).
        /// Flipped automatically by the controller when the character faces left.
        /// Use this for dashes, walks, knockback — anything move-relative.
        /// </summary>
        Character,

        /// <summary>
        /// Raw world coordinates. Never flipped.
        /// Use this for gravity, stage-locked effects, or anything that should
        /// ignore which way the character faces.
        /// </summary>
        World
    }
}