using Guilds.src.guilds.gui.widgets;
using MareLib;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace Guilds;

public class PageEntry
{
    // Name to be displayed on the tab.
    public string name;

    // Delegate to populate the tab.
    public Action<Widget, GuildGui> populationDelegate;

    public PageEntry(string name, Action<Widget, GuildGui> populationDelegate)
    {
        this.name = name;
        this.populationDelegate = populationDelegate;
    }
}

public class GuildMemberInfo
{
    public struct RoleData
    {
        public int authority;
        public string name;
    }

    public PlayerMetrics Metrics { get; set; }
    public RoleData Role { get; set; }

    public GuildMemberInfo(PlayerMetrics metrics, RoleData roleData)
    {
        Metrics = metrics;
        Role = roleData;
    }
}

public class GuildGui : Gui
{
    public override bool UnregisterOnClose => false;
    public Widget? contentContainer;
    public WidgetGuildScrollBar? scrollBar;
    public readonly List<PageEntry> pages = new();

    public int currentPage = 0;
    public string? currentGuild;

    public GuildManager manager;
    public string ownUid;

    public GuildGui()
    {
        manager = MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client);
        ownUid = MainAPI.Capi.World.Player.PlayerUID;

        pages.Add(new PageEntry("Create Guild", (widget, gui) =>
        {
            Widget bg = new WidgetSliceBackground(widget, GuiThemes.Title, new Vector4(0.1f, 0.1f, 0.1f, 1))
            .Alignment(Align.Center)
            .Fixed(0, 0, 64, 12);

            WidgetTextBoxSingle textBox = (WidgetTextBoxSingle)new WidgetTextBoxSingle(bg, FontRegistry.GetFont("friz"), Vector4.One)
            .Alignment(Align.Center)
            .Fixed(0, 0, 64, 12);

            new WidgetGuildButton(textBox, () =>
            {
                GuildRequestPacket packet = new()
                {
                    type = EnumGuildPacket.Create,
                    guildName = textBox.text.Text
                };

                MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client).SendPacket(packet);
            }, "Create Guild", new Vector4(0.5f, 0, 0.7f, 1), Vector4.One).Alignment(Align.CenterBottom, AlignFlags.OutsideV).Fixed(0, 0, 64, 12);
        }));

        pages.Add(new PageEntry("Guild Info", (widget, gui) =>
        {
            if (currentGuild == null)
            {
                new WidgetTextLine(contentContainer, FontRegistry.GetFont("friz"), "No guild selected.", Vector4.One).Alignment(Align.Center)
                .PercentWidth(1)
                .FixedHeight(12);

                return;
            }

            Guild? guild = manager.guildData.GetGuild(currentGuild);

            if (guild == null) return;

            new WidgetTextLine(contentContainer, FontRegistry.GetFont("friz"), guild.Name, Vector4.One)
                    .Alignment(Align.CenterTop)
                    .PercentWidth(1)
                    .FixedHeight(12);

            RoleInfo? roleInfo = manager.guildData.GetPlayersRole(currentGuild, MainAPI.Capi.World.Player.PlayerUID);
            if (roleInfo?.id == 1)
            {
                new WidgetGuildButton(widget, () =>
                {
                    GuildRequestPacket packet = new()
                    {
                        type = EnumGuildPacket.Disband,
                        guildName = currentGuild
                    };
                    MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client).SendPacket(packet);
                }, "Disband Guild", GuiThemes.ButtonColor, GuiThemes.ButtonFontColor).Alignment(Align.CenterTop).Fixed(0, 12, 32, 12);
            }
            else
            {
                new WidgetGuildButton(widget, () =>
                {
                    GuildRequestPacket packet = new()
                    {
                        type = EnumGuildPacket.Leave,
                        guildName = currentGuild
                    };
                    MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client).SendPacket(packet);
                }, "Leave Guild", GuiThemes.ButtonColor, GuiThemes.ButtonFontColor).Alignment(Align.CenterTop).Fixed(0, 12, 32, 12);
            }
        }));

        pages.Add(new PageEntry("Guild Members", (widget, gui) =>
        {
            if (currentGuild == null)
            {
                new WidgetTextLine(contentContainer, FontRegistry.GetFont("friz"), "No guild selected.", Vector4.One)
                    .Alignment(Align.Center)
                    .PercentWidth(1)
                    .FixedHeight(12);

                return;
            }

            Guild? guild = manager.guildData.GetGuild(currentGuild);
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

            Column<GuildMemberInfo> nameColumn = new("Name", 1f, (member) => member.Metrics.lastName, (a, b) => a.Metrics.lastName.CompareTo(b.Metrics.lastName));
            Column<GuildMemberInfo> onlineColumn = new("Online", 0.5f, (member) => member.Metrics.GetLastOnlineString(), (a, b) => b.Metrics.lastOnline.CompareTo(a.Metrics.lastOnline));
            Column<GuildMemberInfo> roleColumn = new("Role", 1f, (member) => member.Role.name, (a, b) => b.Role.authority.CompareTo(a.Role.authority));

            new WidgetSortableTable<GuildMemberInfo>(widget, info, (member, field) =>
            {
                new WidgetGuildPlayerInfoPopup(field, member.Metrics.uid)
                .Alignment(Align.LeftTop)
                .FixedSize(12, 8)
                .FixedPos((MouseX - field.X) / MainAPI.GuiScale, (MouseY - field.Y) / MainAPI.GuiScale);
                MarkForRepartition();
                field.SetBounds();
            }, nameColumn, onlineColumn, roleColumn).Alignment(Align.CenterTop).Percent(0, 0, 0.8f, 0.05f).FixedHeight(12);
        }));

        pages.Add(new PageEntry("Guild Roles", (widget, gui) =>
        {
            if (currentGuild == null)
            {
                new WidgetTextLine(contentContainer, FontRegistry.GetFont("friz"), "No guild selected.", Vector4.One)
                .Alignment(Align.Center)
                .PercentWidth(1)
                .FixedHeight(12);

                return;
            }

            SetupRoleGui(widget, gui);
        }));

        pages.Add(new PageEntry("Players", (widget, gui) =>
        {
            Column<PlayerMetrics> nameColumn = new("Name", 1f, (member) => member.lastName, (a, b) => a.lastName.CompareTo(b.lastName));
            Column<PlayerMetrics> onlineColumn = new("Online", 0.5f, (member) => member.GetLastOnlineString(), (a, b) => b.lastOnline.CompareTo(a.lastOnline));

            List<PlayerMetrics> metrics = MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client).guildData.AllMetrics.OrderByDescending(x => x.lastOnline).ToList();

            new WidgetSortableTable<PlayerMetrics>(widget, metrics, (member, field) =>
            {
                new WidgetGuildPlayerInfoPopup(field, member.uid)
                .Alignment(Align.LeftTop)
                .FixedSize(12, 8)
                .FixedPos((MouseX - field.X) / MainAPI.GuiScale, (MouseY - field.Y) / MainAPI.GuiScale);
                MarkForRepartition();
                field.SetBounds();
            }, nameColumn, onlineColumn).Alignment(Align.CenterTop).Percent(0, 0, 0.8f, 0.05f).FixedHeight(12);
        }));

        pages.Add(new PageEntry("Invites", (widget, gui) =>
        {
            List<string> guildInvites = manager.guildData.GetPlayersInvites(ownUid).ToList();
            guildInvites.Sort();

            int index = 0;

            foreach (string invite in guildInvites)
            {
                // Add button to accept or deny invite.
                new WidgetGuildButton(widget, () =>
                {
                    GuildRequestPacket packet = new()
                    {
                        type = EnumGuildPacket.AcceptInvite,
                        guildName = invite
                    };
                    manager.SendPacket(packet);
                }, $"Join {invite}", new Vector4(0.3f, 0.5f, 0.3f, 1), Vector4.One).Alignment(Align.CenterTop).Fixed(-32, index * 12, 64, 12);

                new WidgetGuildButton(widget, () =>
                {
                    GuildRequestPacket packet = new()
                    {
                        type = EnumGuildPacket.AcceptInvite,
                        guildName = invite
                    };
                    manager.SendPacket(packet);
                }, $"Deny", new Vector4(0.5f, 0.3f, 0.3f, 1), Vector4.One).Alignment(Align.CenterTop).Fixed(32, index * 12, 64, 12);
            }

            if (guildInvites.Count == 0)
            {
                new WidgetTextLine(contentContainer, FontRegistry.GetFont("friz"), "No invites received.", Vector4.One)
                    .Alignment(Align.Center)
                    .PercentWidth(1)
                    .FixedHeight(12);
            }
        }));
    }

    public void SwapToPage(int index)
    {
        if (contentContainer == null) return;
        currentPage = index;
        RefreshPage();
    }

    public void RefreshPage()
    {
        if (contentContainer == null) return;
        contentContainer.ClearChildren();
        scrollBar?.Reset();

        pages[currentPage].populationDelegate(contentContainer, this);
        contentContainer.SetBounds();
        MarkForRepartition();
    }

    public override void PopulateWidgets()
    {
        // Reset gui when re-populating, because a guild has been added or removed if this happened.
        currentPage = 0;
        currentGuild = null;

        WidgetSliceBackground bg = new(null, GuiThemes.Background, new Vector4(0.2f, 0.2f, 0.2f, 1));
        AddWidget(bg.Fixed(0, 0, 200, 200).Alignment(Align.Center));

        List<WidgetGuildTab> tabs = new();
        int index = 0;
        foreach (PageEntry entry in pages)
        {
            int i = index;
            WidgetGuildTab guildTab = (WidgetGuildTab)new WidgetGuildTab(bg, (on) =>
            {
                foreach (WidgetGuildTab tab in tabs) tab.Release();
                SwapToPage(i);
            }, true, new Vector4(0.5f, 0, 0, 1), entry.name).Fixed(0, index * 12, 50, 12).Alignment(Align.LeftTop, AlignFlags.OutsideH);
            index++;
            tabs.Add(guildTab);
        }
        tabs[currentPage].SetDown();

        new WidgetClip(true, bg).Fill();

        // Container that will hold stuff in the tabs.
        contentContainer = new WidgetDummy(bg).Fill().SetChildSizing(ChildSizing.Height);

        // Add page content.
        RefreshPage();

        new WidgetClip(false, bg).Fill();

        scrollBar = (WidgetGuildScrollBar)new WidgetGuildScrollBar(bg, contentContainer, new Vector4(0.5f, 0, 0, 1)).Alignment(Align.RightMiddle, AlignFlags.OutsideH).PercentHeight(1).FixedWidth(8);

        GuildManager manager = MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client);

        HashSet<string> guilds = manager.guildData.GetPlayersGuilds(MainAPI.Capi.World.Player.PlayerUID);

        index = 0;
        List<WidgetGuildTab> guildTabs = new();
        foreach (string guildName in guilds)
        {
            WidgetGuildTab newTab = (WidgetGuildTab)new WidgetGuildTab(bg, (on) =>
            {
                if (on)
                {
                    foreach (WidgetGuildTab tab in guildTabs) tab.Release();
                    currentGuild = guildName;
                }
                else
                {
                    currentGuild = null;
                }

                RefreshPage();
            }, false, new Vector4(0, 0, 0.5f, 1), guildName, true).Fixed(8, index * 12, 50, 12).Alignment(Align.RightTop, AlignFlags.OutsideH);
            index++;
            guildTabs.Add(newTab);
        }
    }

    public static void SetupRoleGui(Widget parent, GuildGui gui)
    {
        // Make a container, centered on the top of the gui, which is 100% the width of it.
        // It will resize to fit all of it's children, or atleast 64.
        new WidgetRoleContainer(parent, gui)
            .Alignment(Align.CenterTop)
            .FixedHeight(16)
            .PercentWidth(1)
            .SetChildSizing(ChildSizing.Height);
    }
}