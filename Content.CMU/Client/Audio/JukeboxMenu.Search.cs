using System.Linq;
using Content.Shared.Audio.Jukebox;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Audio.Jukebox;

public sealed partial class JukeboxMenu
{
    private JukeboxPrototype[] _songs = [];

    private void SetSongCatalog(IEnumerable<JukeboxPrototype> songs)
    {
        _songs = songs.OrderBy(song => song.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(song => song.ID, StringComparer.Ordinal)
            .ToArray();
        RefreshSongList();
    }

    private void RefreshSongList()
    {
        var query = SongSearch.Text.Trim();
        MusicList.Clear();
        MusicList.Add(_songs
            .Where(song => song.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .Select(song => new ItemList.Item(MusicList) { Text = song.Name, Metadata = song.ID }));

        if (MusicList.Count == 0)
            MusicList.AddItem(Loc.GetString("cmu-jukebox-no-matches"), selectable: false);
    }
}
