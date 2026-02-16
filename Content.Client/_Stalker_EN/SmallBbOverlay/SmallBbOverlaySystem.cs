using Robust.Client.Debugging;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;

namespace Content.Client._Stalker_EN.SmallBbOverlay;

public sealed class SmallBbOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly SharedPhysicsSystem _sharedPhysicsSystem = default!;
    [Dependency] private readonly DebugPhysicsSystem _debugPhysicsSystem = default!;

    [Access]
    public bool Added = false;

    private bool _permanentlyRemoved = false;

    public void SetEnabled(bool value)
    {
        if (_permanentlyRemoved)
            return;

        if (Added == value)
            return;

        Added = value;
        if (Added)
        {
            _overlayManager.AddOverlay(new SmallBbOverlay(
                EntityManager,
                _eyeManager,
                _inputManager,
                _resourceCache,
                _sharedPhysicsSystem,
                _debugPhysicsSystem
            ));
        }
        else
            _overlayManager.RemoveOverlay<SmallBbOverlay>();
    }

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Shutdown()
    {
        SetEnabled(false);
        _permanentlyRemoved = true;

        base.Shutdown();
    }
}
