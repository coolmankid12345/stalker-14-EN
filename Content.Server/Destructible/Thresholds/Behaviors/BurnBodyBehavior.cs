using Content.Shared.Body.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects; // stalker-en-changes

namespace Content.Server.Destructible.Thresholds.Behaviors;

[UsedImplicitly]
[DataDefinition]
public sealed partial class BurnBodyBehavior : IThresholdBehavior
{

    public void Execute(EntityUid bodyId, DestructibleSystem system, EntityUid? cause = null)
    {
        var transformSystem = system.EntityManager.System<TransformSystem>();
        var inventorySystem = system.EntityManager.System<InventorySystem>();
        var sharedPopupSystem = system.EntityManager.System<SharedPopupSystem>();

        if (system.EntityManager.TryGetComponent<InventoryComponent>(bodyId, out var comp))
        {
            // stalker-en-changes-start: use proper container removal to fix PVS desync (#441)
            var containerSystem = system.ContainerSystem;
            var bodyCoords = system.EntityManager.GetComponent<TransformComponent>(bodyId).Coordinates;
            foreach (var item in inventorySystem.GetHandOrInventoryEntities(bodyId))
            {
                if (containerSystem.TryGetContainingContainer(item, out var container))
                    containerSystem.Remove(item, container, force: true, destination: bodyCoords);
                else
                    transformSystem.DropNextTo(item, bodyId);
            }
            // stalker-en-changes-end
        }

        var bodyIdentity = Identity.Entity(bodyId, system.EntityManager);
        sharedPopupSystem.PopupCoordinates(Loc.GetString("bodyburn-text-others", ("name", bodyIdentity)), transformSystem.GetMoverCoordinates(bodyId), PopupType.LargeCaution);

        system.EntityManager.QueueDeleteEntity(bodyId);
    }
}
