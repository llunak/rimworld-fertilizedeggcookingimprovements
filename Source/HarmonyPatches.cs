using HarmonyLib;
using RimWorld;
using Verse;
using System.Reflection;

namespace FertilizedEggCookingImprovements;

[StaticConstructorOnStartup]
public class HarmonyPatches
{
    static HarmonyPatches()
    {
        var harmony = new Harmony("llunak.FertilizedEggCookingImprovements");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}
