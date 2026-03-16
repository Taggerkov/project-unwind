namespace Systems.Combat.Core.Input
{
    /// <summary>
    /// <code>
    ///    7 / 8 / 9       NW / N / NE
    ///    4 / 5 / 6   =    W / C / E
    ///    1 / 2 / 3       SW / S / SE
    ///</code>
    /// </summary>
    public enum EInputType
    {
        InputLightAttack,
        InputMediumAttack,
        InputHeavyAttack,
        /// <summary>
        ///  Represents North-West movement in numpad notation.
        /// </summary>
        Input7,
        Input8,
        Input9,
        Input4,
        Input5,
        Input6,
        Input1,
        Input2,
        Input3,
        // Input236,
        // Input214,
        // Input623,
    }
}
