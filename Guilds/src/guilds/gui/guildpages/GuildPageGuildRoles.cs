namespace Guilds;

[GuildPage("Guild Roles", 0)]
public class GuildPageGuildRoles : GuildPageEntry
{
    public GuildPageGuildRoles(string name, int priority, int id, GuildGui guildGui) : base(name, priority, id, guildGui)
    {
    }

    public override void OnPlayerReceivedOwnRequest(GuildPacket packet)
    {
        if (packet.type is EnumGuildPacket.UpdateRole or EnumGuildPacket.RemoveRole or EnumGuildPacket.AddRole)
        {
            guildGui.RefreshPage();
        }
    }

    public override void OnCreatePage(Widget parent)
    {
        if (guildGui.selectedGuildId == -1)
        {
            new WidgetTextLine(parent, parent.Gui, VanillaThemes.Font, "No guild selected.", VanillaThemes.WhitishTextColor)
                .Alignment(Align.Center)
                .PercentWidth(1)
                .FixedHeight(12);

            return;
        }

        new WidgetRoleContainer(parent, guildGui)
            .Alignment(Align.CenterTop)
            .FixedHeight(16)
            .PercentWidth(1)
            .SetChildSizing(ChildSizing.Height);
    }
}