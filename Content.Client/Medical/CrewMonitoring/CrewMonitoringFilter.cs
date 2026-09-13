using Content.Shared.Medical.SuitSensor;
using Content.Shared.Medical.SuitSensors;

namespace Content.Client.Medical.CrewMonitoring;

public static class CrewMonitoringFilter
{
    public static bool Matches(SuitSensorStatus status, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return status.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
               || status.Job.Contains(query, StringComparison.CurrentCultureIgnoreCase)
               || status.Area?.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true;
    }
}
