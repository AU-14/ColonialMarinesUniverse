namespace Content.Shared._CMU14.Dropship.Integrity;

public enum DropshipAlarm : byte
{
    Proximity,
    LowIntegrity,
}

public static class DropshipAlarmData
{
    public static string GetAlertName(DropshipAlarm alarm)
    {
        return alarm switch
        {
            DropshipAlarm.Proximity => Loc.GetString("cmu-gunship-alarm-proximity"),
            DropshipAlarm.LowIntegrity => Loc.GetString("cmu-gunship-alarm-low-integrity"),
            _ => Loc.GetString("cmu-gunship-alarm-unknown"),
        };
    }
}
