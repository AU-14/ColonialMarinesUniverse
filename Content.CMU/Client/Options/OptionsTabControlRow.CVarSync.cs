namespace Content.Client.Options.UI;

public sealed partial class OptionsTabControlRow
{
    private bool _initialized; // CMU14
    private bool _inTree; // CMU14

    // CMU14 Begin: avoid retaining options windows through configuration callbacks.
    protected override void EnteredTree()
    {
        base.EnteredTree();
        _inTree = true;
        if (!_initialized)
            return;
        foreach (var option in _options)
            option.StartListening();
    }

    protected override void ExitedTree()
    {
        foreach (var option in _options)
            option.StopListening();
        _inTree = false;
        base.ExitedTree();
    }
    // CMU14 End
}

public abstract partial class BaseOption
{
    // CMU14: non-CVar options need no subscription, but share the row lifecycle.
    public virtual void StartListening() { }
    public virtual void StopListening() { }
}

public abstract partial class BaseOptionCVar<TValue> where TValue : notnull
{
    private bool _subscribed;

    // CMU14 Begin: reflect external CVar changes without leaking the option or its controls.
    public override void StartListening()
    {
        if (_subscribed)
            return;
        _subscribed = true;
        _cfg.OnValueChanged(_cVar, OnCVarChanged, invokeImmediately: true);
    }

    public override void StopListening()
    {
        if (!_subscribed)
            return;
        _cfg.UnsubValueChanged(_cVar, OnCVarChanged);
        _subscribed = false;
    }

    private void OnCVarChanged(TValue value)
    {
        Value = value;
        ValueChanged();
    }
    // CMU14 End
}
