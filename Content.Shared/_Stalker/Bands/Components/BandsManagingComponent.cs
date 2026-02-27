using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Content.Shared._Stalker.Bands;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Bands.Components
{
    /// <summary>
    /// Component attached to entities that can open the Band Management UI.
    /// </summary>
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState] // stalker-en-changes: added AutoGenerateComponentState
    // [Access(typeof(SharedBandsSystem))]
    public sealed partial class BandsManagingComponent : Component
    {

        /// <summary>
        /// The shop listings prototype ID to use for listing items and prices.
        /// </summary>
        [DataField("shopListingsProto", required: true)]
        public ProtoId<BandShopListingsPrototype> ShopListingsProto { get; private set; } = default!;

        // stalker-en-changes start
        /// <summary>
        /// The faction relation name this NPC manages (e.g. "Loners", "Duty").
        /// When set, the Relations tab uses this instead of resolving from the player's band.
        /// Maps to faction names in STFactionRelationDefaultsPrototype.
        /// </summary>
        [DataField, AutoNetworkedField]
        public string? Faction;
        // stalker-en-changes end
    }
}
