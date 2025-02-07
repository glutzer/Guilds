﻿using MareLib;
using OpenTK.Mathematics;
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
        string? currentGuild = manager.guildGui?.currentGuild;

        if (currentGuild != null
            && manager.guildData.GetGuild(currentGuild) is Guild guild
            && guild.GetRole(MainAPI.Capi.World.Player.PlayerUID) is RoleInfo ownRole)
        {
            if (guild.GetRole(playerUid) is RoleInfo targetRole)
            {
                if (ownRole.HasPermissions(GuildPerms.Kick) && ownRole.authority > targetRole.authority)
                {
                    new WidgetGuildButton(this, () =>
                    {
                        RemoveSelf();
                    }, $"Kick From {currentGuild}", new Vector4(0.3f, 0, 0, 1), Vector4.One).Alignment(Align.LeftTop).FixedSize(32, 8).FixedY(heightOffset += 8);
                }

                if (ownRole.HasPermissions(GuildPerms.Promote) && ownRole.authority > targetRole.authority)
                {
                    new WidgetGuildButton(this, () =>
                    {
                        RemoveSelf();
                    }, "Set Role", new Vector4(0.3f, 0, 0, 1), Vector4.One).Alignment(Align.LeftTop).FixedSize(32, 8).FixedY(heightOffset += 8);
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
                            guildName = currentGuild,
                            type = EnumGuildPacket.CancelInvite
                        };

                        manager.SendPacket(packet);
                        RemoveSelf();
                    }, $"Cancel {currentGuild} Invite", new Vector4(0.3f, 0, 0, 1), Vector4.One).Alignment(Align.LeftTop).FixedSize(32, 8).FixedY(heightOffset += 8);
                }
                else
                {
                    new WidgetGuildButton(this, () =>
                    {
                        GuildRequestPacket packet = new()
                        {
                            targetPlayer = playerUid,
                            guildName = currentGuild,
                            type = EnumGuildPacket.Invite
                        };

                        manager.SendPacket(packet);
                        RemoveSelf();
                    }, $"Invite To {currentGuild}", new Vector4(0.3f, 0, 0, 1), Vector4.One).Alignment(Align.LeftTop).FixedSize(32, 8).FixedY(heightOffset += 8);
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