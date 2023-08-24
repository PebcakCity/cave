using Ardalis.SmartEnum;

namespace Cave.DeviceControllers.Televisions.Roku
{
    /// <summary>
    /// A class representing the state of the TV's media player, documented (I think) here:
    /// https://developer.roku.com/docs/references/scenegraph/media-playback-nodes/video.md
    /// </summary>
    public class MediaState 
        : SmartEnum<MediaState>
    {
        public static readonly MediaState None = new(0, nameof(None));
        public static readonly MediaState Buffering = new(1, nameof(Buffering));
        public static readonly MediaState Playing = new(2, nameof(Playing));
        public static readonly MediaState Paused = new(3, nameof(Paused));
        public static readonly MediaState Stopped = new(4, nameof(Stopped));
        public static readonly MediaState Finished = new(5, nameof(Finished));
        public static readonly MediaState Error = new(6, nameof(Error));
        public MediaState(int value, string name) : base(name, value) { }
    }
}
