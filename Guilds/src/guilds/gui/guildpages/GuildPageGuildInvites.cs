using System.Collections.Generic;
using System.Linq;

namespace Guilds;

[GuildPage("Guild Invites", 0)]
public class GuildPageGuildInvites : GuildPageEntry
{
    public GuildPageGuildInvites(string name, int priority, int id, GuildGui guildGui) : base(name, priority, id, guildGui)
    {
    }

    public override void OnCreatePage(Widget parent)
    {
        string ownUid = guildGui.ownUid;

        List<Guild> guildInvites = manager.guildData.GetPlayersInvites(ownUid).Select(x => manager.guildData.GetGuild(x)).Where(x => x != null).ToList()!;

        int index = 0;

        foreach (Guild guild in guildInvites)
        {
            // Add button to accept or deny invite.
            new WidgetVanillaButton(parent, parent.Gui, () =>
            {
                GuildPacket packet = new()
                {
                    type = EnumGuildPacket.AcceptInvite,
                    guildId = guild.id
                };
                manager.SendPacket(packet);
            }, $"Join {guild.name}").Alignment(Align.CenterTop).Fixed(Gui.Scaled(-32), Gui.Scaled(index * 12), 64, 12);

            new WidgetVanillaButton(parent, parent.Gui, () =>
            {
                GuildPacket packet = new()
                {
                    type = EnumGuildPacket.AcceptInvite,
                    guildId = guild.id
                };
                manager.SendPacket(packet);
            }, $"Deny").Alignment(Align.CenterTop).Fixed(Gui.Scaled(32), Gui.Scaled(index * 12), 64, 12);

            index++;
        }

        if (guildInvites.Count == 0)
        {
            new WidgetTextLine(parent, parent.Gui, VanillaThemes.Font, "No invites received.", VanillaThemes.WhitishTextColor)
                .Alignment(Align.Center)
                .PercentWidth(1f)
                .FixedHeight(12);
        }
    }
}