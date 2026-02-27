using System;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Content.Shared.Actions;
using Robust.Shared.Serialization;
using Robust.Shared.Network;
using Content.Shared._Stalker.Bands;
using Content.Shared._Stalker_EN.FactionRelations; // stalker-en-changes

namespace Content.Shared._Stalker.Bands
{
    // UI key for bands managing UI
    [Serializable, NetSerializable]
    public enum BandsUiKey
    {
        Key = 0
    }
    [Virtual]
    public class SharedBandsSystem : EntitySystem
    {
        [Dependency] private readonly SharedActionsSystem _actions = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
        [Dependency] private readonly IPrototypeManager _protoManager = default!;

        public override void Initialize()
        {
            base.Initialize();
        }
    }

    [Serializable, NetSerializable]
    public sealed class BandsManagingBoundUserInterfaceState : BoundUserInterfaceState
    {
        public string? BandName { get; }
        public int MaxMembers { get; }
        public List<BandMemberInfo> Members { get; }
        public bool CanManage { get; }
        public List<WarZoneInfo> WarZones { get; }
        public List<BandPointsInfo> BandPoints { get; }
        public List<BandShopItem> ShopItems { get; }

        // stalker-en-changes start
        /// <summary>
        /// The player's faction relation name (e.g. "Loners", "Duty"). Null if not in a mapped faction.
        /// </summary>
        public string? PlayerFaction { get; }

        /// <summary>
        /// All faction names from the defaults prototype, for the relations tab.
        /// </summary>
        public List<string> AllFactions { get; }

        /// <summary>
        /// Current faction relations (non-neutral entries only), for the relations tab.
        /// </summary>
        public List<STFactionRelationEntry> FactionRelations { get; }

        /// <summary>
        /// Pending incoming proposals targeting the player's faction.
        /// </summary>
        public List<STFactionRelationProposalEntry> IncomingProposals { get; }

        /// <summary>
        /// Pending outgoing proposals from the player's faction.
        /// </summary>
        public List<STFactionRelationProposalEntry> OutgoingProposals { get; }

        /// <summary>
        /// Per-pair cooldown remaining in seconds. Key: target faction name.
        /// </summary>
        public Dictionary<string, float> CooldownsRemaining { get; }

        /// <summary>
        /// When true, the Relations tab should be hidden (player is in a restricted faction).
        /// </summary>
        public bool HideRelationsTab { get; }

        /// <summary>
        /// Maps faction IDs to human-readable display names (e.g. "ClearSky" → "Clear Sky").
        /// </summary>
        public Dictionary<string, string> FactionDisplayNames { get; }
        // stalker-en-changes end

        public BandsManagingBoundUserInterfaceState(
            string? bandName,
            int maxMembers,
            List<BandMemberInfo> members,
            bool canManage,
            List<WarZoneInfo>? warZones,
            List<BandPointsInfo>? bandPoints,
            List<BandShopItem>? shopItems,
            // stalker-en-changes start
            string? playerFaction = null,
            List<string>? allFactions = null,
            List<STFactionRelationEntry>? factionRelations = null,
            List<STFactionRelationProposalEntry>? incomingProposals = null,
            List<STFactionRelationProposalEntry>? outgoingProposals = null,
            Dictionary<string, float>? cooldownsRemaining = null,
            bool hideRelationsTab = false,
            Dictionary<string, string>? factionDisplayNames = null)
            // stalker-en-changes end
        {
            BandName = bandName;
            MaxMembers = maxMembers;
            Members = members;
            CanManage = canManage;
            WarZones = warZones ?? new List<WarZoneInfo>();
            BandPoints = bandPoints ?? new List<BandPointsInfo>();
            ShopItems = shopItems ?? new List<BandShopItem>();
            // stalker-en-changes start
            PlayerFaction = playerFaction;
            AllFactions = allFactions ?? new List<string>();
            FactionRelations = factionRelations ?? new List<STFactionRelationEntry>();
            IncomingProposals = incomingProposals ?? new List<STFactionRelationProposalEntry>();
            OutgoingProposals = outgoingProposals ?? new List<STFactionRelationProposalEntry>();
            CooldownsRemaining = cooldownsRemaining ?? new Dictionary<string, float>();
            HideRelationsTab = hideRelationsTab;
            FactionDisplayNames = factionDisplayNames ?? new Dictionary<string, string>();
            // stalker-en-changes end
        }
    }

    // --- New Data Structures for War Zone Tab ---

    [Serializable, NetSerializable]
    public sealed class WarZoneInfo
    {
        public string ZoneId { get; }
        public string Owner { get; }
        public float Cooldown { get; } // In seconds
        public string Attacker { get; }
        public string Defender { get; }
        public float Progress { get; } // 0.0 to 1.0. TODO: Not implemented yet

        public WarZoneInfo(string zoneId, string owner, float cooldown, string attacker, string defender, float progress)
        {
            ZoneId = zoneId;
            Owner = owner;
            Cooldown = cooldown;
            Attacker = attacker;
            Defender = defender;
            Progress = progress;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BandPointsInfo
    {
        public string BandProtoId { get; }
        public string BandName { get; }
        public float Points { get; }

        public BandPointsInfo(string bandProtoId, string bandName, float points)
        {
            BandProtoId = bandProtoId;
            BandName = bandName;
            Points = points;
        }
    }

    // --- Existing Data Structures ---

    [Serializable, NetSerializable]
    public sealed class BandMemberInfo
    {
        public NetUserId UserId { get; }
        public string PlayerName { get; } // Keep this for now (ckey)
        public string CharacterName { get; } // Add this field
        public string RoleId { get; }

        public BandMemberInfo(NetUserId userId, string playerName, string characterName, string roleId)
        {
            UserId = userId;
            PlayerName = playerName;
            CharacterName = characterName;
            RoleId = roleId;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BandsManagingAddMemberMessage : BoundUserInterfaceMessage
    {
        public string PlayerName { get; }

        public BandsManagingAddMemberMessage(string playerName)
        {
            PlayerName = playerName;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BandsManagingRemoveMemberMessage : BoundUserInterfaceMessage
    {
        public Guid PlayerUserId { get; }

        public BandsManagingRemoveMemberMessage(Guid playerUserId)
        {
            PlayerUserId = playerUserId;
        }
    }

    // --- New Message for Buying Items ---
    [Serializable, NetSerializable]
    public sealed class BandsManagingBuyItemMessage : BoundUserInterfaceMessage
    {
        public string ItemId { get; }

        public BandsManagingBuyItemMessage(string itemId)
        {
            ItemId = itemId;
        }
    }

    // stalker-en-changes start

    /// <summary>
    /// Message from client to server to propose a faction relation change.
    /// The server determines whether this is instant (escalation) or creates a bilateral proposal.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BandsManagingProposeRelationMessage : BoundUserInterfaceMessage
    {
        public string TargetFaction { get; }
        public int ProposedRelation { get; }
        public string? CustomMessage { get; }
        public bool Broadcast { get; }

        public BandsManagingProposeRelationMessage(string targetFaction, int proposedRelation, string? customMessage, bool broadcast)
        {
            TargetFaction = targetFaction;
            ProposedRelation = proposedRelation;
            CustomMessage = customMessage;
            Broadcast = broadcast;
        }
    }

    /// <summary>
    /// Message from client to server to accept or reject an incoming faction relation proposal.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BandsManagingRespondProposalMessage : BoundUserInterfaceMessage
    {
        public string InitiatingFaction { get; }
        public bool Accept { get; }

        public BandsManagingRespondProposalMessage(string initiatingFaction, bool accept)
        {
            InitiatingFaction = initiatingFaction;
            Accept = accept;
        }
    }

    /// <summary>
    /// Message from client to server to cancel an outgoing faction relation proposal.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BandsManagingCancelProposalMessage : BoundUserInterfaceMessage
    {
        public string TargetFaction { get; }

        public BandsManagingCancelProposalMessage(string targetFaction)
        {
            TargetFaction = targetFaction;
        }
    }

    // stalker-en-changes end
}

