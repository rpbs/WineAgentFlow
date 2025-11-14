using System.ComponentModel;

namespace WineAgentFlow;

public abstract class Tools
{
    
    [Description("Search for a wine by name in the store's inventory from the North Branch and return its details if found.")]
    public static Wine SearchWineNorthBranch(string wineName) => 
        WineDatabase.wines.FirstOrDefault(x => x.name.Equals(wineName, StringComparison.OrdinalIgnoreCase) && x.region == "North");
    
    [Description("Search for a wine by name in the store's inventory from the North Branch and return its details if found.")]
    public static Wine SearchWineSouthBranch(string wineName) => 
        WineDatabase.wines.FirstOrDefault(x => x.name.Equals(wineName, StringComparison.OrdinalIgnoreCase) && x.region == "South");

    [Description("Search for a wine by name in the store's inventory from the North Branch and return its details if found.")]
    public static Wine SearchWineEastBranch(string wineName) => 
        WineDatabase.wines.FirstOrDefault(x => x.name.Equals(wineName, StringComparison.OrdinalIgnoreCase) && x.region == "East");

    [Description("Search for a wine by name in the store's inventory from the North Branch and return its details if found.")]
    public static Wine SearchWineWestBranch(string wineName) => 
        WineDatabase.wines.FirstOrDefault(x => x.name.Equals(wineName, StringComparison.OrdinalIgnoreCase) && x.region == "West");
    
}