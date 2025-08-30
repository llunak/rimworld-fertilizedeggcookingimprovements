using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System;

namespace FertilizedEggCookingImprovements;

// Remember when an egg box was last emptied, and allow emptying after 2 days
// even if the egg box is not full.
// All vanilla animals have hatcherDaystoHatch at least 3, so this makes it very
// unlikely eggs in an egg box hatch.
public sealed class CompEggContainerTimeout : ThingComp
{
    public int lastEmptied;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        if(!respawningAfterLoad)
            lastEmptied = GenTicks.TicksGame;
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref lastEmptied, "FertilizedEggCookingImprovements.lastEmptied", 0);
    }
}

[HarmonyPatch(typeof(CompEggContainer))]
public static class CompEggContainer_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(CanEmpty))]
    [HarmonyPatch(MethodType.Getter)]
    public static bool CanEmpty(bool result, CompEggContainer __instance)
    {
        if(result)
            return true;
        if(__instance.Empty)
            return false;
        Thing thing = __instance.ContainedThing;
        // Fertilized eggs once a day, unfertilized ones once each 5 days.
        int maxDays = thing.def.thingCategories.Contains(ThingCategoryDefOf.EggsFertilized) ? 1 : 5;
        CompEggContainerTimeout comp = __instance.parent.GetComp<CompEggContainerTimeout>();
        if( comp == null )
            return false;
        // Allow if last emptied at least given days ago.
        return comp.lastEmptied + GenDate.TicksPerDay * maxDays < GenTicks.TicksGame;
    }
}

[HarmonyPatch(typeof(ThingOwner))]
public static class ThingOwner_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NotifyRemoved))]
    public static void NotifyRemoved(IThingHolder ___owner)
    {
        if( ___owner is CompEggContainer compEggContainer )
        {
            CompEggContainerTimeout comp = compEggContainer.parent.GetComp<CompEggContainerTimeout>();
            if( comp != null )
                comp.lastEmptied = GenTicks.TicksGame;
        }
    }
}
