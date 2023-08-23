using Cave.Utils;

namespace Cave.DeviceControllers
{
    /// <summary>
    /// A simple wrapper around any data we might be interested in passing to
    /// any observer.  If a nullable field is non-null, that should indicate
    /// that it's part of what updated.
    /// </summary>
    public struct DeviceInfo
    {
        public string? ModelNumber { get; set; }
        public string? SerialNumber { get; set; }
        public int? LampHoursTotal { get; set; }
        public int? LampHoursUsed { get; set; }
        public object? PowerState { get; set; }
        public object? InputSelected { get; set; }
        public bool IsDisplayMuted { get; set; }
        public bool IsAudioMuted { get; set; }
        public string? Message { get; set; }
        public MessageType MessageType { get; set; }
    }
    public enum MessageType { Info, Success, Warning, Error }
}
