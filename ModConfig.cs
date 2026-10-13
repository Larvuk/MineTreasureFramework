using StardewModdingAPI;

namespace MineTreasureFramework
{
    public class ModConfig
    {
        public bool EnableMod { get; set; } = true;
        
        public bool UseRemixedVersionOfSets { get; set; } = false;
        
        public string ChosenFullSet { get; set; } = "Vanilla";
        
        public List<string> ChosenAlterations { get; set; } = new();
    }
}
