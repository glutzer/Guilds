using Vintagestory.API.Common;

namespace Guilds;

public class WidgetRoleContainer : Widget
{
    public Guild? guild;
    public GuildGui guildGui;

    public WidgetRoleContainer(Widget? parent, GuildGui guildGui) : base(parent, guildGui)
    {
        this.guildGui = guildGui;
        guild = guildGui.manager.guildData.GetGuild(guildGui.selectedGuildId);
        if (guild == null) return;

        RoleInfo? ownRole = guild.GetRole(guildGui.ownUid);
        if (ownRole == null) return;

        if (!ownRole.HasPermissions(GuildPerms.ManageRoles))
        {
            new WidgetTextLine(this, guildGui, VanillaThemes.Font, "No role permissions.", VanillaThemes.WhitishTextColor, true).Alignment(Align.Center).FixedSize(64, 32).FixedPos(0, 128);
            return;
        }

        new RoleSelector(this, guildGui, guild, ownRole)
            .Alignment(Align.LeftTop)
            .Percent(0f, 0f, 0.5f, 1f);
    }
}

/// <summary>
/// Left side, click a role button to release the old one.
/// </summary>
public class RoleSelector : Widget
{
    private int selectedRoleIndex = 0;
    private readonly WidgetToggleableButton[] roleSelectionButtons;
    private PermissionSelector? permissionSelector;

    public RoleSelector(Widget? parent, Gui gui, Guild guild, RoleInfo ownRole) : base(parent, gui)
    {
        roleSelectionButtons = new WidgetToggleableButton[guild.roles.Length];
        SetChildSizing(ChildSizing.Height | ChildSizing.Once);

        RoleInfo[] roles = guild.roles;

        GuildManager manager = MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client);

        // Add side by side add/remove role buttons.
        new WidgetVanillaButton(this, gui, () =>
        {
            GuildPacket packet = new()
            {
                type = EnumGuildPacket.AddRole,
                guildId = guild.id
            };

            manager.SendPacket(packet);
        }, "Add Role").Alignment(Align.CenterTop).Fixed(Gui.Scaled(-16), 0, 32, 12);

        new WidgetVanillaButton(this, gui, () =>
        {
            GuildPacket packet = new()
            {
                type = EnumGuildPacket.RemoveRole,
                guildId = guild.id,
                roleId = selectedRoleIndex
            };

            manager.SendPacket(packet);
        }, "Remove Role").Alignment(Align.CenterTop).Fixed(Gui.Scaled(16), 0, 32, 12);

        for (int i = 0; i < roleSelectionButtons.Length; i++)
        {
            RoleInfo role = roles[i];

            int indexOfThis = i;

            roleSelectionButtons[i] = (WidgetToggleableButton)new WidgetToggleableButton(this, gui, (up) =>
            {
                roleSelectionButtons[selectedRoleIndex].Release();
                selectedRoleIndex = indexOfThis;
                UpdatePermissions(role, guild.id);
            }, role.name, role.authority >= ownRole.authority).Alignment(Align.CenterTop).Fixed(0, Gui.Scaled((i * 12) + 24), 64, 12);

            // Button will never be able to be selected or let up now.
            if (role.authority >= ownRole.authority) roleSelectionButtons[i].LockDown();
        }
    }

    public void UpdatePermissions(RoleInfo roleInfo, int guildId)
    {
        permissionSelector?.DeleteSelf();

        permissionSelector = (PermissionSelector)new PermissionSelector(this, Gui, new RoleData(roleInfo, guildId))
            .Alignment(Align.RightTop, AlignFlags.OutsideH)
            .Percent(0f, 0f, 1f, 1f)
            .SetChildSizing(ChildSizing.Height | ChildSizing.Once);
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

    public PermissionSelector(Widget? parent, Gui gui, RoleData roleData) : base(parent, gui)
    {
        this.roleData = roleData;

        int index = 0;

        new WidgetGuildLabeledInput(this, gui, roleData.newName, "Name: ", (s) => roleData.newName = s, (s) => s.Length < 50)
            .Alignment(Align.CenterTop)
            .Fixed(0, Gui.Scaled(index * 8), 64, 8);

        index++;

        // Add a button for each enum in guildperms.
        foreach (GuildPerms enumType in Enum.GetValues(typeof(GuildPerms)))
        {
            WidgetToggleableButton button = (WidgetToggleableButton)new WidgetToggleableButton(this, gui, (up) =>
            {
                if (up)
                {
                    roleData.guildPerms |= enumType;
                }
                else
                {
                    roleData.guildPerms &= ~enumType;
                }
            }, enumType.ToString(), false)
                .Alignment(Align.CenterTop)
                .Fixed(0, Gui.Scaled(index * 8), 64, 8);

            if (roleData.guildPerms.HasFlag(enumType)) button.LockDown();

            index++;
        }

        new WidgetGuildLabeledInput(this, gui, roleData.authority.ToString(), "Authority: ", (s) => roleData.authority = int.Parse(s), (s) => int.TryParse(s, out _))
            .Alignment(Align.CenterTop)
            .Fixed(0, Gui.Scaled(index * 8), 64, 8);

        index++;

        new WidgetVanillaButton(this, gui, () =>
        {
            RoleUpdateInfo packet = new()
            {
                newName = roleData.newName,
                newPerms = roleData.guildPerms,
                newAuthority = roleData.authority
            };

            GuildPacket guildPacket = GuildPacket.Create(EnumGuildPacket.UpdateRole, packet, null, roleData.guildId, roleData.roleId);

            MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client).SendPacket(guildPacket);
        }, "Apply Roles")
            .Alignment(Align.CenterTop)
            .Fixed(0, Gui.Scaled((index * 8) + 12), 64, 12);
    }
}