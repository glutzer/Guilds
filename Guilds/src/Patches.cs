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
            GridPos chunkPos = new(pos.X / 32, 0, pos.Z / 32);

            GuildData guildData = MainAPI.GetGameSystem<GuildManager>(forPlayer.Entity.Api.Side).guildData;


            // Get claim data about this chunk.

            // If the data exists and the player is not in that group: no permission.
            if (data != null)
            {
                if (forPlayer.GetGroup(data.groupUid) == null)
                {
                    __result = "Claimed by group";
                    return false;
                }
            }

            __result = null;
            return false;
        }
    }
}