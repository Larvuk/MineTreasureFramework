using System.Formats.Asn1;
using System.Runtime.CompilerServices;
using GenericModConfigMenu;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;

namespace MineTreasureFramework
{
    public class ModEntry : Mod 
    {
        public static ModEntry? context;
        public ModConfig? Config; 
        public static Random myRand = new();
        
        public bool PacksHaveBeenLoaded = false;
        public List<MTFSet> AlterVanillaTreasuresSets = new();
        public List<MTFSet> FullReplacementSets = new();
        public List<MTFSet> AlterReplacementSets = new();

        private int NumberOfDisplayedFullSets = 0;
        private MTFSet? currentlyParsedAlterationSet = null;

        public int? storedCurrentFloorDuringPopulation = null;

        public override void Entry(IModHelper helper)
        {
            //Standard modding stuff

            context = this;
            Config = Helper.ReadConfig<ModConfig>();
            if (!Config.EnableMod)
                return;
            
            //Finding all available content packs
            LoadAllAvailablePacks();
            
            //GMCM integration
            
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            
            //Harmony stuff begins here

            MineTreasureFrameworkPatches.Initialize(this);

            //Harmony patches declaration

            var harmony = new Harmony(ModManifest.UniqueID);


            //Harmony methods always used by MineTreasureFramework

            harmony.Patch(
                original: AccessTools.Method(typeof(MineShaft), "GetReplacementChestItem"),
                prefix: new HarmonyMethod(typeof(MineTreasureFrameworkPatches), nameof(MineTreasureFrameworkPatches.GetReplacementChestItem_Prefix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(MineShaft), "addLevelChests"),
                prefix: new HarmonyMethod(typeof(MineTreasureFrameworkPatches), nameof(MineTreasureFrameworkPatches.addLevelChests_Prefix))
            );


            //Harmony methods only used when editing floor type

            if(WillChosenSetsChangeFloorTypes()){

                harmony.Patch(
                    original: AccessTools.Method(typeof(MineShaft), "loadLevel"),
                    postfix: new HarmonyMethod(typeof(MineTreasureFrameworkPatches), nameof(MineTreasureFrameworkPatches.loadLevel_Postfix))
                );

                harmony.Patch(
                    original: AccessTools.Method(typeof(MineShaft), "adjustLevelChances"),
                    prefix: new HarmonyMethod(typeof(MineTreasureFrameworkPatches), nameof(MineTreasureFrameworkPatches.adjustLevelChances_Prefix))
                );

                harmony.Patch(
                    original: AccessTools.Method(typeof(MineShaft), "populateLevel"),
                    postfix: new HarmonyMethod(typeof(MineTreasureFrameworkPatches), nameof(MineTreasureFrameworkPatches.populateLevel_Postix))
                );

            }

        }
        
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            //If a content pack was just being installed, its default active sets overrides previous values
            ChangeActiveSetsToNewPackDefaults();
            
            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            string[] fullSets = new string[FullReplacementSets.Count+1];
            fullSets[0] = "Vanilla";
            FullReplacementSets.Do(ListFullSets);

            void ListFullSets(MTFSet obj)
            {
                NumberOfDisplayedFullSets++;
                fullSets[NumberOfDisplayedFullSets] = obj.SetName;
            }

            // add config options
            configMenu.AddTextOption(
                mod: this.ModManifest,
                name: () => "Full set",
                tooltip: () => "The base set of treasures to use in your game's mine.",
                getValue: () => this.Config.ChosenFullSet,
                setValue: value => this.Config.ChosenFullSet = value,
                allowedValues: fullSets
            );

            //Creating an ON/OFF checkbox for every Vanilla Alteration Pack available.

            if (AlterVanillaTreasuresSets.Count > 0)
            {
                configMenu.AddSectionTitle(
                    mod: this.ModManifest,
                    text: () => "Alteration sets for Vanilla"
                );

                AlterVanillaTreasuresSets.Do(ListAlterVanillaSets);

                void ListAlterVanillaSets(MTFSet set)
                {
                    this.currentlyParsedAlterationSet = set;

                    configMenu.AddBoolOption(
                        mod: this.ModManifest,
                        name: () => set.SetName,
                        tooltip: () => set.SetDescription,
                        getValue: set.GetIsAlterSetChecked,
                        setValue: set.SaveAlterSets
                    );
                }
            }
            
            //Creating an ON/OFF checkbox for every Alter-Replacement-Set Alteration Pack available.
            
            if (AlterReplacementSets.Count > 0)
            {
                configMenu.AddSectionTitle(
                    mod: this.ModManifest,
                    text: () => "Alteration sets for other sets than Vanilla"
                );
                
                AlterReplacementSets.Do(ListAlterReplacementSets);

                void ListAlterReplacementSets(MTFSet set)
                {
                    this.currentlyParsedAlterationSet = set;
                    
                    configMenu.AddBoolOption(
                        mod: this.ModManifest,
                        name: () => set.SetName,
                        tooltip: () => set.SetDescription,
                        getValue: set.GetIsAlterSetChecked,
                        setValue: set.SaveAlterSets
                        );
                }

            }
            
            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => "Other options"
            );
            
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Use remixed version of sets",
                tooltip: () => "Some sets (including Vanilla) have an alternate \"remixed\" version. You can activate them here.\nOriginal checkbox in vanilla New Game option panel is deactivated by this mod.",
                getValue: () => this.Config.UseRemixedVersionOfSets,
                setValue: value => this.Config.UseRemixedVersionOfSets = value
            );
        }

        /*
         * Method used from MineTreasureFrameworkPatches.
         */
        public List<Item>? getPossibleTreasuresAfterMTFAlterations(int floor){

            LoadAllAvailablePacks(true);
            
            MTFSet? FinalFullReplacementSet = null;
            List<MTFSet> FinalAlterationSets = new();

            List<Item> ItemList = new();

            bool LoadVanillaItems = false;
            
            bool RemixedRewardsAreOn = false;
            //Not using the Vanilla Remixed Treasures checkbox because of compatibility issues with Android
            //if(Game1.netWorldState.Value.shuffleMineChests.Value == Game1.MineChestType.Remixed){
            if(Config.UseRemixedVersionOfSets){
                RemixedRewardsAreOn = true;
            }

            //Get the active Full Replacement Set (if it exists)
            FinalFullReplacementSet = GetChosenFullReplacementSet();

            //One Replacement Set: lets add it and its alter packs
            if(FinalFullReplacementSet != null){

                foreach (MTFModifiedFloor ModifiedFloor in FinalFullReplacementSet.Floors)
                {
                    if(ModifiedFloor.Floor == floor){
                        if(ModifiedFloor.KeepVanillaVersion == "yes"){
                            LoadVanillaItems = true;
                        }
                        else{
                            foreach(PossibleTreasure treasure in ModifiedFloor.TreasureList){
                                if(treasure.Condition == "none" || (treasure.Condition == "remixed" && RemixedRewardsAreOn) || (treasure.Condition == "not_remixed" && !RemixedRewardsAreOn)){
                                    
                                    //Add the item to the list
                                    Item itemToAdd = ItemRegistry.Create(treasure.QualifiedItemId, treasure.Amount) ?? throw new Exception("Treasures need field QualifiedItemId to be set");
                                    ItemList.Add(itemToAdd);

                                }
                            }
                        }
                    }
                }
            }

            //No replacement set: lets load vanilla treasures
            else{
                LoadVanillaItems = true;
            }

            if (LoadVanillaItems) FinalAlterationSets = AlterVanillaTreasuresSets;
            if(FinalFullReplacementSet != null) FinalAlterationSets.AddRange(AlterReplacementSets);
            FinalAlterationSets = RemoveNotApplicableAlterationSets(FinalAlterationSets, FinalFullReplacementSet);

            if(LoadVanillaItems){
                List<string> IdList = getPossibleTreasuresBeforeMTFAlterations(floor);
                ItemList = loadItemsFromIds(IdList);
            }
            
            foreach (MTFSet set in FinalAlterationSets)
            {

                if(set == null){
                    throw new Exception("json seems to be NULL.");
                }

                foreach (MTFModifiedFloor ModifiedFloor in set.Floors)
                {
                    if(ModifiedFloor.Floor == floor){

                        //Lets modify the list

                        if(ModifiedFloor.ClearAllTreasures == "yes"){
                            //Emptying the vanilla game's treasure list or replacement set's treasure list
                            ItemList.Clear();
                        }

                        foreach(PossibleTreasure treasure in ModifiedFloor.TreasureList){
                            if(treasure.Condition == "none" || (treasure.Condition == "remixed" && RemixedRewardsAreOn) || (treasure.Condition == "not_remixed" && !RemixedRewardsAreOn)){

                                Item item = ItemRegistry.Create(treasure.QualifiedItemId, treasure.Amount) ?? throw new Exception("Treasures need field QualifiedItemId to be set");
                                
                                if(treasure.Remove == "yes"){
                                    //Remove the item from the list
                                    ItemList.RemoveAll(i => i.QualifiedItemId == item.QualifiedItemId);
                                }
                                else{
                                    //Add the item to the list
                                    ItemList.Add(item);
                                }
                                
                            }
                        }
                    }
                }
            }
            if(ItemList.Count > 0){
                return ItemList;
            }
            else return null;
        }

        public MTFSet? GetChosenFullReplacementSet()
        {
            LoadAllAvailablePacks();
                
            //Get the active Full Replacement Set (if it exists)
            MTFSet chosenSet = null;
            FullReplacementSets.Do(LoadFinalFullReplacementSet);
            void LoadFinalFullReplacementSet(MTFSet set)
            {
                if (set.SetName == Config.ChosenFullSet)
                {
                    chosenSet = set;
                }
            }

            return chosenSet;
        }

        /*
         * Remove deactivated alteration sets from the list AND alteration sets that are not applicable to the chosen FullReplacementSet
         * Even AlterVanillaTreasures sets can be applied on FullReplacementSets, as FullReplacementSets can create "holes" using "keep-vanilla-version, making the vanilla treasures used and alterable.
         * But AlterReplacementSets will never be applied onto Vanilla treasures.
         */
        public List<MTFSet> RemoveNotApplicableAlterationSets(List<MTFSet> AlterationSets, MTFSet? FullReplacementSet)
        {
            LoadAllAvailablePacks();

            List<MTFSet> FilteredAlterationSets = new();
            
            AlterationSets.DoIf(Condition, Action);
            bool Condition(MTFSet arg)
            {
                if (Config.ChosenAlterations.Contains(arg.SetName))
                {
                    return (arg.SetType == "alter_vanilla_treasures" || FullReplacementSet != null);
                }
                return false;
            }
            void Action(MTFSet obj)
            {
                FilteredAlterationSets.Add(obj);
            }
            return FilteredAlterationSets;
        }
        

        /*
         * Loads from disk all the sets found in mods
         * into this.AlterVanillaTreasuresSets, this.FullReplacementSets and this.AlterReplacementSets.
         */
        public void LoadAllAvailablePacks(bool forceReload = false)
        {
            if (!forceReload && this.PacksHaveBeenLoaded) return;
            this.AlterVanillaTreasuresSets = new();
            this.FullReplacementSets = new();
            this.AlterReplacementSets = new();
            foreach (IContentPack contentPack in Helper.ContentPacks.GetOwned())
            {
                try
                {
                    MTFData? json = (contentPack.ReadJsonFile<MTFData>("content.json") ?? null) ?? throw new Exception("json seems to be NULL.");
                    //Lets sort sets by set type
                    foreach (MTFSet set in json.Sets)
                    {
                        switch (set.SetType){
                            case "alter_vanilla_treasures":
                                this.AlterVanillaTreasuresSets.Add(set);
                                break;
                            case "full_replacement_set":
                                this.FullReplacementSets.Add(set);
                                break;
                            case "alter_replacement_set":
                                this.AlterReplacementSets.Add(set);
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Monitor.Log($"MTF: error reading content.json file in content pack {contentPack.Manifest.Name}.\r\n{ex}", LogLevel.Error);
                }
            }
            this.PacksHaveBeenLoaded = true;
        }
        
        
        /*
         * If a content pack was just being installed, makes its default active sets override previous values
         */
        public void ChangeActiveSetsToNewPackDefaults()
        {
            foreach (IContentPack contentPack in Helper.ContentPacks.GetOwned())
            {
                try
                {
                    PackConfigData? json = (contentPack.ReadJsonFile<PackConfigData>("config.json") ?? null);

                    if (json?.DefaultFullSet != null)
                    {
                        Config.ChosenFullSet = json.DefaultFullSet;
                        json.DefaultFullSet = null;
                    }

                    if (json?.DefaultAlterations != null)
                    {
                        Config.ChosenAlterations = json.DefaultAlterations;
                        json.DefaultAlterations = null;
                    }
                    contentPack.WriteJsonFile("config.json", json);
                }
                catch (Exception ex)
                {
                    Monitor.Log($"MTF: error reading config.json file in content pack {contentPack.Manifest.Name}.\r\n{ex}", LogLevel.Error);
                }
            }
        }

        public bool WillChosenSetsChangeFloorTypes()
        {
            var result = false;
            
            LoadAllAvailablePacks();
            
            List<MTFSet>? UsedSets = new();
            UsedSets.Add(GetChosenFullReplacementSet());
            UsedSets.AddRange(AlterVanillaTreasuresSets);
            UsedSets.AddRange(AlterReplacementSets);
            UsedSets.Do(Action);
            void Action(MTFSet? obj)
            {
                if (obj is not null && obj.ChangesFloorTypes == true) result = true;
            }

            return result;
        }
        

        /*
         * Takes a list of Alteration Packs (IContentPack)
         * and returns it minus the packs that are not marked as activated ( = listed in the Config file)
         */
        public List<MTFSet> RemoveDeactivatedAlterationSets(List<MTFSet> unfilteredList)
        {
            var newList = new List<MTFSet>();
            unfilteredList.DoIf(Condition, Action);

            bool Condition(MTFSet set)
            {
                return Config.ChosenAlterations.Contains(set.SetName);
            }

            void Action(MTFSet set)
            {
                newList.Add(set);
            }

            return newList;
        }
        

        //Read .json content packs written for MTF
        //return a list of objects representing modified floors and their modifiers
        public List<MTFModifiedFloor> getListOfFloors()
        {
            LoadAllAvailablePacks();

            List<MTFSet> UsedSets = new();
            
            List<MTFModifiedFloor> ListOfFloors = new();

            //Loads active FullSet if defined.
            foreach (MTFSet set in FullReplacementSets)
            {
                if (Config.ChosenFullSet == set.SetName)
                {
                    UsedSets.Add(set);
                }
            }
            
            //Loads active alteration sets.
            foreach (MTFSet set in (Config.ChosenFullSet != "Vanilla"
                         ? AlterReplacementSets
                         : AlterVanillaTreasuresSets))
            {
                if (Config.ChosenAlterations.Contains(set.SetName))
                {
                    UsedSets.Add(set);
                }
            }
            
            //Parsing active sets
            foreach (MTFSet set in UsedSets)
            {
                foreach (MTFModifiedFloor ModifiedFloor in set.Floors)
                {
                    ModifiedFloor.initializeHelpers(getPossibleTreasuresAfterMTFAlterations(ModifiedFloor.Floor));
                    ListOfFloors.Add(ModifiedFloor);
                }
            }

            return ListOfFloors;
        }


        //Dumb list that duplicates information from MineShaft.AddLevelChests() and MineShaft.GetReplacementChestItem() so you can access it without executing unwanted code
        //Returns possible treasures IDs for specified floor before MTF alterations.
        public List<string> getPossibleTreasuresBeforeMTFAlterations(int floor){

            //Vanilla non-remixed treasures
            List<string> list10 = new() { "(B)506"};
            List<string> list20 = new() { "(W)11"};
            List<string> list30 = new() { };
            List<string> list40 = new() { "(W)32"};
            List<string> list50 = new() { "(B)509"};
            List<string> list60 = new() { "(W)21"};
            List<string> list70 = new() { "(W)33"};
            List<string> list80 = new() { "(B)512"};
            List<string> list90 = new() { "(W)8"};
            List<string> list100 = new() { "434"};
            List<string> list110 = new() { "(B)514"};

            //Vanilla remixed treasures (floors 30, 40, 70,100 are not modified)
            if (Config.UseRemixedVersionOfSets)
            {
                list10 = new List<string>{
                    "(B)506","(B)507","(W)12","(W)17","(W)22","(W)31"
                };

                list20 = new List<string>{
                    "(W)11","(W)24","(W)20","517","519"
                };

                list50 = new List<string>{
                    "(B)509","(B)510","(B)508","(W)1","(W)43"
                };

                list60 = new List<string>{
                    "(W)21","(W)44","(W)6","(W)18","(W)27"
                };

                list80 = new List<string>{
                    "(B)512","(B)511","(W)10","(W)7","(W)46","(W)19"
                };

                list90 = new List<string>{
                    "(W)8","(W)52","(W)45","(W)5","(W)60"
                };

                list110 = new List<string>{
                    "(B)514","(B)878","(W)50","(W)28"
                };
            }

            if(floor == 10) return list10;
            if(floor == 20) return list20;
            if(floor == 30) return list30;
            if(floor == 40) return list40;
            if(floor == 50) return list50;
            if(floor == 60) return list60;
            if(floor == 70) return list70;
            if(floor == 80) return list80;
            if(floor == 90) return list90;
            if(floor == 100) return list100;
            if(floor == 110) return list110;

            return new List<string>();
        }

        //Takes a list of item IDs, returns a list of items.
        public static List<Item> loadItemsFromIds(List<string> IdList){

            List<Item> ItemList = new();

            foreach(string id in IdList){
                ItemList.Add(ItemRegistry.Create(id));
            }

            return ItemList;
        }

    }
}