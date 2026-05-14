using Shooter.Input;
using Shooter.Physics;

namespace Shooter.Game;

/// <summary>Owns the live gameplay/runtime state for one loaded map session.</summary>
public sealed class GameSession
{
    private readonly CollisionWorld _collision;
    private readonly GameCombatController _combat;
    private float _fpsAccum;
    private int _fpsFrames;
    private float _fpsValue;
    private bool _menuOpen;
    private int _menuSelection;

    private const int MenuItemCount = 8;

    public GameWorld World { get; }
    public Player Player { get; }
    public WeaponSystem Weapons { get; }
    public PickupSystem Pickups { get; }
    public BulletHoleManager Holes { get; }
    public TracerSystem Tracers { get; }
    public RocketSystem Rockets { get; }
    public MuzzleFlash MuzzleFlash { get; }
    public ScorchManager Scorches { get; }
    public ParticleSystem Particles { get; }
    public LightingEnvironment Lighting { get; }
    public bool ShowDebug { get; private set; }
    public bool MenuOpen => _menuOpen;
    public int MenuSelection => _menuSelection;
    public bool QuitRequested { get; private set; }

    internal GameSession(
        GameWorld world,
        CollisionWorld collision,
        Player player,
        WeaponSystem weapons,
        PickupSystem pickups,
        BulletHoleManager holes,
        TracerSystem tracers,
        RocketSystem rockets,
        MuzzleFlash muzzleFlash,
        ScorchManager scorches,
        ParticleSystem particles,
        LightingEnvironment lighting,
        GameCombatController combat)
    {
        World = world;
        _collision = collision;
        Player = player;
        Weapons = weapons;
        Pickups = pickups;
        Holes = holes;
        Tracers = tracers;
        Rockets = rockets;
        MuzzleFlash = muzzleFlash;
        Scorches = scorches;
        Particles = particles;
        Lighting = lighting;
        _combat = combat;
    }

    public void ToggleDebug() => ShowDebug = !ShowDebug;

    public void Update(float dt, InputState input)
    {
        if (input.WasPressed(InputKey.Esc))
            _menuOpen = !_menuOpen;

        if (_menuOpen)
        {
            HandleMenuInput(input);
            UpdateFps(dt);
            input.EndFrame();
            return;
        }

        Player.Update(dt, input, _collision);
        Weapons.Update(dt);
        Weapons.HandleSelectInput(input);
        Pickups.Update(dt, Player, Weapons);
        Tracers.Update(dt);
        Rockets.Update(dt, _collision);
        MuzzleFlash.Update(dt);
        Particles.Update(dt);

        _combat.UpdateTransientEffects(dt);
        _combat.HandleFire(input);
        UpdateFps(dt);
        input.EndFrame();
    }

    public RenderSystem.RenderFrameData CreateFrameData() => new(
        Player,
        World,
        Weapons,
        Pickups,
        Holes,
        Tracers,
        Rockets,
        MuzzleFlash,
        Scorches,
        Particles,
        Lighting,
        ShowDebug,
        _fpsValue,
        _menuOpen,
        _menuSelection);

    private void HandleMenuInput(InputState input)
    {
        if (input.WasPressed(InputKey.Up)) _menuSelection = (_menuSelection - 1 + MenuItemCount) % MenuItemCount;
        if (input.WasPressed(InputKey.Down)) _menuSelection = (_menuSelection + 1) % MenuItemCount;

        bool increase = input.WasPressed(InputKey.Right) || input.WasPressed(InputKey.Enter);
        bool decrease = input.WasPressed(InputKey.Left);

        switch (_menuSelection)
        {
            case 0:
                if (increase || decrease) Lighting.ParallaxEnabled = !Lighting.ParallaxEnabled;
                break;
            case 1:
                if (increase || decrease) Lighting.SsaoEnabled = !Lighting.SsaoEnabled;
                break;
            case 2:
                if (increase || decrease) Lighting.BloomEnabled = !Lighting.BloomEnabled;
                break;
            case 3:
                if (increase || decrease) Lighting.ShadowsEnabled = !Lighting.ShadowsEnabled;
                break;
            case 4:
                if (increase || decrease) Lighting.AutoExposureEnabled = !Lighting.AutoExposureEnabled;
                break;
            case 5:
                if (increase || decrease) Lighting.FxaaEnabled = !Lighting.FxaaEnabled;
                break;
            case 6:
                float step = 0.005f;
                if (increase) Lighting.PomScale = Math.Clamp(Lighting.PomScale + step, 0f, 0.12f);
                if (decrease) Lighting.PomScale = Math.Clamp(Lighting.PomScale - step, 0f, 0.12f);
                break;
            case 7:
                if (increase || input.WasPressed(InputKey.Enter))
                    QuitRequested = true;
                break;
        }
    }

    private void UpdateFps(float dt)
    {
        _fpsAccum += dt;
        _fpsFrames++;
        if (_fpsAccum >= 0.5f)
        {
            _fpsValue = _fpsFrames / _fpsAccum;
            _fpsAccum = 0f;
            _fpsFrames = 0;
        }
    }
}
