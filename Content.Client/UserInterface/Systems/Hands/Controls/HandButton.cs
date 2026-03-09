using Content.Client.UserInterface.Controls;
using Content.Shared.Hands.Components;

namespace Content.Client.UserInterface.Systems.Hands.Controls;

public sealed class HandButton : SlotControl
{
    public HandLocation HandLocation { get; }

    public HandButton(string handName, HandLocation handLocation)
    {
        HandLocation = handLocation;
        Name = "hand_" + handName;
        SlotName = handName;
        SetBackground(handLocation);
        FullButtonTexturePath = "HandSlotBackground";

        // TODO: slot_highlight_l.png and slot_highlight_r.png are placeholders (copies of slot_highlight.png) — replace with proper per-hand sprites
        HighlightTexturePath = handLocation switch
        {
            HandLocation.Left => "slot_highlight_l",
            HandLocation.Middle => "slot_highlight",
            HandLocation.Right => "slot_highlight_r",
            _ => "slot_highlight"
        };
    }

    private void SetBackground(HandLocation handLoc)
    {
        ButtonTexturePath = handLoc switch
        {
            HandLocation.Left => "Slots/hand_l",
            HandLocation.Middle => "Slots/hand_m",
            HandLocation.Right => "Slots/hand_r",
            _ => ButtonTexturePath
        };
    }
}
