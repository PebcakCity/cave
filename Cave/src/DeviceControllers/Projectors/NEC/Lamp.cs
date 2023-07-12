namespace Cave.src.DeviceControllers.Projectors.NEC
{
    public enum LampNumber
    {
        Lamp1 = 0x00,
        Lamp2 = 0x01
    }
    
    public enum LampInfo
    {
        UsageTimeSeconds = 0x01,
        GoodForSeconds = 0x02,
        RemainingPercent = 0x04,
        RemainingSeconds = 0x08
    }
}
