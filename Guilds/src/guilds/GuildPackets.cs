using OpenTK.Mathematics;
using ProtoBuf;
using Vintagestory.API.Util;

namespace Guilds;

/// <summary>
/// Send from client -> server to update a role.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class RoleUpdatePacket
{
    public int guildId;
    public int roleId;
    public string? newName;
    public int newAuthority;
    public GuildPerms newPerms;

    /// <summary>
    /// Only set on server when sending back to clients.
    /// </summary>
    public string? fromUid;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public enum EnumGuildRequestPacket
{
    Invite,
    CancelInvite,
    AcceptInvite,
    Kick,
    Promote,
    AddRole,
    RemoveRole,
    Create,
    Leave,
    Disband,
    RepGuild
}

/// <summary>
/// Send from client -> server for a request.
/// Broadcasted back to clients.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class GuildRequestPacket
{
    public EnumGuildRequestPacket type;
    public string? targetPlayer;
    public int roleId;
    public int guildId;
    public byte[]? data;

    /// <summary>
    /// Only set on server when sending back to clients.
    /// </summary>
    public string? fromUid;

    public T? ReadData<T>()
    {
        if (data == null) return default;
        return SerializerUtil.Deserialize<T>(data);
    }
}

/// <summary>
/// Send from client -> server to update guild info.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class GuildInfoPacket
{
    public int guildId;
    public string? name;
    public Vector3 color;

    /// <summary>
    /// Only set on server when sending back to clients.
    /// </summary>
    public string? fromUid;
}