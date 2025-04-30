using MareLib;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace Guilds;

[GuildPage("Players", -10)]
public class GuildPagePlayers : GuildPageEntry
{
    public GuildPagePlayers(string name, int priority, int id, GuildGui guildGui) : base(name, priority, id, guildGui)
    {
    }

    public override void OnCreatePage(Widget parent)
    {
        Column<PlayerMetrics> nameColumn = new("Name", 1f, (member) => member.lastName, (a, b) => a.lastName.CompareTo(b.lastName));
        Column<PlayerMetrics> onlineColumn = new("Online", 0.5f, (member) => member.GetLastOnlineString(), (a, b) => b.lastOnline.CompareTo(a.lastOnline));

        List<PlayerMetrics> metrics = MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client).guildData.AllMetrics.OrderByDescending(x => x.lastOnline).ToList();

        new WidgetSortableTable<PlayerMetrics>(parent, metrics, (member, field) =>
        {
            new WidgetGuildPlayerInfoPopup(field, member.uid)
            .Alignment(Align.LeftTop)
            .FixedSize(12, 8)
            .FixedPos(Gui.MouseX - field.X, Gui.MouseY - field.Y);
            guildGui.MarkForRepartition();
        }, nameColumn, onlineColumn).Alignment(Align.CenterTop).Percent(0, 0, 0.8f, 0.05f).FixedHeight(12);
    }
}