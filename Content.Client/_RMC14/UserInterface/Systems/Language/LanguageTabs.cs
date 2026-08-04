using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.UserInterface.Systems.Language;

public sealed class LanguageTabs
{
    private static readonly ResPath Path = new("/language_tabs.txt");
    private readonly IResourceManager _resource;

    public List<LanguageTab> Tabs { get; private set; } = new();

    public LanguageTabs(IResourceManager resource)
    {
        _resource = resource;
        Load();
    }

    public LanguageTab AddTab(string name)
    {
        var tab = new LanguageTab { Name = name };
        Tabs.Add(tab);
        Save();
        return tab;
    }

    public void RemoveTab(LanguageTab tab)
    {
        Tabs.Remove(tab);
        Save();
    }

    public void RenameTab(LanguageTab tab, string newName)
    {
        tab.Name = newName;
        Save();
    }

    public void AddLanguageToTab(LanguageTab tab, string languageId)
    {
        tab.Languages.Add(languageId);
        Save();
    }

    public void RemoveLanguageFromTab(LanguageTab tab, string languageId)
    {
        tab.Languages.Remove(languageId);
        Save();
    }

    public List<LanguageTab> GetTabsContaining(string languageId)
    {
        var result = new List<LanguageTab>();
        foreach (var tab in Tabs)
        {
            if (tab.Languages.Contains(languageId))
                result.Add(tab);
        }
        return result;
    }

    // Format: one tab per line
    // TAB:TabName
    // LANG:LanguageId
    // TAB:NextTabName
    // ...
    private void Load()
    {
        if (!_resource.UserData.TryReadAllText(Path, out var text))
            return;

        Tabs = new();
        LanguageTab? current = null;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("TAB:"))
            {
                current = new LanguageTab { Name = line.Substring(4) };
                Tabs.Add(current);
            }
            else if (line.StartsWith("LANG:") && current != null)
            {
                current.Languages.Add(line.Substring(5));
            }
        }
    }

    public void Save()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var tab in Tabs)
        {
            sb.AppendLine($"TAB:{tab.Name}");
            foreach (var lang in tab.Languages)
                sb.AppendLine($"LANG:{lang}");
        }
        _resource.UserData.WriteAllText(Path, sb.ToString());
    }
}

public sealed class LanguageTab
{
    public string Name { get; set; } = "New Tab";
    public HashSet<string> Languages { get; set; } = new();
}