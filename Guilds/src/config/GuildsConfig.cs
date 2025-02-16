using Newtonsoft.Json;
using ProtoBuf;
using System.IO;
using Vintagestory.API.Config;

namespace Guilds;

/// <summary>
/// Server-side config for guilds.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class GuildsConfig
{
    public static void LoadInstance()
    {
        string guildFile = Path.Combine(GamePaths.DataPath, "guildconfig.json");
        if (File.Exists(guildFile))
        {
            try
            {
                Instance = JsonConvert.DeserializeObject<GuildsConfig>(File.ReadAllText(guildFile)) ?? new GuildsConfig();
            }
            catch
            {
                Instance = new GuildsConfig();
            }
        }
        else
        {
            Instance = new GuildsConfig();
        }
    }

    public static void SaveInstance()
    {
        if (Instance == null) return;
        string guildFile = Path.Combine(GamePaths.DataPath, "guildconfig.json");
        File.WriteAllText(guildFile, JsonConvert.SerializeObject(Instance));
    }

    public static void SetClientInstance(GuildsConfig config)
    {
        Instance = config;
    }

    public static GuildsConfig Instance { get; private set; } = null!;

    // Max guilds player may join.
    public int maxGuildsPerPlayer = 10;

    // Max guilds player may create. A player may not be promoted to guild leader if he is one already.
    public int maxGuildCreationsPerPlayer = 1;

    // Distance a player must be to a chunk to claim it.
    public int claimRadius = 64;

    // Player may only claim adjacent chunks, if a guild has claimed no chunks.
    public bool onlyClaimAdjacents = true;

    // Item paid to claim one chunk.
    public string claimItemCode = "game:gear-temporal";
    public int claimItemCount = 1;

    // Guilds can't exceed this claim count.
    public int totalMaximumClaims = int.MaxValue;
}