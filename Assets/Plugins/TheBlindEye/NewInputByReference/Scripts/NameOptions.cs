namespace NewInputByReference
{
    public enum NameOptions
    {
        /// <summary>
        /// The default behavior.
        /// </summary>
        None = 0,

        /// <summary>
        /// Do not mention the device of the control. For example, instead of "A [Gamepad]",
        /// return just "A".
        /// </summary>
        OmitDevice = 1 << 1,

        /// <summary>
        /// When available, use short display names instead of long ones. For example, instead of "Left Button",
        /// return "LMB".
        /// </summary>
        UseShortNames = 1 << 2
    }
}