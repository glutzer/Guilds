using MareLib;
using System.Collections.Generic;
using System.Linq;

namespace Guilds;

[GuildPage("Guild Members", 0)]
public class GuildPageGuildMembers : GuildPageEntry
{
    public GuildPageGuildMembers(string name, int priority, int id, GuildGui guildGui) : base(name, priority, id, guildGui)
    {
    }

    public override void OnPlayerReceivedOwnRequest(GuildPacket packet)
    {
        if (packet.type is EnumGuildPacket.Promote or EnumGuildPacket.Kick or EnumGuildPacket.CancelInvite)
        {
            guildGui.RefreshPage();
        }
    }

    public override void OnCreatePage(Widget parent)
    {
        int selectedGuildId = guildGui.selectedGuildId;
        string ownUid = guildGui.ownUid;

        if (selectedGuildId == -1)
        {
            new WidgetTextLine(parent, GuiThemes.Font, "No guild selected.", GuiThemes.TextColor)
                .Alignment(Align.Center)
                .PercentWidth(1)
                .FixedHeight(12);

            return;
        }

        Guild? guild = manager.guildData.GetGuild(selectedGuildId);
        if (guild == null) return;

        if (!guild.HasMember(ownUid)) return; // Not in guild.

        List<GuildMemberInfo> info = new();
        foreach (MembershipInfo memberInfo in guild.MemberInfo)
        {
            PlayerMetrics? metrics = manager.guildData.GetMetrics(memberInfo.playerUid ?? "");
            if (metrics == null) continue;

            RoleInfo? role = guild.GetRole(memberInfo.roleId);
            if (role == null) continue;

            info.Add(new GuildMemberInfo(metrics, new GuildMemberInfo.RoleData()
            {
                authority = role.authority,
                name = role.name
            }));
        }

        // Guild members.
        Column<GuildMemberInfo> nameColumn = new("Name", 1f, (member) => member.Metrics.lastName, (a, b) => a.Metrics.lastName.CompareTo(b.Metrics.lastName));
        Column<GuildMemberInfo> onlineColumn = new("Online", 0.5f, (member) => member.Metrics.GetLastOnlineString(), (a, b) => b.Metrics.lastOnline.CompareTo(a.Metrics.lastOnline));
        Column<GuildMemberInfo> roleColumn = new("Role", 1f, (member) => member.Role.name, (a, b) => b.Role.authority.CompareTo(a.Role.authority));

        Widget tableWidget = new WidgetSortableTable<GuildMemberInfo>(parent, info, (member, field) =>
        {
            new WidgetGuildPlayerInfoPopup(field, member.Metrics.uid)
            .Alignment(Align.LeftTop)
            .FixedSize(12, 8)
            .FixedPos(Gui.MouseX - field.X, Gui.MouseY - field.Y);
            guildGui.MarkForRepartition();
        }, nameColumn, onlineColumn, roleColumn)
            .Alignment(Align.CenterTop)
            .PercentWidth(0.8f)
            .SetChildSizing(ChildSizing.Height | ChildSizing.Once);

        // Guild invites.
        HashSet<string> invites = guild.GetInvites();
        if (invites.Count > 0)
        {
            Column<GuildMemberInfo> inviteColumn = new("Invited", 1f, (member) => member.Metrics.lastName, (a, b) => a.Metrics.lastName.CompareTo(b.Metrics.lastName));
            List<GuildMemberInfo> inviteMetrics = invites.Select(x => manager.guildData.GetMetrics(x)).Where(x => x != null).Select(x => new GuildMemberInfo(x!, default)).ToList();

            new WidgetSortableTable<GuildMemberInfo>(tableWidget, inviteMetrics, (member, field) =>
            {
                new WidgetGuildPlayerInfoPopup(field, member.Metrics.uid)
                .Alignment(Align.LeftTop)
                .FixedSize(12, 8)
                .FixedPos(Gui.MouseX - field.X, Gui.MouseY - field.Y);
                guildGui.MarkForRepartition();
            }, inviteColumn, onlineColumn)
                .Alignment(Align.CenterBottom, AlignFlags.OutsideV)
                .PercentWidth(1);
        }
    }
}