using Cave.Utils;

namespace Cave.DeviceControllers
{
    /// <summary>
    /// A simple wrapper around any data we might be interested in passing to
    /// any observer.  If a field is non-null, that should indicate that it's
    /// part of what updated.
    /// </summary>
    public class DeviceStatus
    {
        public string? ModelNumber { get; set; }
        public string? SerialNumber { get; set; }
        public int? LampHoursTotal { get; set; }
        public int? LampHoursUsed { get; set; }
        public Enumeration? PowerState { get; set; }
        public Enumeration? InputSelected { get; set; }
        public bool? VideoMuted { get; set; }
        public bool? AudioMuted { get; set; }
        public string? Message { get; set; }
        public MessageType MessageType { get; set; }
    }
    public enum MessageType { Info, Success, Warning, Error }
}
