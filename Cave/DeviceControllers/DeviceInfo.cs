namespace Cave.DeviceControllers
{
    /// <summary>
    /// A simple wrapper around any data we might be interested in passing to
    /// any observer.  If a nullable field is non-null, that should indicate
    /// that it's part of what updated.
    /// </summary>
    public struct DeviceInfo
    {
        /* General info common to many/most devices */
        public string? ModelNumber { get; set; }
        public string? SerialNumber { get; set; }

        /* Power and input states */
        public object? PowerState { get; set; }
        public object? InputSelected { get; set; }

        /* Video/audio mute states */
        public bool IsDisplayMuted { get; set; }
        public bool IsAudioMuted { get; set; }

        /* More device-specific info */

        /* Projector lamp info */
        public int? LampHoursTotal { get; set; }
        public int? LampHoursUsed { get; set; }

        /* TV/media player state */
        public object? MediaState { get; set; }

        /* Messaging/reporting */

        public string? Message { get; set; }
        public MessageType MessageType { get; set; }
    }
    public enum MessageType { Info, Success, Warning, Error }
}
