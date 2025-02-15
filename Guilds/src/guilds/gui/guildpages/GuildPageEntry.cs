using MareLib;

namespace Guilds;

public class GuildPageAttribute : ClassAttribute
{
    public readonly string name;
    public readonly int priority;

    public GuildPageAttribute(string name, int priority) : base()
    {
        this.name = name;
        this.priority = priority;
    }
}

public abstract class GuildPageEntry
{
    public readonly string name;
    public readonly int priority;
    public readonly int id;
    public readonly GuildGui guildGui;
    public readonly GuildManager manager;

    public GuildPageEntry(string name, int priority, int id, GuildGui guildGui)
    {
        this.name = name;
        this.priority = priority;
        this.id = id;
        this.guildGui = guildGui;
        manager = guildGui.manager;
    }

    public virtual void Initialize()
    {

    }

    /// <summary>
    /// On creating a page when switching to it.
    /// </summary>
    public virtual void OnCreatePage(Widget parent)
    {

    }

    /// <summary>
    /// When a player has received a mirrored request from himself, and the gui is open.
    /// </summary>
    public virtual void OnPlayerReceivedOwnRequest(GuildPacket packet)
    {

    }
}