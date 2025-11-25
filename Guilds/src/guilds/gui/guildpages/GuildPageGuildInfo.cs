using Vintagestory.API.Common;

namespace Guilds;

[GuildPage("Guild Info", 0)]
public class GuildPageGuildInfo : GuildPageEntry
{
    public GuildPageGuildInfo(string name, int priority, int id, GuildGui guildGui) : base(name, priority, id, guildGui)
    {
    }

    public override void OnCreatePage(Widget parent)
    {
        int selectedGuildId = guildGui.selectedGuildId;
        string ownUid = guildGui.ownUid;

        if (selectedGuildId == -1)
        {
            new WidgetTextLine(parent, parent.Gui, VanillaThemes.Font, "No guild selected.", VanillaThemes.WhitishTextColor).Alignment(Align.Center)
                .PercentWidth(1)
                .FixedHeight(12);

            return;
        }

        Guild? guild = guildGui.manager.guildData.GetGuild(selectedGuildId);
        if (guild == null) return;

        new WidgetTextLine(parent, parent.Gui, VanillaThemes.Font, guild.name, VanillaThemes.WhitishTextColor)
            .Alignment(Align.CenterTop)
            .PercentWidth(1)
            .FixedHeight(12);

        RoleInfo? roleInfo = guild.GetRole(ownUid);
        if (roleInfo?.id == 1)
        {
            new WidgetHoldDownGuildButton(parent, parent.Gui, 5f, () =>
            {
                GuildPacket packet = new()
                {
                    type = EnumGuildPacket.Disband,
                    guildId = selectedGuildId
                };

                MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client).SendPacket(packet);
            }, "Disband Guild").Alignment(Align.CenterTop).Fixed(0, Gui.Scaled(196), 64, 16);
        }
        else
        {
            new WidgetVanillaButton(parent, parent.Gui, () =>
            {
                GuildPacket packet = new()
                {
                    type = EnumGuildPacket.Leave,
                    guildId = selectedGuildId
                };

                MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client).SendPacket(packet);
            }, "Leave Guild").Alignment(Align.CenterTop).Fixed(0, Gui.Scaled(256), 32, 12);
        }

        WidgetToggleableButton repButton = new(parent, parent.Gui, (on) =>
        {
            GuildPacket packet = new()
            {
                type = EnumGuildPacket.RepGuild,
                guildId = on ? selectedGuildId : -1
            };

            manager.SendPacket(packet);
        }, "Rep Guild", false);
        repButton.Alignment(Align.CenterTop).Fixed(0, Gui.Scaled(12), 32, 12);

        ClaimManager claimManager = MainAPI.GetGameSystem<ClaimManager>(EnumAppSide.Client);
        new WidgetTextLine(repButton, parent.Gui, VanillaThemes.Font, $"Claims: {claimManager.claimData.GetClaimCount(guild)}/{ClaimManager.GetMaxClaims(guild)}", VanillaThemes.WhitishTextColor, true)
            .Alignment(Align.LeftMiddle, AlignFlags.OutsideH)
            .FixedSize(32, 12);

        if (roleInfo?.HasPermissions(GuildPerms.ManageGuildInfo) == true)
        {
            GuildUpdateInfo packet = new()
            {
                name = guild.name,
                color = guild.Color
            };

            new WidgetColorPicker(parent, parent.Gui, color =>
            {
                packet.color = color;
            }, guild.Color).Alignment(Align.CenterTop).Fixed(0, Gui.Scaled(32), 64, 64);

            new WidgetGuildLabeledInput(parent, parent.Gui, guild.name, "Guild Name", s =>
            {
                packet.name = s;
            }, s =>
            {
                return s.Length is > 2 and < 33;
            }).Alignment(Align.CenterTop).Fixed(0, Gui.Scaled(112), 64, 12);

            new WidgetVanillaButton(parent, parent.Gui, () =>
            {
                GuildPacket p = GuildPacket.Create(EnumGuildPacket.UpdateInfo, packet, null, guild.id, 0);
                manager.SendPacket(p);
            }, "Save").Alignment(Align.CenterTop).Fixed(0, Gui.Scaled(128), 32, 12);
        }

        PlayerMetrics? metrics = manager.guildData.GetMetrics(ownUid);
        if (metrics != null && metrics.reppedGuildId == selectedGuildId)
        {
            repButton.LockDown();
        }
    }
}