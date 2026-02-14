namespace Content.Server._Stalker.MapLightSimulation;

[RegisterComponent]
public sealed partial class MapDayComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public double DayTime = 5400d; // 1,5 hours

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<Color> Colors = new()
    {
        Color.FromHex("#e2d2b711"),
        Color.FromHex("#e2d2b722"),
        Color.FromHex("#d9c5b622"),
        Color.FromHex("#744a3eFF"),
        Color.FromHex("#0d1926FF"),
        Color.FromHex("#010105FF"),
        Color.FromHex("#0d1926FF"),
        Color.FromHex("#5d492aFF"),
        Color.FromHex("#b2904eFF"),
        Color.FromHex("#d9c5b622"),
        Color.FromHex("#e2d2b722"),
        Color.FromHex("#e2d2b711"),
    };
}
