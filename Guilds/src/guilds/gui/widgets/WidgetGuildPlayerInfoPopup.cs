using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Guilds;

public class WidgetGuildPlayerInfoPopup : Widget
{
    public override int SortPriority => 1;

    public WidgetGuildPlayerInfoPopup(Widget? parent, Gui gui, string playerUid) : base(parent, gui)
    {
        // Fits all children.
        SetChildSizing(ChildSizing.Width | ChildSizing.Height);

        int heightOffset = -8;

        GuildManager manager = MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client);
        Guild? guild = manager.guildData.GetGuild(manager.guildGui?.selectedGuildId ?? -1);

        if (guild != null && guild.GetRole(MainAPI.Capi.World.Player.PlayerUID) is RoleInfo ownRole)
        {
            if (guild.GetRole(playerUid) is RoleInfo targetRole)
            {
                if (ownRole.HasPermissions(GuildPerms.Kick) && ownRole.authority > targetRole.authority)
                {
                    new WidgetHoldDownGuildButton(this, gui, 2, () =>
                    {
                        manager.SendPacket(GuildPacket.Create(EnumGuildPacket.Kick, playerUid, guild.id));
                        DeleteSelf();
                    }, $"Kick From {guild.name}").Alignment(Align.LeftTop).FixedSize(64, 12).FixedY(heightOffset += 12);
                }
            }

            if (ownRole.HasPermissions(GuildPerms.Invite) && !guild.HasMember(playerUid))
            {
                if (guild.IsInvited(playerUid))
                {
                    new WidgetVanillaButton(this, gui, () =>
                    {
                        manager.SendPacket(GuildPacket.Create(EnumGuildPacket.CancelInvite, playerUid, guild.id, 0));
                        DeleteSelf();
                    }, $"Cancel {guild.name} Invite").Alignment(Align.LeftTop).FixedSize(64, 12).FixedY(heightOffset += 12);
                }
                else
                {
                    new WidgetVanillaButton(this, gui, () =>
                    {
                        manager.SendPacket(GuildPacket.Create(EnumGuildPacket.Invite, playerUid, guild.id, 0));
                        DeleteSelf();
                    }, $"Invite To {guild.name}").Alignment(Align.LeftTop).FixedSize(64, 12).FixedY(heightOffset += 12);
                }
            }

            // Roles.
            if (guild.GetRole(playerUid) is RoleInfo promoteRole && ownRole.HasPermissions(GuildPerms.Promote) && promoteRole.authority < ownRole.authority)
            {
                foreach (RoleInfo role in guild.roles.OrderByDescending(x => x.authority))
                {
                    if (role.authority >= ownRole.authority) continue;
                    if (role.id == promoteRole.id) continue;

                    new WidgetHoldDownGuildButton(this, gui, 2f, () =>
                    {
                        manager.SendPacket(GuildPacket.Create(EnumGuildPacket.Promote, playerUid, guild.id, role.id));
                        DeleteSelf();
                    }, role.authority >= promoteRole.authority ? $"Promote To {role.name}" : $"Demote To {role.name}").Alignment(Align.LeftTop).FixedSize(64, 12).FixedY(heightOffset += 12);
                }

                if (ownRole.id == 1) // Founder.
                {
                    new WidgetHoldDownGuildButton(this, gui, 10f, () =>
                    {
                        manager.SendPacket(GuildPacket.Create(EnumGuildPacket.Promote, playerUid, guild.id, 1));
                        DeleteSelf();
                    }, $"Make Guild Leader").Alignment(Align.LeftTop).FixedSize(64, 12).FixedY(heightOffset += 12);
                }
            }
        }
    }

    public override void RegisterEvents(GuiEvents guiEvents)
    {
        guiEvents.MouseDown += GuiEvents_MouseDown;
    }

    private void GuiEvents_MouseDown(MouseEvent obj)
    {
        if (!IsInAllBounds(obj))
        {
            DeleteSelf();
        }
    }
}