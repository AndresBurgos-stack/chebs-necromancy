using ChebsValheimLibrary.Common;

namespace ChebsNecromancy.Minions.Charred
{
    internal class CharredTwitcherMinion : CharredMinion
    {
        public static MemoryConfigEntry<string, List<string>> ItemsCost;

        public static void CreateConfigs(BasePlugin plugin)
        {
            const string serverSyncedHeading = "CharredTwitcher (Server Synced)";

            var itemsCost = plugin.ModConfig(serverSyncedHeading, "ItemsCost", "BoneFragments:10,CharredBone:5",
                "The items that are consumed when creating a Charred Twitcher. Twitchers never wear armor, so holding Shift has no effect on them. Please use a comma-delimited list of prefab names with a : and integer for amount. Alternative items can be specified with a | eg. Wood|Coal:5 to mean wood and/or coal.",
                null, true);
            ItemsCost = new MemoryConfigEntry<string, List<string>>(itemsCost, s => s?.Split(',').Select(str => str.Trim()).ToList());
        }
    }
}
