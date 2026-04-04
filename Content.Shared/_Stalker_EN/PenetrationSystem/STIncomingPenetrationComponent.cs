using Robust.Shared.GameObjects;

namespace Content.Shared._Stalker.Weapons.Ranged;

/// <summary>
/// Temporarily placed on a target entity during a projectile hit to communicate
/// the incoming bullet's penetration class to SharedArmorSystem during the
/// DamageModifyEvent inventory relay. Removed immediately after damage resolves.
/// Never serialized or networked — it only lives for the duration of one hit event.
/// </summary>
[RegisterComponent]
public sealed partial class STIncomingPenetrationComponent : Component
{
    public int PenetrationClass;

    /// <summary>
    /// Piercing bypass: fraction of armor's Piercing protection that is IGNORED.
    /// Range [0.05, 0.90]. LOW = armor effective (bullet stopped). HIGH = armor bypassed.
    ///
    /// Designed for 100HP players with longer TTK in mind:
    ///   - Bullet stopped (delta &lt;= -1): armor is 85%+ effective on piercing
    ///   - Even match (delta = 0): armor is 75% effective
    ///   - Over-penetrating (delta &gt;= +1): armor starts losing effectiveness
    ///
    ///   bypass = clamp(0.25 + delta * 0.10, 0.05, 0.90)
    ///
    ///   pen1 vs armor6 → delta=-5 → bypass=0.05  (floor, nearly full protection)
    ///   pen3 vs armor4 → delta=-1 → bypass=0.15  (stopped, 85% pierce protection)
    ///   pen3 vs armor3 → delta= 0 → bypass=0.25  (even, 75% pierce protection)
    ///   pen4 vs armor3 → delta=+1 → bypass=0.35  (over-pen, 65% protection)
    ///   pen8 vs armor3 → delta=+5 → bypass=0.75  (heavy pen, 25% protection)
    /// </summary>
    public float CalculatePiercingBypass(int armorClass)
    {
        var delta = PenetrationClass - armorClass;
        return Math.Clamp(0.25f + delta * 0.10f, 0.05f, 0.90f);
    }

    /// <summary>
    /// Blunt bypass: fraction of armor's Blunt protection that is IGNORED.
    /// Range [0.05, 0.65]. Blunt = stopping power / kinetic energy transfer.
    ///
    /// Under-penetration: bullet stopped, most kinetic energy absorbed by armor plate.
    /// Even match: armor still absorbs most blunt.
    /// Over-penetration: bullet exits before fully dumping energy — LESS blunt
    ///   transfers to target (paradoxically, armor absorbs more blunt when bullet
    ///   passes through quickly). Capped at 0.65 — energy always transfers somewhat.
    ///
    ///   bypass = clamp(0.20 + delta * 0.09, 0.05, 0.65)
    ///
    ///   pen1 vs armor6 → delta=-5 → bypass=0.05  (floor, nearly full blunt protection)
    ///   pen3 vs armor4 → delta=-1 → bypass=0.11  (stopped, 89% blunt protection)
    ///   pen3 vs armor3 → delta= 0 → bypass=0.20  (even, 80% blunt protection)
    ///   pen5 vs armor3 → delta=+2 → bypass=0.38  (over-pen, 62% protection)
    ///   pen8 vs armor3 → delta=+5 → bypass=0.65  (cap: bullet exits fast, 35% left)
    /// </summary>
    public float CalculateBluntBypass(int armorClass)
    {
        var delta = PenetrationClass - armorClass;
        return Math.Clamp(0.20f + delta * 0.09f, 0.05f, 0.65f);
    }
}
