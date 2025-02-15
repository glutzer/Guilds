using MareLib;
using OpenTK.Mathematics;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Guilds;

public class WidgetGuildPlayerInfoPopup : Widget
{
    public override int SortPriority => 1;
    private readonly string playerUid;

    public WidgetGuildPlayerInfoPopup(Widget? parent, string playerUid) : base(parent)
    {
        // Fits all children.
        SetChildSizing(ChildSizing.Height | ChildSizing.Width);

        this.playerUid = playerUid;
        int heightOffset = -8;

        GuildManager manager = MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client);
        Guild? guild = manager.guildData.GetGuild(manager.guildGui?.selectedGuildId ?? -1);

        if (guild != null && guild.GetRole(MainAPI.Capi.World.Player.PlayerUID) is RoleInfo ownRole)
        {
            if (guild.GetRole(playerUid) is RoleInfo targetRole)
            {
                if (ownRole.HasPermissions(GuildPerms.Kick) && ownRole.authority > targetRole.authority)
                {
                    new WidgetHoldDownGuildButton(this, 2, () =>
                    {
                        GuildRequestPacket packet = new()
                        {
                            targetPlayer = playerUid,
                            guildId = guild.id,
                            type = EnumGuildRequestPacket.Kick
                        };

                        manager.SendPacket(packet);
                        RemoveSelf();
                    }, $"Kick From {guild.name}", new Vector4(0.3f, 0, 0, 1), Vector4.One).Alignment(Align.LeftTop).FixedSize(64, 12).FixedY(heightOffset += 12);
                }
            }

            if (ownRole.HasPermissions(GuildPerms.Invite) && !guild.HasMember(playerUid))
            {
                if (guild.IsInvited(playerUid))
                {
                    new WidgetGuildButton(this, () =>
                    {
                        GuildRequestPacket packet = new()
                        {
                            targetPlayer = playerUid,
                            guildId = guild.id,
                            type = EnumGuildRequestPacket.CancelInvite
                        };

                        manager.SendPacket(packet);
                        RemoveSelf();
                    }, $"Cancel {guild.name} Invite", new Vector4(0.3f, 0, 0, 1), Vector4.One).Alignment(Align.LeftTop).FixedSize(64, 12).FixedY(heightOffset += 12);
                }
                else
                {
                    new WidgetGuildButton(this, () =>
                    {
                        GuildRequestPacket packet = new()
                        {
                            targetPlayer = playerUid,
                            guildId = guild.id,
                            type = EnumGuildRequestPacket.Invite
                        };

                        manager.SendPacket(packet);
                        RemoveSelf();
                    }, $"Invite To {guild.name}", new Vector4(0.3f, 0, 0, 1), Vector4.One).Alignment(Align.LeftTop).FixedSize(64, 12).FixedY(heightOffset += 12);
                }
            }

            // Roles.
            if (guild.GetRole(playerUid) is RoleInfo promoteRole && ownRole.HasPermissions(GuildPerms.Promote) && promoteRole.authority < ownRole.authority)
            {
                foreach (RoleInfo role in guild.roles.OrderByDescending(x => x.authority))
                {
                    if (role.authority >= ownRole.authority) continue;

                    if (role.id == promoteRole.id) continue;

                    new WidgetHoldDownGuildButton(this, 2, () =>
                    {
                        GuildRequestPacket packet = new()
                        {
                            targetPlayer = playerUid,
                            guildId = guild.id,
                            type = EnumGuildRequestPacket.Promote,
                            roleId = role.id
                        };

                        manager.SendPacket(packet);
                        RemoveSelf();
                    }, role.authority >= promoteRole.authority ? $"Promote To {role.name}" : $"Demote To {role.name}", new Vector4(0.3f, 0, 0, 1), Vector4.One).Alignment(Align.LeftTop).FixedSize(64, 12).FixedY(heightOffset += 12);
                }

                if (ownRole.id == 1) // Founder.
                {
                    new WidgetHoldDownGuildButton(this, 10, () =>
                    {
                        GuildRequestPacket packet = new()
                        {
                            targetPlayer = playerUid,
                            guildId = guild.id,
                            type = EnumGuildRequestPacket.Promote,
                            roleId = 1
                        };

                        manager.SendPacket(packet);
                        RemoveSelf();
                    }, $"Make Guild Leader", new Vector4(0.3f, 0, 0, 1), Vector4.One).Alignment(Align.LeftTop).FixedSize(64, 12).FixedY(heightOffset += 12);
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
            RemoveSelf();
        }
    }
}