using Content.Server.Chat.Systems;
using Content.Shared._Stalker_EN.RemovableMarkings;

namespace Content.Server._Stalker_EN.RemovableMarkings;

public sealed class RemovableMarkingsSystem : SharedRemovableMarkingsSystem
{
    [Dependency] private readonly ChatSystem _chatSystem = default!;

    protected override void DoRemovalEmote(Entity<RemovableMarkingsComponent> entity)
    {
        // This method assumes RemovalEmoteId to be notnull as said in its summary
        _chatSystem.TryEmoteWithChat(entity.Owner, entity.Comp.RemovalEmoteId!);
    }
}
