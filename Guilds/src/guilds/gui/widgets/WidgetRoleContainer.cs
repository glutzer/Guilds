﻿using MareLib;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace Guilds;

public class WidgetRoleContainer : Widget
{
    public Guild? guild;
    public GuildGui guildGui;

    public WidgetRoleContainer(Widget? parent, GuildGui guildGui) : base(parent)
    {
        GuildManager.OnClientUpdate += OnUpdate;

        this.guildGui = guildGui;
        guild = guildGui.manager.guildData.GetGuild(guildGui.selectedGuildId);
        if (guild == null) return;

        RoleInfo? ownRole = guild.GetRole(guildGui.ownUid);
        if (ownRole == null) return;

        if (!ownRole.HasPermissions(GuildPerms.ManageRoles))
        {
            new WidgetTextLine(this, FontRegistry.GetFont("friz"), "No role permissions.", Vector4.One, true).Alignment(Align.Center).FixedSize(64, 32);
            return;
        }

        new RoleSelector(this, guild, ownRole)
            .Alignment(Align.LeftTop)
            .Percent(0, 0, 0.5f, 1f);
    }

    public override void Dispose()
    {
        GuildManager.OnClientUpdate -= OnUpdate;
    }

    public void OnUpdate(EnumClientGuildUpdate type, object? obj)
    {
        if (type == EnumClientGuildUpdate.GuildRolesChanged)
        {
            List<Widget> selector = children.Where(t => t is RoleSelector).ToList();

            if (selector.Count == 0) return;
            foreach (Widget select in selector)
            {
                select.RemoveSelf();
            }

            RoleInfo? ownRole = guild?.GetRole(guildGui.ownUid);
            if (ownRole == null || guild == null) return;

            if (!ownRole.HasPermissions(GuildPerms.ManageRoles))
            {
                new WidgetTextLine(this, FontRegistry.GetFont("friz"), "No role permissions.", Vector4.One, true).Alignment(Align.Center).FixedSize(64, 32);
                return;
            }

            new RoleSelector(this, guild, ownRole)
            .Alignment(Align.LeftTop)
            .Percent(0, 0, 0.5f, 1f);

            SetBounds();
            guildGui.MarkForRepartition();
        }
    }
}

/// <summary>
/// Left side, click a role button to release the old one.
/// </summary>
public class RoleSelector : Widget
{
    private int selectedRoleIndex = 0;
    private readonly WidgetToggleableButton[] roleSelectionButtons;
    private readonly PermissionSelector? permissionSelector;
    private Gui? gui;

    public RoleSelector(Widget? parent, Guild guild, RoleInfo ownRole) : base(parent)
    {
        roleSelectionButtons = new WidgetToggleableButton[guild.roles.Length];
        SetChildSizing(ChildSizing.Height | ChildSizing.Once);

        RoleInfo[] roles = guild.roles;

        GuildManager manager = MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client);

        // Add side by side add/remove role buttons.
        new WidgetGuildButton(this, () =>
        {
            GuildRequestPacket packet = new()
            {
                type = EnumGuildRequestPacket.AddRole,
                guildId = guild.id
            };

            manager.SendPacket(packet);
        }, "Add Role", GuiThemes.ButtonColor, GuiThemes.ButtonFontColor).Alignment(Align.CenterTop).Fixed(-16, 0, 32, 12);

        new WidgetGuildButton(this, () =>
        {
            GuildRequestPacket packet = new()
            {
                type = EnumGuildRequestPacket.RemoveRole,
                guildId = guild.id,
                roleId = selectedRoleIndex
            };

            manager.SendPacket(packet);
        }, "Remove Role", GuiThemes.ButtonColor, GuiThemes.ButtonFontColor).Alignment(Align.CenterTop).Fixed(16, 0, 32, 12);

        for (int i = 0; i < roleSelectionButtons.Length; i++)
        {
            RoleInfo role = roles[i];

            int indexOfThis = i;

            Vector4 color = GuiThemes.ButtonColor;
            if (role.authority >= ownRole.authority) color = new(0.1f, 0.1f, 0.1f, 1);

            roleSelectionButtons[i] = (WidgetToggleableButton)new WidgetToggleableButton(this, (up) =>
            {
                roleSelectionButtons[selectedRoleIndex].Release();
                selectedRoleIndex = indexOfThis;
                UpdatePermissions(role, guild.id);
            }, role.name, color).Alignment(Align.CenterTop).Fixed(0, (i * 12) + 24, 64, 12);

            // Button will never be able to be selected or let up now.
            if (role.authority >= ownRole.authority) roleSelectionButtons[i].LockDown();
        }
    }

    public override void RegisterEvents(GuiEvents guiEvents)
    {
        gui = guiEvents.gui;
    }

    public void UpdatePermissions(RoleInfo roleInfo, int guildId)
    {
        permissionSelector?.RemoveSelf();

        new PermissionSelector(this, new RoleData(roleInfo, guildId))
            .Alignment(Align.RightTop, AlignFlags.OutsideH)
            .Percent(0, 0, 1, 1)
            .SetChildSizing(ChildSizing.Height | ChildSizing.Once);

        SetBounds();
        gui?.MarkForRepartition();
    }
}

public class RoleData
{
    public int guildId;
    public int roleId;
    public string newName;
    public GuildPerms guildPerms;
    public int authority;

    public RoleData(RoleInfo roleInfo, int guildId)
    {
        this.guildId = guildId;
        roleId = roleInfo.id;
        newName = roleInfo.name;
        guildPerms = roleInfo.GetPermissions();
        authority = roleInfo.authority;
    }
}

public class PermissionSelector : Widget
{
    public RoleData roleData;

    public PermissionSelector(Widget? parent, RoleData roleData) : base(parent)
    {
        this.roleData = roleData;

        int index = 0;

        new WidgetGuildLabeledInput(this, roleData.newName, "Name: ", (s) => roleData.newName = s, (s) => s.Length < 50)
            .Alignment(Align.CenterTop)
            .Fixed(0, index * 8, 64, 8);

        index++;

        // Add a button for each enum in guildperms.
        foreach (GuildPerms enumType in Enum.GetValues(typeof(GuildPerms)))
        {
            WidgetToggleableButton button = (WidgetToggleableButton)new WidgetToggleableButton(this, (up) =>
            {
                if (up)
                {
                    roleData.guildPerms |= enumType;
                }
                else
                {
                    roleData.guildPerms &= ~enumType;
                }
            }, enumType.ToString(), GuiThemes.ButtonColor, false)
                .Alignment(Align.CenterTop)
                .Fixed(0, index * 8, 64, 8);

            if (roleData.guildPerms.HasFlag(enumType)) button.LockDown();

            index++;
        }

        new WidgetGuildLabeledInput(this, roleData.authority.ToString(), "Authority: ", (s) => roleData.authority = int.Parse(s), (s) => int.TryParse(s, out _))
            .Alignment(Align.CenterTop)
            .Fixed(0, index * 8, 64, 8);

        index++;

        new WidgetGuildButton(this, () =>
        {
            RoleUpdatePacket packet = new()
            {
                guildId = roleData.guildId,
                roleId = roleData.roleId,
                newName = roleData.newName,
                newPerms = roleData.guildPerms,
                newAuthority = roleData.authority
            };

            MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client).SendPacket(packet);
        }, "Apply Roles", GuiThemes.ButtonColor, GuiThemes.ButtonFontColor)
            .Alignment(Align.CenterTop)
            .Fixed(0, (index * 8) + 12, 64, 12);
    }
}