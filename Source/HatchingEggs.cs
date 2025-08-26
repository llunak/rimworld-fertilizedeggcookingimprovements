using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System;
using System.Collections.Generic;

namespace FertilizedEggCookingImprovements;

// Add a filter to storage/bills that makes it possible to prevent fertilized eggs
// that are necessary to keep the animal population.

[DefOf]
public static class DefOfs
{
    public static SpecialThingFilterDef EFI_HatchingEggs;

    static DefOfs()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(DefOfs));
    }
}

public class SpecialThingFilterWorker_HatchingEggs : SpecialThingFilterWorker
{
    // Dialog_AutoSlaughter.CountPlayerAnimals() is unfortunately not static,
    // but the constructor does basically nothing, so using a dummy is safe.
    private static Dialog_AutoSlaughter dialog = new Dialog_AutoSlaughter( null );

    public override bool Matches(Thing t)
    {
        if( !CanEverMatch( t.def ))
            return false;
        return isHatchingEgg( t );
    }
    public override bool CanEverMatch(ThingDef def)
    {
        return def.thingCategories != null && def.thingCategories.Contains(ThingCategoryDefOf.EggsFertilized);
    }

    private static bool isHatchingEgg( Thing thing )
    {
        Map map = thing.MapHeld;
        if( map == null )
            return false;
        ThingDef hatcherRace = thing.def.GetCompProperties<CompProperties_Hatcher>()?.hatcherPawn?.race;
        if( hatcherRace == null )
            return false;
        AutoSlaughterConfig config = map.autoSlaughterManager.configs.FirstOrDefault( cfg => cfg.animal == hatcherRace );
        if( config == null )
            return false;
        Dialog_AutoSlaughter.AnimalCountRecord record = default(Dialog_AutoSlaughter.AnimalCountRecord);
        dialog.CountPlayerAnimals(map, config, config.animal, out record.male, out record.maleYoung,
            out record.female, out record.femaleYoung, out record.total, out record.pregnant, out record.bonded);
        // Animals at full count, this egg is not needed for hatching.
        // With 'Improved Auto Slaughter' young actually refers to all (=adults+young).
        // TODO vanilla
        if( record.maleYoung >= config.maxMalesYoung && record.femaleYoung >= config.maxFemalesYoung )
            return false;
        // TODO this counts only things on the ground
        List< Thing > eggs = map.listerThings.ThingsOfDef( thing.def );
        int totalEggCount = 0;
        foreach( Thing egg in eggs )
            totalEggCount += egg.stackCount;
        // There are not enough eggs, every egg is a hatching egg.
        if( record.maleYoung + record.femaleYoung + totalEggCount < config.maxMalesYoung + config.maxFemalesYoung )
            return true;
        // Eggs in storages with allowed hatching eggs.
        int hatchingEggCount = 0;
        foreach( Thing egg in eggs )
            if( isInHatchingLocation( egg ))
                hatchingEggCount += egg.stackCount;
        // TODO how to handle the uncertainty that an egg may hatch into a male or female?
        if( record.maleYoung + record.femaleYoung + hatchingEggCount >= config.maxMalesYoung + config.maxFemalesYoung )
        {
            // There are enough eggs in zones that accept hatching eggs, so those are hatching eggs,
            // the rest are not.
            return isInHatchingLocation( thing );
        }
        // Enough total eggs, but not enough in zones that allow hatching eggs, consider every egg to be a hatching egg,
        // until those zones get sufficient number.
        return true;
    }

    // In a zone (or possibly storage) that allows hatching eggs. Those are considered reserved for hatching
    // additional animals if their count is not at full.
    private static bool isInHatchingLocation( Thing thing )
    {
        if( thing.ParentHolder is IStoreSettingsParent storeSettings
            && !storeSettings.GetStoreSettings().filter.disallowedSpecialFilters.Contains( DefOfs.EFI_HatchingEggs ))
        {
            return true;
        }
        Map map = thing.Map;
        if( map == null || !thing.Position.InBounds( thing.Map ))
            return false;
        if( thing.Map.zoneManager.ZoneAt( thing.Position ) is Zone_Stockpile stockpile
            && !stockpile.settings.filter.disallowedSpecialFilters.Contains( DefOfs.EFI_HatchingEggs ))
        {
            return true;
        }
        return false;
    }
}
