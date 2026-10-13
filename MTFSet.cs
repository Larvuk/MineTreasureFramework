namespace MineTreasureFramework;

public class MTFSet
{
    public string SetName;
    public string SetType = "alter_vanilla_treasures";
    public string? SetDescription = null;
    public bool ChangesFloorTypes = true;
    public List<MTFModifiedFloor> Floors = new List<MTFModifiedFloor>();
    
    public void SaveAlterSets(bool bAdded)
    {
        if (bAdded && ModEntry.context.Config.ChosenAlterations.Contains(this.SetName) == false)
        {
            ModEntry.context.Config.ChosenAlterations.Add(this.SetName);
        }
        else if(!bAdded)
        {
            ModEntry.context.Config.ChosenAlterations.Remove(this.SetName);
        }
    }
    
    public bool GetIsAlterSetChecked()
    {   
        return ModEntry.context.Config.ChosenAlterations.Contains(this.SetName);
    }
}