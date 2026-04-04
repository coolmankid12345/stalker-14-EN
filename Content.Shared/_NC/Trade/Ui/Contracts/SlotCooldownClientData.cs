using Robust.Shared.Serialization;

namespace Content.Shared._NC.Trade;

[Serializable, NetSerializable]
public sealed class SlotCooldownClientData
{
    public string Difficulty = string.Empty;
    public string LastContractId = string.Empty;
    public string LastContractName = string.Empty;
    public int RemainingSeconds;

    public SlotCooldownClientData() { }

    public SlotCooldownClientData(
        string difficulty,
        string lastContractId,
        string lastContractName,
        int remainingSeconds)
    {
        Difficulty = difficulty;
        LastContractId = lastContractId;
        LastContractName = lastContractName;
        RemainingSeconds = remainingSeconds;
    }
}
