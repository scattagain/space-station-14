using System.Linq;
using Content.Client.Gameplay;
using Content.Client.Hands.Systems;
using Content.Client.Verbs;
using Content.Client.Viewport;
using Content.Shared.Verbs;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Systems.InteractionHint.Widgets;

public sealed class HintUiController : UIController, IOnSystemChanged<VerbSystem>
{
    public enum VerbType
    {
        Activate,
        AltActivate,
        Interact
    }

    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private VerbSystem? _verbSystem;
    private HintGui? Gui => UIManager.GetActiveUIWidgetOrNull<HintGui>();

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        UpdateHints();
    }

    public void OnSystemLoaded(VerbSystem system)
    {
        _verbSystem = system;
    }

    public void OnSystemUnloaded(VerbSystem system)
    {
        _verbSystem = null;
    }

    private void UpdateHints()
    {
        // Yoinked straight from InteractionOutlineSystem, which breaks the "Don’t copy paste code" convention
        // This *will* be changed... once I understand the codebase enough to make a "correct" implementation
        // Also, since this is the same code for the outline system, it doesn't target items in your inventory
        var currentState = _stateManager.CurrentState;
        if (currentState is not GameplayStateBase screen)
            return;

        EntityUid? entityToClick = null;
        if (_uiManager.CurrentlyHovered is IViewportControl vp
            && _inputManager.MouseScreenPosition.IsValid)
        {
            var mousePosWorld = vp.PixelToMap(_inputManager.MouseScreenPosition.Position);

            if (vp is ScalingViewport svp)
            {
                entityToClick = screen.GetClickedEntity(mousePosWorld, svp.Eye);
            }
            else
            {
                entityToClick = screen.GetClickedEntity(mousePosWorld);
            }
        }

        Gui?.UpdateHints(entityToClick);
    }

    public Verb? GetVerb(EntityUid? target, VerbType verbType)
    {
        if (_verbSystem == null)
            return null;
        var user = _playerManager.LocalEntity;
        if (_entityManager.Deleted(target) || _entityManager.Deleted(user))
            return null;

        var verbs = verbType switch
        {
            VerbType.Activate => _verbSystem.GetLocalVerbs(target.Value, user.Value, typeof(ActivationVerb)),
            VerbType.AltActivate => _verbSystem.GetLocalVerbs(target.Value, user.Value, typeof(AlternativeVerb)),
            VerbType.Interact => _verbSystem.GetLocalVerbs(target.Value, user.Value,
                new List<Type>() { typeof(InteractionVerb), typeof(ActivationVerb) }), // Interactions fallback to activations
            _ => throw new ArgumentOutOfRangeException(nameof(verbType), verbType, null)
        };

        if (verbs.Count == 0)
            return null;

        return verbs.First();
    }
}
