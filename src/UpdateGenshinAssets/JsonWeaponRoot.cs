namespace UpdateGenshinAssets;

public class Datum
{
    public string object_type { get; set; }
    public string name { get; set; }
    public string type { get; set; }
    public int rarity { get; set; }
    public string thumbnail { get; set; }
    public string link { get; set; }
}

public class JsonWeaponRoot
{
    public string version { get; set; }
    public List<Datum> data { get; set; }
}