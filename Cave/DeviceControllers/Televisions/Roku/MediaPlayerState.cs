using Ardalis.SmartEnum;

namespace Cave.DeviceControllers.Televisions.Roku
{
    /// <summary>
    /// A class representing the state of the TV's media player, documented (I think) here:
    /// https://developer.roku.com/docs/references/scenegraph/media-playback-nodes/video.md
    /// </summary>
    public class MediaPlayerState 
        : SmartEnum<MediaPlayerState>
    {
        public static readonly MediaPlayerState None = new(0, nameof(None));
        public static readonly MediaPlayerState Buffering = new(1, nameof(Buffering));
        public static readonly MediaPlayerState Playing = new(2, nameof(Playing));
        public static readonly MediaPlayerState Paused = new(3, nameof(Paused));
        public static readonly MediaPlayerState Stopped = new(4, nameof(Stopped));
        public static readonly MediaPlayerState Finished = new(5, nameof(Finished));
        public static readonly MediaPlayerState Error = new(6, nameof(Error));
        public MediaPlayerState(int value, string name) : base(name, value) { }
    }
}
