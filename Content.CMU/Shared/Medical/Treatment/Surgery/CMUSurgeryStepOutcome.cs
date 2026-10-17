namespace Content.Shared.CMU14.Medical.Treatment.Surgery;

/// <summary>The outcome of applying a surgical effect and committing its markers.</summary>
public enum CMUSurgeryStepOutcome : byte
{
    Failed,
    Disabled,
    InvalidSite,
    InvalidTool,
    Succeeded,
}
