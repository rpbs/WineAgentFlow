namespace WineAgentFlow;

public static class WineDatabase
{
    public static List<Wine> wines = new()
    {
        new Wine("Chateau Margaux", "North", 999.99m),
        new Wine("Screaming Eagle Cabernet Sauvignon", "South", 2999.99m),
        new Wine("Penfolds Grange", "East", 849.99m),
        new Wine("Domaine de la Romanée-Conti", "North", 15999.99m),
        new Wine("Vega Sicilia Único", "South", 499.99m)
    };
}

public record Wine(string name, string region, decimal price);