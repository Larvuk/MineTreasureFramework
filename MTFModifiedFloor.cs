using System;
using System.Collections.Generic;
using System.ComponentModel;
using StardewValley;

namespace MineTreasureFramework
{

    public class MTFModifiedFloor
    {
        public int Floor;
        public string ClearAllTreasures = "no"; //Used with "alter_vanilla_treasures" and "alter_replacement_set" packs
        public string KeepVanillaVersion = "no"; //Used with "full_replacement_set" packs
        public List<PossibleTreasure> TreasureList = new List<PossibleTreasure>();

        //empty_room == empty treasure chest room
        //mining_floor == load the MapId map with monsters and rocks
        //vanilla == empty_room if floor 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110. mining_floor elsewhere.
        public string OnNoTreasureFound = "vanilla";
        public string? MapId = null;

        private bool TreasureFloorToNormalFloor = false;
        private bool NormalFloorToTreasureFloor = false;

        public void initializeHelpers(List<Item>? FinalItemList){
            if(Floor % 10 == 0 && (FinalItemList == null || FinalItemList.Count == 0)){
                if(OnNoTreasureFound == "mining_floor"){
                    TreasureFloorToNormalFloor = true;
                }
            }
            else if(Floor % 10 != 0 && FinalItemList != null && FinalItemList.Count > 0){
                NormalFloorToTreasureFloor = true;
            }
            else if(Floor % 10 != 0 && (FinalItemList == null || FinalItemList.Count == 0) && OnNoTreasureFound == "empty_room"){
                NormalFloorToTreasureFloor = true;
            }
        }
        public bool IsFloorTypeModified(){
            return TreasureFloorToNormalFloor || NormalFloorToTreasureFloor;
        }
        public bool IsFloorTypeModifiedToTreasure(){
            return NormalFloorToTreasureFloor;
        }
        public bool IsFloorTypeModifiedToNormal(){
            return TreasureFloorToNormalFloor;
        }
    }

    public class PossibleTreasure
    {
        public string QualifiedItemId = "";
        public int Amount = 1;
        public string Condition = "none";
        public string Remove = "no";
    }

}