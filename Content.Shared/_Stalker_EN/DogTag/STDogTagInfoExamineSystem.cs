using Content.Shared.Examine;

namespace Content.Shared._Stalker_EN.DogTag;

/// <summary>
/// Displays the engraved owner name and age when a dog tag with
/// <see cref="STDogTagInfoComponent"/> is examined.
/// </summary>
public sealed class STDogTagInfoExamineSystem : EntitySystem
{
    private const string LocExamineName = "st-dogtag-examine-name";
    private const string LocExamineAge = "st-dogtag-examine-age";
    private const string LocUnknown = "st-dogtag-examine-unknown";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STDogTagInfoComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(EntityUid uid, STDogTagInfoComponent component, ExaminedEvent args)
    {
        var name = string.IsNullOrEmpty(component.OwnerName)
            ? Loc.GetString(LocUnknown)
            : component.OwnerName;

        var age = component.OwnerAge > 0
            ? component.OwnerAge.ToString()
            : Loc.GetString(LocUnknown);

        using (args.PushGroup(nameof(STDogTagInfoComponent)))
        {
            args.PushMarkup(Loc.GetString(LocExamineName, ("name", name)));
            args.PushMarkup(Loc.GetString(LocExamineAge, ("age", age)));
        }
    }
}
