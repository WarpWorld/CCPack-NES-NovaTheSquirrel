using ConnectorLib;
using CrowdControl.Common;
using JetBrains.Annotations;
using ConnectorType = CrowdControl.Common.ConnectorType;

namespace CrowdControl.Games.Packs.NovaTheSquirrel;

/// <summary>Provides Crowd Control effects for <c>Nova the Squirrel</c> on NES.</summary>
[UsedImplicitly]
public class NovaTheSquirrel : NESEffectPack
{
    /// <summary>Initializes a new instance of the <see cref="NovaTheSquirrel"/> effect pack.</summary>
    /// <param name="player">The Crowd Control user running the game.</param>
    /// <param name="responseHandler">The handler that sends effect responses to Crowd Control.</param>
    /// <param name="statusUpdateHandler">The handler that publishes pack status updates.</param>
    public NovaTheSquirrel(UserRecord player, Func<CrowdControlBlock, bool> responseHandler, Action<object> statusUpdateHandler) : base(player, responseHandler, statusUpdateHandler) { }

    /// <summary>Address of the flag indicating that Nova is moving through a normal door.</summary>
    private const ushort ADDR_IS_NORMAL_DOOR = 0x0023;

    /// <summary>Address of Nova's current health in half-heart units.</summary>
    private const ushort ADDR_PLAYER_HEALTH = 0x004B;

    /// <summary>Address of the current internal level number.</summary>
    private const ushort ADDR_LEVEL = 0x00A7;

    /// <summary>Address of the pending level-reload flag.</summary>
    private const ushort ADDR_NEED_LEVEL_RELOAD = 0x00A9;

    /// <summary>Address used to queue an engine-native player ability change.</summary>
    private const ushort ADDR_NEED_ABILITY_CHANGE = 0x0390;

    /// <summary>Address of the pending level-rerender flag.</summary>
    private const ushort ADDR_NEED_LEVEL_RERENDER = 0x0392;

    /// <summary>Address of the pending or active dialog flag.</summary>
    private const ushort ADDR_NEED_DIALOG = 0x0394;

    /// <summary>Address of the inventory cursor swap state used to identify the pause screen.</summary>
    private const ushort ADDR_INVENTORY_SWAP = 0x0396;

    /// <summary>Address of the currently mapped switchable PRG ROM bank.</summary>
    private const ushort ADDR_PRG_BANK = 0x039E;

    /// <summary>Address of the block-placement mode flag.</summary>
    private const ushort ADDR_PLACE_BLOCK = 0x03DA;

    /// <summary>Address of the first pause-screen swap-list entry.</summary>
    private const ushort ADDR_SWAP_LIST_0 = 0x0700;

    /// <summary>Address of the final selectable pause-menu entry.</summary>
    private const ushort ADDR_LAST_MENU_OPTION = 0x0716;

    /// <summary>Address of Nova's active player ability.</summary>
    private const ushort ADDR_PLAYER_ABILITY = 0x7200;

    /// <summary>Address of the flag indicating that options were opened from the pause screen.</summary>
    private const ushort ADDR_OPTIONS_VIA_INVENTORY = 0x7267;

    /// <summary>Health value to restore when the active one-hit KO effect ends.</summary>
    private byte? _ohkoRestoreHealth;

    /// <summary>Ability value to restore when the active timed ability effect ends.</summary>
    private byte? _abilityRestore;

    /// <summary>Maps effect-code ability names to viewer-facing names and game ability identifiers.</summary>
    private static readonly Dictionary<string, (string Name, byte Id)> Abilities = new()
    {
        ["none"] = ("No Ability", 0),
        ["blaster"] = ("Blaster", 1),
        ["glider"] = ("Glider", 2),
        ["bomb"] = ("Bomb", 3),
        ["fire"] = ("Fire", 4),
        ["firework"] = ("Firework", 5),
        ["ice"] = ("Ice", 6),
        ["boomerang"] = ("Boomerang", 7),
        ["mirror"] = ("Mirror", 8),
        ["water"] = ("Water", 9),
        ["fan"] = ("Fan", 10),
        ["burger"] = ("Dinner Blaster", 11)
    };

    /// <inheritdoc/>
    public override ROMTable ROMTable => new[]
    {
        new ROMInfo("Nova the Squirrel", null, Patching.Ignore, ROMStatus.ValidPatched, s => Patching.MD5(s, "ecc08609f53c236e16731e75c857c934"))
    };

    /// <inheritdoc/>
    public override EffectList Effects { get; } = (List<Effect>)
    [
        new("Full Heal", "fullheal") { Description = "Restores Nova to four full hearts.", Price = 125, Category = "Health", Tags = ["heal", "health", "heart"], Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
        new("Restore One Heart", "heal") { Description = "Restores one of Nova's hearts.", Price = 75, Category = "Health", Tags = ["heal", "health", "heart"], Alignment = (Alignment)Morality.SlightlyHelpful + Orderliness.Controlled },
        new("Take One Heart", "hurt") { Description = "Removes one heart without directly knocking Nova out.", Price = 75, Category = "Health", Tags = ["damage", "hurt", "health", "heart"], Alignment = (Alignment)Morality.SlightlyHarmful + Orderliness.Controlled },
        new("Knock Out Nova", "kill") { Description = "Immediately knocks Nova out and returns her to the last checkpoint.", Price = 200, Category = "Health", Tags = ["kill", "death", "damage", "health"], Alignment = (Alignment)Morality.ExtremelyHarmful + Orderliness.Controlled },
        new("One-Hit KO", "ohko") { Description = "Temporarily limits Nova to half a heart; her prior health is restored afterward if she survives.", Price = 150, Category = "Health", Tags = ["ohko", "one hit", "damage", "health"], Duration = 15, Alignment = (Alignment)Morality.ExtremelyHarmful + Orderliness.Controlled },

        new("Set Ability: None", "ability_none") { Description = "Temporarily removes Nova's current ability.", Price = 75, Category = "Set Ability", Tags = ["ability", "power", "none", "remove"], Duration = 10, Alignment = (Alignment)Morality.SlightlyHarmful + Orderliness.Controlled },
        new("Set Ability: Blaster", "ability_blaster") { Description = "Temporarily gives Nova the standard projectile Blaster ability.", Price = 100, Category = "Set Ability", Tags = ["ability", "power", "blaster", "projectile"], Duration = 10, Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
        new("Set Ability: Glider", "ability_glider") { Description = "Temporarily gives Nova the aerial Glider ability.", Price = 100, Category = "Set Ability", Tags = ["ability", "power", "glider", "flight"], Duration = 10, Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
        new("Set Ability: Bomb", "ability_bomb") { Description = "Temporarily gives Nova the explosive Bomb ability.", Price = 100, Category = "Set Ability", Tags = ["ability", "power", "bomb", "explosive"], Duration = 10, Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
        new("Set Ability: Fire", "ability_fire") { Description = "Temporarily gives Nova the Fire ability for launching fireballs.", Price = 100, Category = "Set Ability", Tags = ["ability", "power", "fire", "fireball"], Duration = 10, Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
        new("Set Ability: Firework", "ability_firework") { Description = "Temporarily gives Nova the guided Firework projectile ability.", Price = 100, Category = "Set Ability", Tags = ["ability", "power", "fire", "firework", "projectile"], Duration = 10, Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
        new("Set Ability: Ice", "ability_ice") { Description = "Temporarily gives Nova the Ice ability for creating rideable ice blocks.", Price = 100, Category = "Set Ability", Tags = ["ability", "power", "ice", "block"], Duration = 10, Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
        new("Set Ability: Boomerang", "ability_boomerang") { Description = "Temporarily gives Nova the four-direction Boomerang ability.", Price = 100, Category = "Set Ability", Tags = ["ability", "power", "boomerang", "projectile"], Duration = 10, Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
        new("Set Ability: Mirror", "ability_mirror") { Description = "Temporarily gives Nova the Mirror ability for reflecting enemy projectiles.", Price = 100, Category = "Set Ability", Tags = ["ability", "power", "mirror", "reflect", "projectile"], Duration = 10, Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
        new("Set Ability: Water", "ability_water") { Description = "Temporarily gives Nova the Water Bottle ability.", Price = 100, Category = "Set Ability", Tags = ["ability", "power", "water", "bottle"], Duration = 10, Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
        new("Set Ability: Fan", "ability_fan") { Description = "Temporarily gives Nova the Fan ability for launching tornadoes.", Price = 100, Category = "Set Ability", Tags = ["ability", "power", "fan", "tornado"], Duration = 10, Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
        new("Set Ability: Dinner Blaster", "ability_burger") { Description = "Temporarily gives Nova the powerful explosive Dinner Blaster ability.", Price = 125, Category = "Set Ability", Tags = ["ability", "power", "blaster", "dinner", "burger", "explosive"], Duration = 10, Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled }
    ];

    /// <inheritdoc/>
    public override Game Game { get; } = new("Nova the Squirrel", "NovaTheSquirrel", "NES", ConnectorType.NESConnector);

    /// <inheritdoc/>
    public override List<string> MetadataCommon { get; } = ["health", "location"];

    /// <inheritdoc/>
    protected override async Task<DataResponse> RequestData(string key)
    {
        switch (key)
        {
            case "health":
                if (!Connector.Read8(ADDR_PLAYER_HEALTH, out byte health)) return DataResponse.DelayEstimated(key);
                return DataResponse.Success(key, health);
            case "location":
                if (!Connector.Read8(ADDR_LEVEL, out byte level)) return DataResponse.DelayEstimated(key);
                return DataResponse.Success(key, new { level });
            default:
                return await base.RequestData(key);
        }
    }

    /// <inheritdoc/>
    protected override GameState GetGameState()
    {
        if (!Connector.Read8(ADDR_PRG_BANK, out byte prgBank) ||
            !Connector.Read8(ADDR_PLAYER_HEALTH, out byte health))
            return GameState.Unknown;

        if (prgBank == 0x0E)
        {
            if (!Connector.Read8(ADDR_OPTIONS_VIA_INVENTORY, out byte optionsViaInventory) ||
                !Connector.Read8(ADDR_LAST_MENU_OPTION, out byte lastMenuOption) ||
                !Connector.Read8(ADDR_SWAP_LIST_0, out byte swapList0) ||
                !Connector.Read8(ADDR_INVENTORY_SWAP, out byte inventorySwap))
                return GameState.Unknown;

            bool isPauseScreen = optionsViaInventory != 0 ||
                                 (swapList0 == 0x50 &&
                                  lastMenuOption is 9 or 10 &&
                                  (inventorySwap == 0x80 || inventorySwap < 10));
            return isPauseScreen && health is > 0 and <= 8 ? GameState.Paused : GameState.Menu;
        }

        if (prgBank is 0x03 or 0x04 or 0x05 or 0x0D) return GameState.Menu;

        if (!Connector.Read8(ADDR_NEED_DIALOG, out byte needDialog) ||
            !Connector.Read8(ADDR_NEED_LEVEL_RELOAD, out byte needLevelReload) ||
            !Connector.Read8(ADDR_NEED_LEVEL_RERENDER, out byte needLevelRerender) ||
            !Connector.Read8(ADDR_IS_NORMAL_DOOR, out byte isNormalDoor) ||
            !Connector.Read8(ADDR_PLACE_BLOCK, out byte placeBlock))
            return GameState.Unknown;

        if (needDialog != 0 || prgBank == 0x07) return GameState.Cutscene;
        if (needLevelReload != 0 || needLevelRerender != 0 || isNormalDoor != 0) return GameState.Loading;
        if (placeBlock != 0) return GameState.InputLocked;
        if (health is 0 or > 8) return GameState.BadPlayerState;
        return prgBank is 0x08 or 0x09 ? GameState.InLevel : GameState.Loading;
    }

    /// <inheritdoc/>
    protected override void StartEffect(EffectRequest request)
    {
        string[] codeParams = FinalCode(request).Split('_');

        switch (codeParams[0])
        {
            case "fullheal":
                if (!Connector.Read8(ADDR_PLAYER_HEALTH, out byte fullHealHealth))
                {
                    Respond(request, EffectStatus.FailTemporary, StandardErrors.ConnectorReadFailure, $"{ADDR_PLAYER_HEALTH:X4}");
                    return;
                }
                if (fullHealHealth >= 8)
                {
                    Respond(request, EffectStatus.FailTemporary, StandardErrors.AlreadyMaximum, "health");
                    return;
                }
                TryEffect(request,
                    action: () => Connector.Write8(ADDR_PLAYER_HEALTH, 8),
                    followUp: () => Connector.SendMessage($"{request.DisplayViewer} fully healed Nova."),
                    mutex: "health");
                return;
            case "heal":
                if (!Connector.Read8(ADDR_PLAYER_HEALTH, out byte healHealth))
                {
                    Respond(request, EffectStatus.FailTemporary, StandardErrors.ConnectorReadFailure, $"{ADDR_PLAYER_HEALTH:X4}");
                    return;
                }
                if (healHealth >= 8)
                {
                    Respond(request, EffectStatus.FailTemporary, StandardErrors.AlreadyMaximum, "health");
                    return;
                }
                TryEffect(request,
                    action: () => Connector.RangeAdd8(ADDR_PLAYER_HEALTH, 1, 0, 8, false),
                    followUp: () => Connector.SendMessage($"{request.DisplayViewer} restored one of Nova's hearts."),
                    mutex: "health");
                return;
            case "hurt":
                if (!Connector.Read8(ADDR_PLAYER_HEALTH, out byte hurtHealth))
                {
                    Respond(request, EffectStatus.FailTemporary, StandardErrors.ConnectorReadFailure, $"{ADDR_PLAYER_HEALTH:X4}");
                    return;
                }
                if (hurtHealth <= 1)
                {
                    Respond(request, EffectStatus.FailTemporary, StandardErrors.AlreadyMinimum, "health");
                    return;
                }
                TryEffect(request,
                    action: () => Connector.RangeAdd8(ADDR_PLAYER_HEALTH, -1, 1, 8, false),
                    followUp: () => Connector.SendMessage($"{request.DisplayViewer} took one of Nova's hearts."),
                    mutex: "health");
                return;
            case "kill":
                if (!Connector.Read8(ADDR_PLAYER_HEALTH, out byte _))
                {
                    Respond(request, EffectStatus.FailTemporary, StandardErrors.ConnectorReadFailure, $"{ADDR_PLAYER_HEALTH:X4}");
                    return;
                }
                TryEffect(request,
                    action: () => Connector.Write8(ADDR_PLAYER_HEALTH, 0),
                    followUp: () => Connector.SendMessage($"{request.DisplayViewer} knocked out Nova."),
                    mutex: "health");
                return;
            case "ohko":
                {
                    if (!Connector.Read8(ADDR_PLAYER_HEALTH, out byte restoreHealth))
                    {
                        Respond(request, EffectStatus.FailTemporary, StandardErrors.ConnectorReadFailure, $"{ADDR_PLAYER_HEALTH:X4}");
                        return;
                    }

                    bool StartCondition()
                    {
                        if (GetGameState() != GameState.InLevel ||
                            !Connector.Read8(ADDR_PLAYER_HEALTH, out byte currentHealth))
                            return false;
                        restoreHealth = currentHealth;
                        return true;
                    }

                    EffectState s = RepeatAction(request,
                        startCondition: StartCondition,
                        startAction: () =>
                        {
                            if (!Connector.Write8(ADDR_PLAYER_HEALTH, 1)) return false;
                            _ohkoRestoreHealth = restoreHealth;
                            return true;
                        },
                        startRetry: TimeSpan.FromSeconds(1),
                        refreshCondition: () => GetGameState() == GameState.InLevel,
                        refreshAction: () => Connector.Write8(ADDR_PLAYER_HEALTH, 1),
                        mutex: "health"
                    );
                    s.WhenStarted.Then(() => Connector.SendMessage($"{request.DisplayViewer} put Nova into one-hit KO."));
                }
                return;
            case "ability":
                SetAbility(request, codeParams);
                return;
            default:
                Respond(request, EffectStatus.FailTemporary, StandardErrors.EffectUnknown, FinalCode(request));
                return;
        }
    }

    /// <summary>Validates and starts a timed player ability effect.</summary>
    /// <param name="request">The effect request being processed.</param>
    /// <param name="codeParams">The parsed effect-code components containing the requested ability.</param>
    private void SetAbility(EffectRequest request, string[] codeParams)
    {
        if (codeParams.Length != 2 || !Abilities.TryGetValue(codeParams[1], out var ability))
        {
            Respond(request, EffectStatus.FailTemporary, StandardErrors.EffectUnknown, FinalCode(request));
            return;
        }

        if (!Connector.Read8(ADDR_PLAYER_ABILITY, out byte currentAbility))
        {
            Respond(request, EffectStatus.FailTemporary, StandardErrors.ConnectorReadFailure, $"{ADDR_PLAYER_ABILITY:X4}");
            return;
        }

        StartTimed(request,
            startCondition: () => GetGameState() == GameState.InLevel,
            continueCondition: () => GetGameState() == GameState.InLevel,
            continueConditionInterval: TimeSpan.FromSeconds(1),
            action: () => StartAbility(ability.Id, currentAbility),
            mutex: "ability"
        ).WhenStarted.Then(() => Connector.SendMessage($"{request.DisplayViewer} set Nova's ability to {ability.Name}."));
    }

    /// <summary>Freezes the selected ability and queues the corresponding engine-native ability update.</summary>
    /// <param name="ability">The ability identifier to activate.</param>
    /// <param name="restoreAbility">The ability identifier to restore when the timed effect ends.</param>
    /// <returns><see langword="true"/> if the ability was frozen and queued successfully; otherwise, <see langword="false"/>.</returns>
    private bool StartAbility(byte ability, byte restoreAbility)
    {
        if (!Connector.Freeze8(ADDR_PLAYER_ABILITY, ability)) return false;
        if (Connector.Write8(ADDR_NEED_ABILITY_CHANGE, (byte)(0x80 | ability)))
        {
            _abilityRestore = restoreAbility;
            return true;
        }
        if (!Connector.Unfreeze(ADDR_PLAYER_ABILITY)) return false;
        return false;
    }

    /// <summary>Removes the active ability freeze and queues restoration of the previous ability.</summary>
    /// <returns><see langword="true"/> if no restoration was needed or the previous ability was restored; otherwise, <see langword="false"/>.</returns>
    private bool RestoreAbility()
    {
        if (!_abilityRestore.HasValue) return true;
        if (!Connector.Write8(ADDR_NEED_ABILITY_CHANGE, (byte)(0x80 | _abilityRestore.Value))) return false;
        if (!Connector.Unfreeze(ADDR_PLAYER_ABILITY)) return false;
        _abilityRestore = null;
        return true;
    }

    /// <summary>Restores the health saved when the one-hit KO effect began, provided Nova is alive.</summary>
    /// <returns><see langword="true"/> if no restoration was needed or health was restored; otherwise, <see langword="false"/>.</returns>
    private bool RestoreOhkoHealth()
    {
        if (!_ohkoRestoreHealth.HasValue) return true;
        if (!Connector.Read8(ADDR_PLAYER_HEALTH, out byte currentHealth)) return false;
        if (currentHealth != 0 && !Connector.Write8(ADDR_PLAYER_HEALTH, _ohkoRestoreHealth.Value)) return false;
        _ohkoRestoreHealth = null;
        return true;
    }

    /// <inheritdoc/>
    protected override bool StopEffect(EffectRequest request)
    {
        bool success = base.StopEffect(request);
        if (request.EffectID == "ohko") success &= RestoreOhkoHealth();
        if (request.EffectID.StartsWith("ability_", StringComparison.Ordinal)) success &= RestoreAbility();
        return success;
    }

    /// <inheritdoc/>
    public override bool StopAllEffects()
    {
        bool success = base.StopAllEffects();
        success &= RestoreOhkoHealth();
        success &= RestoreAbility();
        return success;
    }
}