using Cave.Utils;

namespace Cave.DeviceControllers
{
    public class DeviceStatus
    {
        public Enumeration? PowerState {get; set;}
        public Enumeration? InputSelected {get; set;}
        public bool? VideoMuted {get; set;}
        public bool? AudioMuted {get; set;}
        public string? Message {get; set;}
        public MessageTypes MessageType {get; set;}
        public enum MessageTypes { Info, Warning, Error }
    }
}
