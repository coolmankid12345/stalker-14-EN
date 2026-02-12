using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Client._Stalker.Utilities.BoxExtensions;
using Content.Client.Hands.Systems;
using Content.Client.Items.Systems;
using Content.Client.Storage;
using Content.Client.Storage.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.Input;
using Content.Shared.Item;
using Content.Shared.Storage;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Collections;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Storage.Controls;

public sealed class StorageWindow : BaseWindow
{
    [Dependency] private readonly IEntityManager _entity = default!;
    private readonly StorageUIController _storageController;

    public EntityUid? StorageEntity;

    private readonly GridContainer _pieceGrid;
    private readonly GridContainer _backgroundGrid;
    private readonly GridContainer _sidebar;

    private Control _titleContainer;
    private Label _titleLabel;

    // Needs to be nullable in case a piece is in default spot.
    private readonly Dictionary<EntityUid, (ItemStorageLocation? Loc, ItemGridPiece Control)> _pieces = new();
    private readonly List<Control> _controlGrid = new();

    private ValueList<EntityUid> _contained = new();
    private ValueList<EntityUid> _toRemove = new();

    // Manually store this because you can't have a 0x0 GridContainer but we still need to add child controls for 1x1 containers.
    private Vector2i _pieceGridSize;

    private TextureButton? _backButton;

    private bool _isDirty;

    public event Action<GUIBoundKeyEventArgs, ItemGridPiece>? OnPiecePressed;
    public event Action<GUIBoundKeyEventArgs, ItemGridPiece>? OnPieceUnpressed;

    private readonly string _emptyTexturePath = "Storage/tile_empty";
    private Texture? _emptyTexture;
    private readonly string _blockedTexturePath = "Storage/tile_blocked";
    private Texture? _blockedTexture;
    private readonly string _emptyOpaqueTexturePath = "Storage/tile_empty_opaque";
    private Texture? _emptyOpaqueTexture;
    private readonly string _blockedOpaqueTexturePath = "Storage/tile_blocked_opaque";
    private Texture? _blockedOpaqueTexture;
    private readonly string _exitTexturePath = "Storage/exit";
    private Texture? _exitTexture;
    private readonly string _backTexturePath = "Storage/back";
    private Texture? _backTexture;
    private readonly string _sidebarTopTexturePath = "Storage/sidebar_top";
    private Texture? _sidebarTopTexture;
    private readonly string _sidebarMiddleTexturePath = "Storage/sidebar_mid";
    private Texture? _sidebarMiddleTexture;
    private readonly string _sidebarBottomTexturePath = "Storage/sidebar_bottom";
    private Texture? _sidebarBottomTexture;
    private readonly string _sidebarFatTexturePath = "Storage/sidebar_fat";
    private Texture? _sidebarFatTexture;

    // Stalker-Changes-Start

    public event Action? OnCraftButtonPressed;
    public event Action? OnDisassembleButtonPressed;

    private const string StalkerStoragePath = "/Textures/_Stalker/Interface/STDefault/Storage/";

    private readonly string _craftTexturePath = StalkerStoragePath + "craft";
    private Texture? _craftTexture;
    private readonly string _disassebleTexturePath = StalkerStoragePath + "disasseble"; // typo matches filename on disk
    private Texture? _disassembleTexture;

    private readonly string _addTexturePath = StalkerStoragePath + "tile_empty_add";
    private Texture? _addEmptyTexture;

    private readonly string _baseInteriorTexturePath = StalkerStoragePath + "tile_empty_";
    private Texture? _baseInteriorTexture;

    // Corner variants: "corner" = clean (diagonal present), "curved" = has inner notch (diagonal missing)
    private readonly string _cornerTopLeftPath = StalkerStoragePath + "tile_empty_corner_top_left";
    private Texture? _cornerTopLeftTexture;
    private readonly string _cornerTopRightPath = StalkerStoragePath + "tile_empty_corner_top_right";
    private Texture? _cornerTopRightTexture;
    private readonly string _cornerBottomLeftPath = StalkerStoragePath + "tile_empty_corner_bottom_left";
    private Texture? _cornerBottomLeftTexture;
    private readonly string _cornerBottomRightPath = StalkerStoragePath + "tile_empty_corner_bottom_right";
    private Texture? _cornerBottomRightTexture;

    private readonly string _curvedTopLeftPath = StalkerStoragePath + "tile_empty_curved_top_left";
    private Texture? _curvedTopLeftTexture;
    private readonly string _curvedTopRightPath = StalkerStoragePath + "tile_empty_curved_top_right";
    private Texture? _curvedTopRightTexture;
    private readonly string _curvedBottomLeftPath = StalkerStoragePath + "tile_empty_curved_bottom_left";
    private Texture? _curvedBottomLeftTexture;
    private readonly string _curvedBottomRightPath = StalkerStoragePath + "tile_empty_curved_bottom_right";
    private Texture? _curvedBottomRightTexture;

    // Edge variants: "edge" = clean, "T-shape" = both diag notches, "F-shape" = one diag notch
    private readonly string _edgeTopPath = StalkerStoragePath + "tile_empty_edge_top";
    private Texture? _edgeTopTexture;
    private readonly string _edgeBottomPath = StalkerStoragePath + "tile_empty_edge_bottom";
    private Texture? _edgeBottomTexture;
    private readonly string _edgeLeftPath = StalkerStoragePath + "tile_empty_edge_left";
    private Texture? _edgeLeftTexture;
    private readonly string _edgeRightPath = StalkerStoragePath + "tile_empty_edge_right";
    private Texture? _edgeRightTexture;

    private readonly string _tShapeTopPath = StalkerStoragePath + "tile_empty_T-shape_top";
    private Texture? _tShapeTopTexture;
    private readonly string _tShapeBottomPath = StalkerStoragePath + "tile_empty_T-shape_bottom";
    private Texture? _tShapeBottomTexture;
    private readonly string _tShapeLeftPath = StalkerStoragePath + "tile_empty_T-shape_left";
    private Texture? _tShapeLeftTexture;
    private readonly string _tShapeRightPath = StalkerStoragePath + "tile_empty_T-shape_right";
    private Texture? _tShapeRightTexture;

    private readonly string _fShapeTopPath = StalkerStoragePath + "tile_empty_F-shape_top";
    private Texture? _fShapeTopTexture;
    private readonly string _fShapeTopInvertedPath = StalkerStoragePath + "tile_empty_F-shape_top_inverted";
    private Texture? _fShapeTopInvertedTexture;
    private readonly string _fShapeBottomPath = StalkerStoragePath + "tile_empty_F-shape_bottom";
    private Texture? _fShapeBottomTexture;
    private readonly string _fShapeBottomInvertedPath = StalkerStoragePath + "tile_empty_F-shape_bottom_inverted";
    private Texture? _fShapeBottomInvertedTexture;
    private readonly string _fShapeLeftPath = StalkerStoragePath + "tile_empty_F-shape_left";
    private Texture? _fShapeLeftTexture;
    private readonly string _fShapeLeftInvertedPath = StalkerStoragePath + "tile_empty_F-shape_left_inverted";
    private Texture? _fShapeLeftInvertedTexture;
    private readonly string _fShapeRightPath = StalkerStoragePath + "tile_empty_F-shape_right";
    private Texture? _fShapeRightTexture;
    private readonly string _fShapeRightInvertedPath = StalkerStoragePath + "tile_empty_F-shape_right_inverted";
    private Texture? _fShapeRightInvertedTexture;

    private readonly string _corridorHorizontalPath = StalkerStoragePath + "tile_empty_corridor_horizontal";
    private Texture? _corridorHorizontalTexture;
    private readonly string _corridorVerticalPath = StalkerStoragePath + "tile_empty_corridor_vertical";
    private Texture? _corridorVerticalTexture;

    private readonly string _uShapeTopPath = StalkerStoragePath + "tile_empty_U-shape_top";
    private Texture? _uShapeTopTexture;
    private readonly string _uShapeBottomPath = StalkerStoragePath + "tile_empty_U-shape_bottom";
    private Texture? _uShapeBottomTexture;
    private readonly string _uShapeLeftPath = StalkerStoragePath + "tile_empty_U-shape_left";
    private Texture? _uShapeLeftTexture;
    private readonly string _uShapeRightPath = StalkerStoragePath + "tile_empty_U-shape_right";
    private Texture? _uShapeRightTexture;

    private readonly string _isolatedPath = StalkerStoragePath + "tile_empty_isolated";
    private Texture? _isolatedTexture;

    private readonly string _plusShapePath = StalkerStoragePath + "tile_empty_plus-shape";
    private Texture? _plusShapeTexture;

    private readonly string _innerTopLeftPath = StalkerStoragePath + "tile_empty_inner_top_left";
    private Texture? _innerTopLeftTexture;
    private readonly string _innerTopRightPath = StalkerStoragePath + "tile_empty_inner_top_right";
    private Texture? _innerTopRightTexture;
    private readonly string _innerBottomLeftPath = StalkerStoragePath + "tile_empty_inner_bottom_left";
    private Texture? _innerBottomLeftTexture;
    private readonly string _innerBottomRightPath = StalkerStoragePath + "tile_empty_inner_bottom_right";
    private Texture? _innerBottomRightTexture;

    private readonly string _innerDoubleTopPath = StalkerStoragePath + "tile_empty_inner_double_top";
    private Texture? _innerDoubleTopTexture;
    private readonly string _innerDoubleBottomPath = StalkerStoragePath + "tile_empty_inner_double_bottom";
    private Texture? _innerDoubleBottomTexture;
    private readonly string _innerDoubleLeftPath = StalkerStoragePath + "tile_empty_inner_double_left";
    private Texture? _innerDoubleLeftTexture;
    private readonly string _innerDoubleRightPath = StalkerStoragePath + "tile_empty_inner_double_right";
    private Texture? _innerDoubleRightTexture;
    private readonly string _innerDoubleDiagonalPath = StalkerStoragePath + "tile_empty_inner_double_diagonal";
    private Texture? _innerDoubleDiagonalTexture;
    private readonly string _innerDoubleDiagonalReversePath = StalkerStoragePath + "tile_empty_inner_double_diagonal_reverse";
    private Texture? _innerDoubleDiagonalReverseTexture;

    // Inner triples: named by the sole SURVIVING diagonal
    private readonly string _innerTripleTopLeftPath = StalkerStoragePath + "tile_empty_inner_triple_top_left";
    private Texture? _innerTripleTopLeftTexture;
    private readonly string _innerTripleTopRightPath = StalkerStoragePath + "tile_empty_inner_triple_top_right";
    private Texture? _innerTripleTopRightTexture;
    private readonly string _innerTripleBottomLeftPath = StalkerStoragePath + "tile_empty_inner_triple_bottom_left";
    private Texture? _innerTripleBottomLeftTexture;
    private readonly string _innerTripleBottomRightPath = StalkerStoragePath + "tile_empty_inner_triple_bottom_right";
    private Texture? _innerTripleBottomRightTexture;
    // Stalker-Changes-End

    public StorageWindow()
    {
        IoCManager.InjectDependencies(this);
        Resizable = false;

        _storageController = UserInterfaceManager.GetUIController<StorageUIController>();

        OnThemeUpdated();

        MouseFilter = MouseFilterMode.Stop;

        _sidebar = new GridContainer
        {
            Name = "SideBar",
            HSeparationOverride = 0,
            VSeparationOverride = 0,
            Columns = 1
        };

        _pieceGrid = new GridContainer
        {
            Name = "PieceGrid",
            HSeparationOverride = 0,
            VSeparationOverride = 0
        };

        _backgroundGrid = new GridContainer
        {
            Name = "BackgroundGrid",
            HSeparationOverride = 0,
            VSeparationOverride = 0
        };

        _titleLabel = new Label()
        {
            HorizontalExpand = true,
            Name = "StorageLabel",
            ClipText = true,
            Text = Loc.GetString("comp-storage-window-dummy"),
            StyleClasses =
            {
                "FancyWindowTitle",
            }
        };

        _titleContainer = new PanelContainer()
        {
            StyleClasses =
            {
                "WindowHeadingBackground"
            },
            Children =
            {
                _titleLabel
            }
        };

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Children =
            {
                _titleContainer,
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Children =
                    {
                        _sidebar,
                        new Control
                        {
                            Children =
                            {
                                _backgroundGrid,
                                _pieceGrid
                            }
                        }
                    }
                }
            }
        };

        AddChild(container);
    }

    protected override void OnThemeUpdated()
    {
        base.OnThemeUpdated();

        // Stalker-Changes-Start
        _craftTexture = Theme.ResolveTextureOrNull(_craftTexturePath)?.Texture;
        _disassembleTexture = Theme.ResolveTextureOrNull(_disassebleTexturePath)?.Texture;
        _addEmptyTexture = Theme.ResolveTextureOrNull(_addTexturePath)?.Texture;

        _baseInteriorTexture = Theme.ResolveTextureOrNull(_baseInteriorTexturePath)?.Texture;

        _cornerTopLeftTexture = Theme.ResolveTextureOrNull(_cornerTopLeftPath)?.Texture;
        _cornerTopRightTexture = Theme.ResolveTextureOrNull(_cornerTopRightPath)?.Texture;
        _cornerBottomLeftTexture = Theme.ResolveTextureOrNull(_cornerBottomLeftPath)?.Texture;
        _cornerBottomRightTexture = Theme.ResolveTextureOrNull(_cornerBottomRightPath)?.Texture;

        _curvedTopLeftTexture = Theme.ResolveTextureOrNull(_curvedTopLeftPath)?.Texture;
        _curvedTopRightTexture = Theme.ResolveTextureOrNull(_curvedTopRightPath)?.Texture;
        _curvedBottomLeftTexture = Theme.ResolveTextureOrNull(_curvedBottomLeftPath)?.Texture;
        _curvedBottomRightTexture = Theme.ResolveTextureOrNull(_curvedBottomRightPath)?.Texture;

        _edgeTopTexture = Theme.ResolveTextureOrNull(_edgeTopPath)?.Texture;
        _edgeBottomTexture = Theme.ResolveTextureOrNull(_edgeBottomPath)?.Texture;
        _edgeLeftTexture = Theme.ResolveTextureOrNull(_edgeLeftPath)?.Texture;
        _edgeRightTexture = Theme.ResolveTextureOrNull(_edgeRightPath)?.Texture;

        _tShapeTopTexture = Theme.ResolveTextureOrNull(_tShapeTopPath)?.Texture;
        _tShapeBottomTexture = Theme.ResolveTextureOrNull(_tShapeBottomPath)?.Texture;
        _tShapeLeftTexture = Theme.ResolveTextureOrNull(_tShapeLeftPath)?.Texture;
        _tShapeRightTexture = Theme.ResolveTextureOrNull(_tShapeRightPath)?.Texture;

        _fShapeTopTexture = Theme.ResolveTextureOrNull(_fShapeTopPath)?.Texture;
        _fShapeTopInvertedTexture = Theme.ResolveTextureOrNull(_fShapeTopInvertedPath)?.Texture;
        _fShapeBottomTexture = Theme.ResolveTextureOrNull(_fShapeBottomPath)?.Texture;
        _fShapeBottomInvertedTexture = Theme.ResolveTextureOrNull(_fShapeBottomInvertedPath)?.Texture;
        _fShapeLeftTexture = Theme.ResolveTextureOrNull(_fShapeLeftPath)?.Texture;
        _fShapeLeftInvertedTexture = Theme.ResolveTextureOrNull(_fShapeLeftInvertedPath)?.Texture;
        _fShapeRightTexture = Theme.ResolveTextureOrNull(_fShapeRightPath)?.Texture;
        _fShapeRightInvertedTexture = Theme.ResolveTextureOrNull(_fShapeRightInvertedPath)?.Texture;

        _corridorHorizontalTexture = Theme.ResolveTextureOrNull(_corridorHorizontalPath)?.Texture;
        _corridorVerticalTexture = Theme.ResolveTextureOrNull(_corridorVerticalPath)?.Texture;

        _uShapeTopTexture = Theme.ResolveTextureOrNull(_uShapeTopPath)?.Texture;
        _uShapeBottomTexture = Theme.ResolveTextureOrNull(_uShapeBottomPath)?.Texture;
        _uShapeLeftTexture = Theme.ResolveTextureOrNull(_uShapeLeftPath)?.Texture;
        _uShapeRightTexture = Theme.ResolveTextureOrNull(_uShapeRightPath)?.Texture;

        _isolatedTexture = Theme.ResolveTextureOrNull(_isolatedPath)?.Texture;
        _plusShapeTexture = Theme.ResolveTextureOrNull(_plusShapePath)?.Texture;

        _innerTopLeftTexture = Theme.ResolveTextureOrNull(_innerTopLeftPath)?.Texture;
        _innerTopRightTexture = Theme.ResolveTextureOrNull(_innerTopRightPath)?.Texture;
        _innerBottomLeftTexture = Theme.ResolveTextureOrNull(_innerBottomLeftPath)?.Texture;
        _innerBottomRightTexture = Theme.ResolveTextureOrNull(_innerBottomRightPath)?.Texture;

        _innerDoubleTopTexture = Theme.ResolveTextureOrNull(_innerDoubleTopPath)?.Texture;
        _innerDoubleBottomTexture = Theme.ResolveTextureOrNull(_innerDoubleBottomPath)?.Texture;
        _innerDoubleLeftTexture = Theme.ResolveTextureOrNull(_innerDoubleLeftPath)?.Texture;
        _innerDoubleRightTexture = Theme.ResolveTextureOrNull(_innerDoubleRightPath)?.Texture;
        _innerDoubleDiagonalTexture = Theme.ResolveTextureOrNull(_innerDoubleDiagonalPath)?.Texture;
        _innerDoubleDiagonalReverseTexture = Theme.ResolveTextureOrNull(_innerDoubleDiagonalReversePath)?.Texture;

        _innerTripleTopLeftTexture = Theme.ResolveTextureOrNull(_innerTripleTopLeftPath)?.Texture;
        _innerTripleTopRightTexture = Theme.ResolveTextureOrNull(_innerTripleTopRightPath)?.Texture;
        _innerTripleBottomLeftTexture = Theme.ResolveTextureOrNull(_innerTripleBottomLeftPath)?.Texture;
        _innerTripleBottomRightTexture = Theme.ResolveTextureOrNull(_innerTripleBottomRightPath)?.Texture;
        // Stalker-Changes-End
        _emptyTexture = Theme.ResolveTextureOrNull(_emptyTexturePath)?.Texture;
        _blockedTexture = Theme.ResolveTextureOrNull(_blockedTexturePath)?.Texture;
        _emptyOpaqueTexture = Theme.ResolveTextureOrNull(_emptyOpaqueTexturePath)?.Texture;
        _blockedOpaqueTexture = Theme.ResolveTextureOrNull(_blockedOpaqueTexturePath)?.Texture;
        _exitTexture = Theme.ResolveTextureOrNull(_exitTexturePath)?.Texture;
        _backTexture = Theme.ResolveTextureOrNull(_backTexturePath)?.Texture;
        _sidebarTopTexture = Theme.ResolveTextureOrNull(_sidebarTopTexturePath)?.Texture;
        _sidebarMiddleTexture = Theme.ResolveTextureOrNull(_sidebarMiddleTexturePath)?.Texture;
        _sidebarBottomTexture = Theme.ResolveTextureOrNull(_sidebarBottomTexturePath)?.Texture;
        _sidebarFatTexture = Theme.ResolveTextureOrNull(_sidebarFatTexturePath)?.Texture;
    }

    public void UpdateContainer(Entity<StorageComponent>? entity)
    {
        Visible = entity != null;
        StorageEntity = entity;
        if (entity == null)
            return;

        if (UserInterfaceManager.GetUIController<StorageUIController>().WindowTitle)
        {
            _titleLabel.Text = Identity.Name(entity.Value, _entity);
            _titleContainer.Visible = true;
        }
        else
        {
            _titleContainer.Visible = false;
        }

        BuildGridRepresentation();
    }

    private void CloseParent()
    {
        if (StorageEntity == null)
            return;

        var containerSystem = _entity.System<SharedContainerSystem>();
        var uiSystem = _entity.System<UserInterfaceSystem>();

        if (containerSystem.TryGetContainingContainer(StorageEntity.Value, out var container) &&
            _entity.TryGetComponent(container.Owner, out StorageComponent? storage) &&
            storage.Container.Contains(StorageEntity.Value) &&
            uiSystem
                .TryGetOpenUi<StorageBoundUserInterface>(container.Owner,
                    StorageComponent.StorageUiKey.Key,
                    out var parentBui))
        {
            parentBui.CloseWindow(Position);
        }
    }

    private void BuildGridRepresentation()
    {
        if (!_entity.TryGetComponent<StorageComponent>(StorageEntity, out var comp) || comp.Grid.Count == 0)
            return;

        var boundingGrid = comp.Grid.GetBoundingBox();

        BuildBackground();

        #region Sidebar
        _sidebar.Children.Clear();
        var rows = boundingGrid.Height + 1;
        _sidebar.Rows = rows;

        // Stalker-Changes-Start
        var craftButton = new TextureButton
        {
            TextureNormal = _craftTexture,
            Scale = new Vector2(2, 2),
            Visible = comp.Craft,
        };
        craftButton.OnPressed += _ => OnCraftButtonPressed?.Invoke();
        var diassembleButton = new TextureButton
        {
            TextureNormal = _disassembleTexture,
            Scale = new Vector2(2, 2),
            Visible = comp.Disassemble
        };
        diassembleButton.OnPressed += _ => OnDisassembleButtonPressed?.Invoke();

        var craftContainer = new BoxContainer
        {
            Children =
            {
                new TextureRect
                {
                    Texture = boundingGrid.Height == 1 ? _sidebarBottomTexture : _sidebarMiddleTexture,
                    TextureScale = new Vector2(2, 2),
                    Children =
                    {
                        craftButton
                    }
                }
            }
        };
        var disassembleContainer = new BoxContainer
        {
            Children =
            {
                new TextureRect
                {
                    Texture = boundingGrid.Height == 1 ? _sidebarBottomTexture : _sidebarMiddleTexture,
                    TextureScale = new Vector2(2, 2),
                    Children =
                    {
                        diassembleButton
                    }
                }
            }
        };

        // Stalker-Changes-End

        var exitButton = new TextureButton
        {
            Name = "ExitButton",
            TextureNormal = _exitTexture,
            Scale = new Vector2(2, 2),
        };
        exitButton.OnPressed += _ =>
        {
            // Close ourselves and all parent BUIs.
            Close();
            CloseParent();
        };
        exitButton.OnKeyBindDown += args =>
        {
            // it just makes sense...
            if (!args.Handled && args.Function == ContentKeyFunctions.ActivateItemInWorld)
            {
                Close();
                CloseParent();
                args.Handle();
            }
        };

        var exitContainer = new BoxContainer
        {
            Name = "ExitContainer",
            Children =
            {
                new TextureRect
                {
                    Texture = boundingGrid.Height != 0
                        ? _sidebarTopTexture
                        : _sidebarFatTexture,
                    TextureScale = new Vector2(2, 2),
                    Children =
                    {
                        exitButton
                    }
                }
            }
        };

        _sidebar.AddChild(exitContainer);
        _sidebar.AddChild(craftContainer); // Stalker-Changes
        _sidebar.AddChild(disassembleContainer); // Stalker-Changes
        var offset = 4; // Stalker-Changes

        if (_entity.System<StorageSystem>().NestedStorage && rows > 0)
        {
            _backButton = new TextureButton
            {
                TextureNormal = _backTexture,
                Scale = new Vector2(2, 2),
            };
            _backButton.OnPressed += _ =>
            {
                var containerSystem = _entity.System<SharedContainerSystem>();

                if (containerSystem.TryGetContainingContainer(StorageEntity.Value, out var container) &&
                    _entity.TryGetComponent(container.Owner, out StorageComponent? storage) &&
                    storage.Container.Contains(StorageEntity.Value))
                {
                    Close();

                    if (_entity.System<SharedUserInterfaceSystem>()
                        .TryGetOpenUi<StorageBoundUserInterface>(container.Owner,
                            StorageComponent.StorageUiKey.Key,
                            out var parentBui))
                    {
                        parentBui.Show(Position);
                    }
                }
            };

            var backContainer = new BoxContainer
            {
                Name = "ExitContainer",
                Children =
                {
                    new TextureRect
                    {
                        Texture = rows > 2 ? _sidebarMiddleTexture : _sidebarBottomTexture,
                        TextureScale = new Vector2(2, 2),
                        Children =
                        {
                            _backButton,
                        }
                    }
                }
            };

            _sidebar.AddChild(backContainer);
        }

        var fillerRows = rows - offset;

        for (var i = 0; i < fillerRows; i++)
        {
            _sidebar.AddChild(new TextureRect
            {
                Texture = i != (fillerRows - 1) ? _sidebarMiddleTexture : _sidebarBottomTexture,
                TextureScale = new Vector2(2, 2),
            });
        }

        #endregion

        FlagDirty();
    }

    public void BuildBackground()
    {
        if (!_entity.TryGetComponent<StorageComponent>(StorageEntity, out var comp) || !comp.Grid.Any())
            return;

        var boundingGrid = comp.Grid.GetBoundingBox();

        var blockedTexture = _storageController.OpaqueStorageWindow
            ? _blockedOpaqueTexture
            : _blockedTexture;

        // Stalker-Changes-Start
        var gridPoints = new HashSet<Vector2i>(comp.Grid.SelectMany(BoxExtensions.GetAllPoints));
        // Stalker-Changes-End

        _backgroundGrid.Children.Clear();
        _backgroundGrid.Rows = boundingGrid.Height + 1;
        _backgroundGrid.Columns = boundingGrid.Width + 1;
        for (var y = boundingGrid.Bottom; y <= boundingGrid.Top; y++)
        {
            for (var x = boundingGrid.Left; x <= boundingGrid.Right; x++)
            {
                var pos = new Vector2i(x, y);
                var texture = gridPoints.Contains(pos)
                    ? GetAppropriateTexture(gridPoints, pos) // Stalker-Changes
                    : blockedTexture;

                _backgroundGrid.AddChild(new TextureRect
                {
                    Texture = texture,
                    TextureScale = new Vector2(2, 2)
                });
            }
        }
    }

    public void Reclaim(ItemStorageLocation location, ItemGridPiece draggingGhost)
    {
        draggingGhost.OnPiecePressed += OnPiecePressed;
        draggingGhost.OnPieceUnpressed += OnPieceUnpressed;
        _pieces[draggingGhost.Entity] = (location, draggingGhost);
        draggingGhost.Location = location;
        var controlIndex = GetGridIndex(draggingGhost);
        _controlGrid[controlIndex].AddChild(draggingGhost);
    }

    private int GetGridIndex(ItemGridPiece piece)
    {
        return piece.Location.Position.X + piece.Location.Position.Y * _pieceGrid.Columns;
    }

    public void FlagDirty()
    {
        _isDirty = true;
    }

    public void RemoveGrid(ItemGridPiece control)
    {
        control.Orphan();
        _pieces.Remove(control.Entity);
        control.OnPiecePressed -= OnPiecePressed;
        control.OnPieceUnpressed -= OnPieceUnpressed;
    }

    public void BuildItemPieces()
    {
        if (!_entity.TryGetComponent<StorageComponent>(StorageEntity, out var storageComp))
            return;

        if (storageComp.Grid.Count == 0)
            return;

        var boundingGrid = storageComp.Grid.GetBoundingBox();
        var size = _emptyTexture!.Size * 2;
        _contained.Clear();
        _contained.AddRange(storageComp.Container.ContainedEntities.Reverse());

        var width = boundingGrid.Width + 1;
        var height = boundingGrid.Height + 1;

        // Build the grid representation
         if (_pieceGrid.Rows != _pieceGridSize.Y || _pieceGrid.Columns != _pieceGridSize.X)
        {
            _pieceGrid.Rows = height;
            _pieceGrid.Columns = width;
            _controlGrid.Clear();

            for (var y = boundingGrid.Bottom; y <= boundingGrid.Top; y++)
            {
                for (var x = boundingGrid.Left; x <= boundingGrid.Right; x++)
                {
                    var control = new Control
                    {
                        MinSize = size
                    };

                    _controlGrid.Add(control);
                    _pieceGrid.AddChild(control);
                }
            }
        }

        _pieceGridSize = new(width, height);
        _toRemove.Clear();

        // Remove entities no longer relevant / Update existing ones
        foreach (var (ent, data) in _pieces)
        {
            if (storageComp.StoredItems.TryGetValue(ent, out var updated))
            {
                data.Control.Marked = IsMarked(ent);

                if (data.Loc.Equals(updated))
                {
                    DebugTools.Assert(data.Control.Location == updated);
                    continue;
                }

                // Update
                data.Control.Location = updated;
                var index = GetGridIndex(data.Control);
                data.Control.Orphan();
                _controlGrid[index].AddChild(data.Control);
                _pieces[ent] = (updated, data.Control);
                continue;
            }

            _toRemove.Add(ent);
        }

        foreach (var ent in _toRemove)
        {
            _pieces.Remove(ent, out var data);
            data.Control.Orphan();
        }

        // Add new ones
        foreach (var (ent, loc) in storageComp.StoredItems)
        {
            if (_pieces.TryGetValue(ent, out var existing))
            {
                DebugTools.Assert(existing.Loc == loc);
                continue;
            }

            if (_entity.TryGetComponent<ItemComponent>(ent, out var itemEntComponent))
            {
                var gridPiece = new ItemGridPiece((ent, itemEntComponent), loc, _entity)
                {
                    MinSize = size,
                    Marked = IsMarked(ent),
                };
                gridPiece.OnPiecePressed += OnPiecePressed;
                gridPiece.OnPieceUnpressed += OnPieceUnpressed;
                var controlIndex = loc.Position.X + loc.Position.Y * (boundingGrid.Width + 1);

                _controlGrid[controlIndex].AddChild(gridPiece);
                _pieces[ent] = (loc, gridPiece);
            }
        }
    }

    private ItemGridPieceMarks? IsMarked(EntityUid uid)
    {
        return _contained.IndexOf(uid) switch
        {
            0 => ItemGridPieceMarks.First,
            1 => ItemGridPieceMarks.Second,
            _ => null,
        };
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!IsOpen)
            return;

        if (_isDirty)
        {
            _isDirty = false;
            BuildItemPieces();
        }

        var containerSystem = _entity.System<SharedContainerSystem>();

        if (_backButton != null)
        {
            if (StorageEntity != null && _entity.System<StorageSystem>().NestedStorage)
            {
                // If parent container nests us then show back button
                if (containerSystem.TryGetContainingContainer(StorageEntity.Value, out var container) &&
                    _entity.TryGetComponent(container.Owner, out StorageComponent? storageComp) && storageComp.Container.Contains(StorageEntity.Value))
                {
                    _backButton.Visible = true;
                }
                else
                {
                    _backButton.Visible = false;
                }
            }
            // Hide the button.
            else
            {
                _backButton.Visible = false;
            }
        }

        var itemSystem = _entity.System<ItemSystem>();
        var storageSystem = _entity.System<StorageSystem>();
        var handsSystem = _entity.System<HandsSystem>();

        foreach (var child in _backgroundGrid.Children)
        {
            child.ModulateSelfOverride = Color.FromHex("#FFFFFF"); // Stalker-Changes | White because default #222222 makes it black, IDK why.
        }

        if (UserInterfaceManager.CurrentlyHovered is StorageWindow con && con != this)
            return;

        if (!_entity.TryGetComponent<StorageComponent>(StorageEntity, out var storageComponent))
            return;

        EntityUid currentEnt;
        ItemStorageLocation currentLocation;
        var usingInHand = false;
        if (_storageController.IsDragging && _storageController.DraggingGhost is { } dragging)
        {
            currentEnt = dragging.Entity;
            currentLocation = dragging.Location;
        }
        else if (handsSystem.GetActiveHandEntity() is { } handEntity &&
                 storageSystem.CanInsert(StorageEntity.Value, handEntity, out _, storageComp: storageComponent, ignoreLocation: true))
        {
            currentEnt = handEntity;
            currentLocation = new ItemStorageLocation(_storageController.DraggingRotation, Vector2i.Zero);
            usingInHand = true;
        }
        else
        {
            return;
        }

        if (!_entity.TryGetComponent<ItemComponent>(currentEnt, out var itemComp))
            return;

        var origin = GetMouseGridPieceLocation((currentEnt, itemComp), currentLocation);

        var itemShape = itemSystem.GetAdjustedItemShape(
            (currentEnt, itemComp),
            currentLocation.Rotation,
            origin);
        var itemBounding = itemShape.GetBoundingBox();

        var validLocation = storageSystem.ItemFitsInGridLocation(
            (currentEnt, itemComp),
            (StorageEntity.Value, storageComponent),
            origin,
            currentLocation.Rotation);

        foreach (var locations in storageComponent.SavedLocations)
        {
            if (!_entity.TryGetComponent<MetaDataComponent>(currentEnt, out var meta) || meta.EntityName != locations.Key)
                continue;

            float spot = 0;
            var marked = new ValueList<Control>();

            foreach (var location in locations.Value)
            {
                var shape = itemSystem.GetAdjustedItemShape(currentEnt, location);
                var bound = shape.GetBoundingBox();

                var spotFree = storageSystem.ItemFitsInGridLocation(currentEnt, StorageEntity.Value, location);

                if (spotFree)
                    spot++;

                for (var y = bound.Bottom; y <= bound.Top; y++)
                {
                    for (var x = bound.Left; x <= bound.Right; x++)
                    {
                        if (TryGetBackgroundCell(x, y, out var cell) && shape.Contains(x, y) && !marked.Contains(cell))
                        {
                            marked.Add(cell);
                            cell.ModulateSelfOverride = spotFree
                                ? Color.FromHsv(new Vector4(0.18f, 1 / spot, 0.5f / spot + 0.5f, 1f))
                                : Color.FromHex("#2222CC");
                        }
                    }
                }
            }
        }

        var validColor = usingInHand ? Color.Goldenrod : Color.FromHex("#1E8000");

        for (var y = itemBounding.Bottom; y <= itemBounding.Top; y++)
        {
            for (var x = itemBounding.Left; x <= itemBounding.Right; x++)
            {
                if (TryGetBackgroundCell(x, y, out var cell) && itemShape.Contains(x, y))
                {
                    cell.ModulateSelfOverride = validLocation ? validColor : Color.FromHex("#B40046");
                }
            }
        }
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        if (_storageController.StaticStorageUIEnabled)
            return DragMode.None;

        if (_sidebar.SizeBox.Contains(relativeMousePos - _sidebar.Position))
        {
            return DragMode.Move;
        }

        return DragMode.None;
    }

    public Vector2i GetMouseGridPieceLocation(Entity<ItemComponent?> entity, ItemStorageLocation location)
    {
        var origin = Vector2i.Zero;

        if (StorageEntity != null)
            origin = _entity.GetComponent<StorageComponent>(StorageEntity.Value).Grid.GetBoundingBox().BottomLeft;

        var textureSize = (Vector2) _emptyTexture!.Size * 2;
        var position = ((UserInterfaceManager.MousePositionScaled.Position
                         - _backgroundGrid.GlobalPosition
                         - ItemGridPiece.GetCenterOffset(entity, location, _entity) * 2
                         + textureSize / 2f)
                        / textureSize).Floored() + origin;
        return position;
    }

    public bool TryGetBackgroundCell(int x, int y, [NotNullWhen(true)] out Control? cell)
    {
        cell = null;

        if (!_entity.TryGetComponent<StorageComponent>(StorageEntity, out var storageComponent))
            return false;
        var boundingBox = storageComponent.Grid.GetBoundingBox();
        x -= boundingBox.Left;
        y -= boundingBox.Bottom;

        if (x < 0 ||
            x >= _backgroundGrid.Columns ||
            y < 0 ||
            y >= _backgroundGrid.Rows)
        {
            return false;
        }

        cell = _backgroundGrid.GetChild(y * _backgroundGrid.Columns + x);
        return true;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (!IsOpen)
            return;

        var storageSystem = _entity.System<StorageSystem>();
        var handsSystem = _entity.System<HandsSystem>();

        if (args.Function == ContentKeyFunctions.MoveStoredItem && StorageEntity != null)
        {
            if (handsSystem.GetActiveHandEntity() is { } handEntity &&
                storageSystem.CanInsert(StorageEntity.Value, handEntity, out _))
            {
                var pos = GetMouseGridPieceLocation((handEntity, null),
                    new ItemStorageLocation(_storageController.DraggingRotation, Vector2i.Zero));

                var insertLocation = new ItemStorageLocation(_storageController.DraggingRotation, pos);
                if (storageSystem.ItemFitsInGridLocation(
                        (handEntity, null),
                        (StorageEntity.Value, null),
                        insertLocation))
                {
                    _entity.RaisePredictiveEvent(new StorageInsertItemIntoLocationEvent(
                        _entity.GetNetEntity(handEntity),
                        _entity.GetNetEntity(StorageEntity.Value),
                        insertLocation));
                    _storageController.DraggingRotation = Angle.Zero;
                    args.Handle();
                }
            }
        }
    }

    // Stalker-Changes-Start

    /// <remarks>
    /// GridContainer renders y=Bottom at visual top of screen, so n=y-1, s=y+1 in screen-space.
    /// Texture names describe where the border IS (opposite of where the neighbors are).
    /// </remarks>
    private Texture? GetAppropriateTexture(HashSet<Vector2i> gridPoints, Vector2i position)
    {
        var n = gridPoints.Contains(new Vector2i(position.X, position.Y - 1));
        var s = gridPoints.Contains(new Vector2i(position.X, position.Y + 1));
        var w = gridPoints.Contains(new Vector2i(position.X - 1, position.Y));
        var e = gridPoints.Contains(new Vector2i(position.X + 1, position.Y));

        var cardinals = (n ? 1 : 0) + (s ? 1 : 0) + (w ? 1 : 0) + (e ? 1 : 0);

        switch (cardinals)
        {
            case 0:
                return _isolatedTexture ?? _addEmptyTexture;

            case 1:
                if (n) return _uShapeBottomTexture ?? _addEmptyTexture;
                if (s) return _uShapeTopTexture ?? _addEmptyTexture;
                if (w) return _uShapeRightTexture ?? _addEmptyTexture;
                return _uShapeLeftTexture ?? _addEmptyTexture;

            case 2:
            {
                if (n && s) return _corridorHorizontalTexture ?? _addEmptyTexture;
                if (w && e) return _corridorVerticalTexture ?? _addEmptyTexture;

                // Diagonal present → clean corner, diagonal missing → curved (has inner notch)
                if (s && e)
                {
                    var seD = gridPoints.Contains(new Vector2i(position.X + 1, position.Y + 1));
                    return seD
                        ? _cornerTopLeftTexture ?? _curvedTopLeftTexture ?? _addEmptyTexture
                        : _curvedTopLeftTexture ?? _addEmptyTexture;
                }
                if (s && w)
                {
                    var swD = gridPoints.Contains(new Vector2i(position.X - 1, position.Y + 1));
                    return swD
                        ? _cornerTopRightTexture ?? _curvedTopRightTexture ?? _addEmptyTexture
                        : _curvedTopRightTexture ?? _addEmptyTexture;
                }
                if (n && e)
                {
                    var neD = gridPoints.Contains(new Vector2i(position.X + 1, position.Y - 1));
                    return neD
                        ? _cornerBottomLeftTexture ?? _curvedBottomLeftTexture ?? _addEmptyTexture
                        : _curvedBottomLeftTexture ?? _addEmptyTexture;
                }
                {
                    var nwD = gridPoints.Contains(new Vector2i(position.X - 1, position.Y - 1));
                    return nwD
                        ? _cornerBottomRightTexture ?? _curvedBottomRightTexture ?? _addEmptyTexture
                        : _curvedBottomRightTexture ?? _addEmptyTexture;
                }
            }

            case 3:
            {
                // Both diags present → clean edge, one missing → F-shape, both missing → T-shape
                var nw = n && w && gridPoints.Contains(new Vector2i(position.X - 1, position.Y - 1));
                var ne = n && e && gridPoints.Contains(new Vector2i(position.X + 1, position.Y - 1));
                var sw = s && w && gridPoints.Contains(new Vector2i(position.X - 1, position.Y + 1));
                var se = s && e && gridPoints.Contains(new Vector2i(position.X + 1, position.Y + 1));

                if (!n)
                {
                    if (sw && se) return _edgeTopTexture ?? _tShapeTopTexture ?? _addEmptyTexture;
                    if (!sw && se) return _fShapeTopTexture ?? _tShapeTopTexture ?? _addEmptyTexture;
                    if (sw && !se) return _fShapeTopInvertedTexture ?? _tShapeTopTexture ?? _addEmptyTexture;
                    return _tShapeTopTexture ?? _addEmptyTexture;
                }

                if (!s)
                {
                    if (nw && ne) return _edgeBottomTexture ?? _tShapeBottomTexture ?? _addEmptyTexture;
                    if (!nw && ne) return _fShapeBottomInvertedTexture ?? _tShapeBottomTexture ?? _addEmptyTexture;
                    if (nw && !ne) return _fShapeBottomTexture ?? _tShapeBottomTexture ?? _addEmptyTexture;
                    return _tShapeBottomTexture ?? _addEmptyTexture;
                }

                if (!w)
                {
                    if (ne && se) return _edgeLeftTexture ?? _tShapeLeftTexture ?? _addEmptyTexture;
                    if (!ne && se) return _fShapeLeftInvertedTexture ?? _tShapeLeftTexture ?? _addEmptyTexture;
                    if (ne && !se) return _fShapeLeftTexture ?? _tShapeLeftTexture ?? _addEmptyTexture;
                    return _tShapeLeftTexture ?? _addEmptyTexture;
                }

                if (nw && sw) return _edgeRightTexture ?? _tShapeRightTexture ?? _addEmptyTexture;
                if (!nw && sw) return _fShapeRightTexture ?? _tShapeRightTexture ?? _addEmptyTexture;
                if (nw && !sw) return _fShapeRightInvertedTexture ?? _tShapeRightTexture ?? _addEmptyTexture;
                return _tShapeRightTexture ?? _addEmptyTexture;
            }

            case 4:
            {
                var nwD = gridPoints.Contains(new Vector2i(position.X - 1, position.Y - 1));
                var neD = gridPoints.Contains(new Vector2i(position.X + 1, position.Y - 1));
                var swD = gridPoints.Contains(new Vector2i(position.X - 1, position.Y + 1));
                var seD = gridPoints.Contains(new Vector2i(position.X + 1, position.Y + 1));

                var missing = (!nwD ? 1 : 0) + (!neD ? 1 : 0) + (!swD ? 1 : 0) + (!seD ? 1 : 0);

                switch (missing)
                {
                    case 0:
                        return _baseInteriorTexture ?? _emptyTexture;
                    case 1:
                        if (!nwD) return _innerTopLeftTexture ?? _baseInteriorTexture ?? _emptyTexture;
                        if (!neD) return _innerTopRightTexture ?? _baseInteriorTexture ?? _emptyTexture;
                        if (!swD) return _innerBottomLeftTexture ?? _baseInteriorTexture ?? _emptyTexture;
                        return _innerBottomRightTexture ?? _baseInteriorTexture ?? _emptyTexture;
                    case 2:
                        if (!nwD && !neD) return _innerDoubleTopTexture ?? _baseInteriorTexture ?? _emptyTexture;
                        if (!swD && !seD) return _innerDoubleBottomTexture ?? _baseInteriorTexture ?? _emptyTexture;
                        if (!nwD && !swD) return _innerDoubleLeftTexture ?? _baseInteriorTexture ?? _emptyTexture;
                        if (!neD && !seD) return _innerDoubleRightTexture ?? _baseInteriorTexture ?? _emptyTexture;
                        if (!nwD && !seD) return _innerDoubleDiagonalTexture ?? _baseInteriorTexture ?? _emptyTexture;
                        return _innerDoubleDiagonalReverseTexture ?? _baseInteriorTexture ?? _emptyTexture;
                    case 3:
                        // Named by the sole surviving diagonal
                        if (nwD) return _innerTripleTopLeftTexture ?? _baseInteriorTexture ?? _emptyTexture;
                        if (neD) return _innerTripleTopRightTexture ?? _baseInteriorTexture ?? _emptyTexture;
                        if (swD) return _innerTripleBottomLeftTexture ?? _baseInteriorTexture ?? _emptyTexture;
                        return _innerTripleBottomRightTexture ?? _baseInteriorTexture ?? _emptyTexture;
                    default:
                        return _plusShapeTexture ?? _baseInteriorTexture ?? _emptyTexture;
                }
            }

            default:
                return _addEmptyTexture;
        }
    }

    // Stalker-Changes-End
}
