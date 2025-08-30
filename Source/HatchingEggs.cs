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
        if( isFullAnimalCount( config, record, 0 ))
            return false;
        // TODO this counts only things on the ground
        List< Thing > eggs = map.listerThings.ThingsOfDef( thing.def );
        int totalEggCount = 0;
        foreach( Thing egg in eggs )
            if( map.areaManager.Home[ egg.Position ] )
                totalEggCount += egg.stackCount;
        // There are not enough eggs, every egg is a hatching egg.
        if( !isFullAnimalCount( config, record, totalEggCount ))
            return true;
        // Eggs in storages with allowed hatching eggs.
        int hatchingEggCount = 0;
        foreach( Thing egg in eggs )
            if( isInHatchingLocation( egg ))
                hatchingEggCount += egg.stackCount;
        // If there are enough eggs in zones that accept hatching eggs, those are hatching eggs,
        // the rest are not.
        if( isFullAnimalCount( config, record, hatchingEggCount ))
            return isInHatchingLocation( thing );
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

    private static bool isFullAnimalCount( AutoSlaughterConfig config, Dialog_AutoSlaughter.AnimalCountRecord record, int eggs )
    {
        if( config.maxTotal != -1 )
            return record.total + eggs >= config.maxTotal;
        // With 'Improved Auto Slaughter' young actually refers to all (=adults+young).
        // Without it, it makes sense only to count young ones, as newly hatches eggs cannot (immediately) replenish adults.
        // The '/ 4' is because this assumes that at least a quarter of the eggs will hatch into the expected gender.
        if( config.maxMalesYoung != -1 && config.maxFemalesYoung != -1 )
            return record.maleYoung + eggs / 4 >= config.maxMalesYoung && record.femaleYoung + eggs / 4 >= config.maxFemalesYoung;
        // Otherwise there is probably(?) no reasonable way to do this.
        return false;
    }
}
