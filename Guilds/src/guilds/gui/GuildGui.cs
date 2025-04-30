using MareLib;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace Guilds;

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
    public GuildManager manager;
    public string ownUid;

    public override bool UnregisterOnClose => false;
    public Widget? contentContainer; // Needed to refresh content.
    public WidgetGuildScrollBar? scrollBar; // Needed to reset.

    public int currentPage = 0;
    public int selectedGuildId = -1;

    public readonly List<GuildPageEntry> pages = new();

    public GuildGui()
    {
        manager = MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client);
        ownUid = MainAPI.Capi.World.Player.PlayerUID;

        (Type, GuildPageAttribute)[] pageTypes = AttributeUtilities.GetAllAnnotatedClasses<GuildPageAttribute>();

        int index = 0;
        foreach ((Type type, GuildPageAttribute attribute) in pageTypes)
        {
            GuildPageEntry entry = (GuildPageEntry)Activator.CreateInstance(type, new object[] { attribute.name, attribute.priority, index++, this })!;
            pages.Add(entry);
        }

        pages = pages.OrderByDescending(x => x.priority).ThenBy(x => x.name).ToList();
        foreach (GuildPageEntry page in pages) page.Initialize();

        manager.OnClientReceivedGuildPacket += p =>
        {
            if (!IsOpened()) return;

            if (p.fromUid == ownUid)
            {
                pages[currentPage].OnPlayerReceivedOwnRequest(p);
            }
        };

        manager.OnPlayersGuildsChanged += SetWidgets;
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

        pages[currentPage].OnCreatePage(contentContainer);
        MarkForRepartition();
    }

    public override void PopulateWidgets()
    {
        WidgetSliceBackground bg = new(null, GuiThemes.Background, new Vector4(0.2f, 0.2f, 0.2f, 1));
        AddWidget(bg.Fixed(0, 0, 200, 200).Alignment(Align.Center));

        List<WidgetGuildTab> tabs = new();
        int index = 0;
        foreach (GuildPageEntry entry in pages)
        {
            int i = index;
            new WidgetGuildTab(bg, (on) =>
            {
                foreach (WidgetGuildTab tab in tabs) tab.Release();
                SwapToPage(i);
            }, true, new Vector4(0.5f, 0, 0, 1), entry.name)
                .Fixed(0, Scaled(index * 12), 50, 12)
                .Alignment(Align.LeftTop, AlignFlags.OutsideH)
                .As(out WidgetGuildTab guildTab);
            index++;
            tabs.Add(guildTab);

            if (i == currentPage) guildTab.SetDown();
        }
        tabs[currentPage].SetDown();

        new WidgetClip(true, bg).Fill();

        // Container that will hold stuff in the tabs.
        contentContainer = new WidgetContainer(bg).Fill().SetChildSizing(ChildSizing.Height);

        // Add page content.
        RefreshPage();

        new WidgetClip(false, bg).Fill();

        scrollBar = (WidgetGuildScrollBar)new WidgetGuildScrollBar(bg, contentContainer).Alignment(Align.RightMiddle, AlignFlags.OutsideH).PercentHeight(1).FixedWidth(8);

        GuildManager manager = MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client);

        HashSet<int> guilds = manager.guildData.GetPlayersGuilds(MainAPI.Capi.World.Player.PlayerUID);

        index = 0;
        List<WidgetGuildTab> guildTabs = new();
        foreach (int guildId in guilds)
        {
            Guild? guild = manager.guildData.GetGuild(guildId);
            if (guild == null) continue;

            if (guild.name == null)
            {
                Console.WriteLine($"Guild id {guildId} had null name.");
                continue;
            }

            WidgetGuildTab newTab = (WidgetGuildTab)new WidgetGuildTab(bg, (on) =>
            {
                if (on)
                {
                    foreach (WidgetGuildTab tab in guildTabs) tab.Release();
                    selectedGuildId = guildId;
                }
                else
                {
                    selectedGuildId = -1;
                }

                RefreshPage();
            }, false, new Vector4(guild.Color, 1), guild.name, true).Fixed(Scaled(8), Scaled(index * 12), 50, 12).Alignment(Align.RightTop, AlignFlags.OutsideH);
            index++;
            guildTabs.Add(newTab);

            if (guild.id == selectedGuildId) newTab.SetDown();
        }
    }
}