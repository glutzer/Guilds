using Newtonsoft.Json;
using ProtoBuf;
using System;

namespace Guilds;

[Flags]
[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public enum BorderFlags
{
    North = 1 << 0,
    East = 1 << 1,
    South = 1 << 2,
    West = 1 << 3
}

[JsonObject(MemberSerialization.OptIn)]
[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public struct GuildClaim
{
    [JsonProperty]
    public int guildId;
    [JsonProperty]
    public BorderFlags borderFlags;
    [JsonProperty]
    public GridPos2d position;

    public GuildClaim(GridPos2d position, int guildId)
    {
        this.position = position;
        this.guildId = guildId;
    }
}