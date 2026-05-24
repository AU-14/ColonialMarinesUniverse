using Content.Server.Database;
using Content.Shared.CCVar;
using Robust.Server.Upload;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server.Administration;

public sealed partial class ContentNetworkResourceManager
{
    [Dependency] private IServerDbManager _serverDb = default!;
    [Dependency] private NetworkResourceManager _netRes = default!;
    [Dependency] private IConfigurationManager _cfgManager = default!;

    [ViewVariables] public bool StoreUploaded { get; set; } = true;

    public void Initialize()
    {
        _cfgManager.OnValueChanged(CCVars.ResourceUploadingStoreEnabled, value => StoreUploaded = value, true);
        AutoDelete(_cfgManager.GetCVar(CCVars.ResourceUploadingStoreDeletionDays));
        _netRes.ResourcesUploaded += OnUploadResources;
    }

    private async void OnUploadResources(NetworkResourcesUploadedEvent ev)
    {
        if (!StoreUploaded)
            return;

        foreach (var (relativePath, data) in ev.Files)
        {
            await _serverDb.AddUploadedResourceLogAsync(ev.Session.UserId, DateTime.Now, relativePath.ToString(), data);
        }
    }

    private async void AutoDelete(int days)
    {
        if (days > 0)
            await _serverDb.PurgeUploadedResourceLogAsync(days);
    }
}
