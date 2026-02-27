using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.DogTag;

/// <summary>
/// Stores the owner's identity information engraved on a dog tag at spawn time.
/// Data is displayed when the dog tag is examined.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STDogTagInfoComponent : Component
{
    /// <summary>
    /// The character name engraved on the dog tag.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string OwnerName = string.Empty;

    /// <summary>
    /// The character's age engraved on the dog tag.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int OwnerAge;
}
