using Content.Shared._Stalker_EN.RemovableMarkings;

namespace Content.Client._Stalker_EN.RemovableMarkings;

public sealed class RemovableMarkingsSystem : SharedRemovableMarkingsSystem
{
    // emotes are only done on server so nothing happens on client
    protected override void DoRemovalEmote(Entity<RemovableMarkingsComponent> entity) { }
}
