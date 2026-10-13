using StardewValley;
using StardewModdingAPI;
using StardewValley.Extensions;
using StardewValley.Locations;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;

namespace MineTreasureFramework
{
    /**
    * Patch class used by Harmony
    * Contains a duplicate version of Stardewvalley.Locations.MineShaft.GetReplacementChestItem(int floor).
    */
    public class MineTreasureFrameworkPatches
    {
        private static ModEntry? context;

        public static void Initialize(ModEntry modEntry)
        {
            context = modEntry;
        }

        /**
        * Duplicate version of Stardewvalley.Locations.MineShaft.GetReplacementChestItem(int floor).
        * Original version handles the "remixed" mine treasures, by creating 4 to 6 possible treasures to replace a specified treasure floor's treasure.
        * Original version will not be executed if this is a local mine floor >= 1 and <= 119.
        * this duplicate version let MineTreasureFramework content mods specify different possible treasures and return one of them.
        * It handles non-remixed mine treasures (code from Stardewvalley.Locations.MineShaft.GetReplacementChestItem) but only on moddable floors.
        * int floor : floor level to change treasure pool from. Needs to be > 0 and < 120 or this prefix method will end. Floor 120 cannot be modified.
        */
        public static bool GetReplacementChestItem_Prefix(ref Item? __result, int floor)
        {
            
            //Mod error situation
            if(context is null) return true;

            //In case of non-configurable floor lets go back to non-modded code
            if(floor < 1 || floor >= 120){
                return true;
            }

            //This part of original code will always be skipped with MTF
            //So lets do it now
            switch (floor)
            {
                case 5:
                    Game1.player.completeQuest("14");
                    if (!Game1.player.hasOrWillReceiveMail("guildQuest"))
                    {
                        Game1.addMailForTomorrow("guildQuest");
                    }
                    break;
                case 40:
                    Game1.player.completeQuest("17");
                    break;
            }

            //New code from MineTreasureFramework : handling changes requested from json content mod
            List<Item>? modifiedList = context.getPossibleTreasuresAfterMTFAlterations(floor);
            //--

            //Choosing from the list
            if (modifiedList != null && modifiedList.Count > 0)
            {
                __result =  Utility.CreateRandom((double)Game1.uniqueIDForThisGame * 512.0, floor).ChooseFrom(modifiedList);
                //context.Monitor.Log($"Chosen item : {__result.itemId}.", LogLevel.Error);
            }
            else __result = null;

            return false;
        }

        /**
        * Harmony prefix method that will prevent the creation of a treasure chest on this level if all treasure possibilities have bee cleared
        * Without this prefix, original addLevelChests would create a chest with default vanilla treasure if the list of possible treasures is empty
        */
        public static bool addLevelChests_Prefix(MineShaft __instance){
            int floor = __instance.mineLevel;
            if(context != null) {
                foreach (MTFModifiedFloor ModifiedFloor in context.getListOfFloors()){
                    if(ModifiedFloor.Floor == floor){

                        List<Item>? PossibleTreasures = context.getPossibleTreasuresAfterMTFAlterations(floor);
                        if(PossibleTreasures == null){

                            //Prevents the original addLevelChests to run if we dont want any chest
                            return false;
                        }
                        break;
                    }
                }
            }

            //Execute original method that will add a chest
            return true;
        }



        /**
        * Harmony postfix method that will handle the change of map (and map populating) when floor type has been changed
        * i.e. it was a treasure chest floor but has been changed to regular mining floor
        * or mining floor changed to treasure chest floor
        */
        public static void loadLevel_Postfix(MineShaft __instance, int level){
            

            if(context == null) return;

            if(level > 119) return;

            MTFModifiedFloor? CurrentModifiedFloor = null;

            foreach (MTFModifiedFloor ModifiedFloor in context.getListOfFloors()){
                if(ModifiedFloor.Floor == level){
                    if(!ModifiedFloor.IsFloorTypeModified()) return;
                    CurrentModifiedFloor = ModifiedFloor;
                    break;
                }
            }

            if(CurrentModifiedFloor == null) return;

            if(CurrentModifiedFloor.IsFloorTypeModifiedToTreasure()){
                if(level % 5 == 0){
                    __instance.mapPath.Value = Path.Combine("Maps", "Mines", "10");
                }
                else{
                    __instance.mapPath.Value = Path.Combine("Maps", "Mines", "10b");
                }
            }
            else{
                if(CurrentModifiedFloor.MapId == null){
                    CurrentModifiedFloor.MapId = "15";
                }
                    __instance.mapPath.Value = Path.Combine("Maps", "Mines", CurrentModifiedFloor.MapId);
            }

            //Duplicate code from original LoadLevel method : handles map image source

            if (__instance.getMineArea() == 0 || __instance.getMineArea() == 10 || (__instance.getMineArea(level) != 0 && __instance.getMineArea(level) != 10))
                    {
                        if (__instance.getMineArea(level) == 40)
                        {
                            __instance.mapImageSource.Value = Path.Combine("Maps", "Mines", "mine_frost");
                            if (level >= 70)
                            {
                                __instance.mapImageSource.Value += "_dark";
                                __instance.loadedDarkArea = true;
                            }
                        }
                        else if (__instance.getMineArea(level) == 80)
                        {
                            __instance.mapImageSource.Value = Path.Combine("Maps", "Mines", "mine_lava");
                            if (level >= 110 && level != 120)
                            {
                                __instance.mapImageSource.Value += "_dark";
                                __instance.loadedDarkArea = true;
                            }
                        }
                    }

                    if (level >= 30 && level < 40)
                    {
                        __instance.loadedDarkArea = true;
                        if (__instance.mapImageSource.Value == null)
                        {
                            __instance.mapImageSource.Value = Path.Combine("Maps", "Mines", "mine_dark");
                        }
                        else if (!__instance.mapImageSource.Value.EndsWith("dark"))
                        {
                            __instance.mapImageSource.Value += "_dark";
                        }
                    }

                    if (__instance.GetAdditionalDifficulty() > 0)
                    {
                        string text = Path.Combine("Maps", "Mines", "mine");
                        if (__instance.mapImageSource.Value != null)
                        {
                            text = __instance.mapImageSource.Value;
                        }

                        if (text.EndsWith("_dark"))
                        {
                            text = text.Remove(text.Length - "_dark".Length);
                        }

                        string text2 = text;
                        if (level % 40 >= 30)
                        {
                            __instance.loadedDarkArea = true;
                        }

                        if (__instance.loadedDarkArea)
                        {
                            text += "_dark";
                        }

                        text += "_dangerous";
                        try
                        {
                            __instance.mapImageSource.Value = text;
                            Game1.temporaryContent.Load<Texture2D>(__instance.mapImageSource.Value);
                        }
                        catch (ContentLoadException)
                        {
                            text = text2 + "_dangerous";
                            try
                            {
                                __instance.mapImageSource.Value = text;
                                Game1.temporaryContent.Load<Texture2D>(__instance.mapImageSource.Value);
                            }
                            catch (ContentLoadException)
                            {
                                text = text2;
                                if (__instance.loadedDarkArea)
                                {
                                    text += "_dark";
                                }

                                try
                                {
                                    __instance.mapImageSource.Value = text;
                                    Game1.temporaryContent.Load<Texture2D>(__instance.mapImageSource.Value);
                                    goto end_IL_0839;
                                }
                                catch (ContentLoadException)
                                {
                                    __instance.mapImageSource.Value = text2;
                                    goto end_IL_0839;
                                }

                            end_IL_0839:;
                            }
                        }
                    }


            //--end duplicate code


            __instance.updateMap();

            __instance.ApplyDiggableTileFixes();
            if (!__instance.isSideBranch())
            {
                MineShaft.lowestLevelReached = Math.Max(MineShaft.lowestLevelReached, level);
                if (level % 5 == 0 && __instance.getMineArea() != 121)
                {
                    __instance.ElevatorLightSpot = Utility.findTile(__instance, 80, "Buildings");
                    __instance.UpdateWhenCurrentLocation(Game1.currentGameTime);
                }
            }
        }
        
        /**
        * Handle alterations of monster- and rock-population when the floor has changed type
        * either from treasure floor to mining floor
        * or from mining floor to treasure floor
        * Actually just changes the floor num during MineShaft.adjustLevelChances and MineShaft.populateLevel()
        * So the game will populate the flor AS IF it was the type of floor we'd like to emulate.
        */
        public static bool adjustLevelChances_Prefix(ref MineShaft __instance){

            if(context == null) return true;

            if(__instance.mineLevel > 119) return true;

            MTFModifiedFloor? CurrentModifiedFloor = null;

            foreach (MTFModifiedFloor ModifiedFloor in context.getListOfFloors()){
                if(ModifiedFloor.Floor == __instance.mineLevel){
                    if(!ModifiedFloor.IsFloorTypeModified()) return true;
                    CurrentModifiedFloor = ModifiedFloor;
                    break;
                }
            }

            if(CurrentModifiedFloor == null) return true;

            context.storedCurrentFloorDuringPopulation = __instance.mineLevel;

            int MineAreaNum = __instance.getMineArea();
            switch(MineAreaNum){
                case 40:
                case 80:
                    __instance.mineLevel = MineAreaNum;
                break;
                default:
                    __instance.mineLevel = 10;
                break;
            }

            if(CurrentModifiedFloor.IsFloorTypeModifiedToTreasure()){
                //mineLevel is already fine !
            }
            else{
                __instance.mineLevel += 5;
            }

            return true;
        }

        /**
        * If adjustLevelChances_Prefix has been run then MineShaft.mineLevel has a fake value
        * lets just get it back to its original value
        */
        public static void populateLevel_Postix(ref MineShaft __instance){

            if(context == null) return;

            if(context.storedCurrentFloorDuringPopulation != null){
                
                //Returning minelevel to its original (and deeply true) value
                __instance.mineLevel = (int)context.storedCurrentFloorDuringPopulation;
                context.storedCurrentFloorDuringPopulation = null;

            }
        }
    }
}