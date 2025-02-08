using HarmonyLib;
using MareLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Common;

namespace Guilds;

public class Patches
{
    // Rewrite this to make player unable to touch claimed chunks instead.
    [HarmonyPatch(typeof(WorldMap))]
    [HarmonyPatch("GetBlockingLandClaimant")]
    public static class ClaimPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref string __result, IPlayer forPlayer, BlockPos pos, EnumBlockAccessFlags accessFlag)
        {
            GridPos2d chunkPos = new(pos.X / 32, pos.Z / 32);

            ClaimManager claimManager = MainAPI.GetGameSystem<ClaimManager>(forPlayer.Entity.Api.Side);

            if (claimManager.claimData.TryGetClaim(chunkPos, out GuildClaim claim))
            {
                Guild? guild = claimManager.guildManager.guildData.GetGuild(claim.guildId);
                if (guild == null) return false;

                RoleInfo? roleInfo = guild.GetRole(forPlayer.PlayerUID);
                if (roleInfo == null)
                {
                    __result = $"guild {guild.Name}";
                    return false;
                }

                if (accessFlag == EnumBlockAccessFlags.Use && !roleInfo.HasPermissions(GuildPerms.UseBlocks))
                {
                    __result = $"{guild.Name} has not granted use permissions";
                    return false;
                }

                if (accessFlag == EnumBlockAccessFlags.BuildOrBreak && !roleInfo.HasPermissions(GuildPerms.BreakBlocks))
                {
                    __result = $"{guild.Name} has not granted build permissions";
                    return false;
                }
            }

            return false;
        }
    }
}