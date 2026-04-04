using System.Linq;
using Content.Client._NC.Trade.Controls;
using Content.Shared._NC.Trade;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;


namespace Content.Client._NC.Trade;

public sealed partial class NcStoreMenu
{
    private readonly Dictionary<string, NcContractCard> _contractCardsById = new();
    private readonly List<string> _contractCardOrder = new();
    private readonly List<SlotCooldownRowRefs> _slotCooldownRows = new();

    public void PopulateContracts(List<ContractClientData>? list, int skipCost, string skipCurrency, int skipBalance)
    {
        var contractList = ContractList;
        if (contractList == null)
            return;

        if (list == null || list.Count == 0)
        {
            ResetContractsListToEmpty(contractList);
            ApplyTabsVisibility();
            return;
        }

        var ordered = OrderContracts(list);
        if (!TryUpdateContractsInPlace(contractList, ordered, skipCost, skipCurrency, skipBalance))
            RebuildContracts(contractList, ordered, skipCost, skipCurrency, skipBalance);

        ApplyTabsVisibility();
    }

    public void PopulateSlotCooldowns(List<SlotCooldownClientData>? cooldowns)
    {
        if (SlotCooldownsSection == null || SlotCooldownsHost == null)
            return;

        if (cooldowns == null || cooldowns.Count == 0)
        {
            HideAllSlotCooldownRows();
            SlotCooldownsSection.Visible = false;
            return;
        }

        EnsureSlotCooldownRows(cooldowns.Count);

        for (var i = 0; i < _slotCooldownRows.Count; i++)
        {
            var rowRefs = _slotCooldownRows[i];
            if (i >= cooldowns.Count)
            {
                rowRefs.Row.Visible = false;
                continue;
            }

            rowRefs.Row.Visible = true;
            var cooldown = cooldowns[i];
            var difficulty = string.IsNullOrWhiteSpace(cooldown.Difficulty) ? "Unknown" : cooldown.Difficulty;

            var title = Loc.GetString(
                "nc-store-slot-cooldown-title",
                ("difficulty", DifficultyName(difficulty)));
            rowRefs.Title.Text = title;
            rowRefs.Title.ToolTip = title;
            rowRefs.Timer.Text = FormatCountdown(cooldown.RemainingSeconds);
        }

        SlotCooldownsSection.Visible = true;
    }

    private void EnsureSlotCooldownRows(int requiredCount)
    {
        if (SlotCooldownsHost == null)
            return;

        while (_slotCooldownRows.Count < requiredCount)
        {
            var row = CreateSlotCooldownRow();
            _slotCooldownRows.Add(row);
            SlotCooldownsHost.AddChild(row.Row);
        }
    }

    private void HideAllSlotCooldownRows()
    {
        for (var i = 0; i < _slotCooldownRows.Count; i++)
            _slotCooldownRows[i].Row.Visible = false;
    }

    private static SlotCooldownRowRefs CreateSlotCooldownRow()
    {
        var row = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#23262F"),
                BorderColor = Color.FromHex("#6A5A39"),
                BorderThickness = new(1),
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 4,
                ContentMarginBottomOverride = 4
            }
        };

        var rowLine = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 6
        };

        var title = new Label
        {
            HorizontalExpand = true,
            ClipText = true
        };

        var timer = new Label
        {
            HorizontalAlignment = HAlignment.Right,
            Modulate = Color.FromHex("#D7C18E")
        };

        rowLine.AddChild(title);
        rowLine.AddChild(timer);
        row.AddChild(rowLine);

        return new SlotCooldownRowRefs(row, title, timer);
    }

    private sealed record SlotCooldownRowRefs(
        PanelContainer Row,
        Label Title,
        Label Timer);

    private static string FormatCountdown(int totalSeconds)
    {
        var clamped = Math.Max(0, totalSeconds);
        var span = TimeSpan.FromSeconds(clamped);
        var minutes = Math.Max(0, (int) span.TotalMinutes);
        return $"{minutes:00}:{span.Seconds:00}";
    }

    private static string DifficultyName(string difficulty) =>
        difficulty switch
        {
            "Easy" => Loc.GetString("nc-store-difficulty-easy"),
            "Medium" => Loc.GetString("nc-store-difficulty-medium"),
            "Hard" => Loc.GetString("nc-store-difficulty-hard"),
            _ => difficulty
        };

    private static List<ContractClientData> OrderContracts(List<ContractClientData> contracts)
    {
        return contracts
            .OrderBy(x => x.Difficulty switch
            {
                "Easy" => 0,
                "Medium" => 1,
                "Hard" => 2,
                _ => 99
            })
            .ThenBy(x => x.Name)
            .ThenBy(x => x.Id)
            .ToList();
    }

    private bool TryUpdateContractsInPlace(
        Control contractList,
        List<ContractClientData> ordered,
        int skipCost,
        string skipCurrency,
        int skipBalance)
    {
        if (contractList.ChildCount != ordered.Count || _contractCardOrder.Count != ordered.Count)
            return false;

        for (var i = 0; i < ordered.Count; i++)
        {
            var contract = ordered[i];
            if (!_contractCardsById.TryGetValue(contract.Id, out var card) ||
                !string.Equals(_contractCardOrder[i], contract.Id, StringComparison.Ordinal) ||
                !ReferenceEquals(contractList.GetChild(i), card))
                return false;
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            var contract = ordered[i];
            _contractCardsById[contract.Id].UpdateData(contract, skipCost, skipCurrency, skipBalance);
        }

        return true;
    }

    private void RebuildContracts(
        Control contractList,
        List<ContractClientData> ordered,
        int skipCost,
        string skipCurrency,
        int skipBalance)
    {
        var activeIds = new HashSet<string>();
        contractList.RemoveAllChildren();
        _contractCardOrder.Clear();

        for (var i = 0; i < ordered.Count; i++)
        {
            var contract = ordered[i];
            activeIds.Add(contract.Id);

            if (!_contractCardsById.TryGetValue(contract.Id, out var card))
            {
                card = CreateContractCard(contract, skipCost, skipCurrency, skipBalance);
                _contractCardsById[contract.Id] = card;
            }
            else
            {
                card.UpdateData(contract, skipCost, skipCurrency, skipBalance);
            }

            contractList.AddChild(card);
            _contractCardOrder.Add(contract.Id);
        }

        PruneContractCards(activeIds);
    }

    private NcContractCard CreateContractCard(ContractClientData contract, int skipCost, string skipCurrency, int skipBalance)
    {
        var card = new NcContractCard(contract, _proto, _sprites, skipCost, skipCurrency, skipBalance);
        card.OnClaim += id => OnContractClaim?.Invoke(id);
        card.OnTake += id => OnContractTake?.Invoke(id);
        card.OnSkip += id => OnContractSkip?.Invoke(id);
        card.OnRequestPinpointer += id => OnContractRequestPinpointer?.Invoke(id);
        return card;
    }

    private void ResetContractsListToEmpty(Control contractList)
    {
        contractList.RemoveAllChildren();
        PruneContractCards(Array.Empty<string>());
        _contractCardOrder.Clear();

        contractList.AddChild(
            new Label
            {
                Text = Loc.GetString("nc-store-contracts-empty"),
                HorizontalAlignment = HAlignment.Center,
                Margin = new(0, 8, 0, 0)
            });
    }

    private void PruneContractCards(IEnumerable<string> activeIds)
    {
        var active = activeIds is HashSet<string> set
            ? set
            : new HashSet<string>(activeIds);

        var staleIds = _contractCardsById.Keys
            .Where(id => !active.Contains(id))
            .ToArray();

        for (var i = 0; i < staleIds.Length; i++)
        {
            var id = staleIds[i];
            if (_contractCardsById.Remove(id, out var card))
                card.Parent?.RemoveChild(card);
        }
    }
}
