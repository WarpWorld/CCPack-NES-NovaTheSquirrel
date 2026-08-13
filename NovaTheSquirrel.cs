using ConnectorLib;
using CrowdControl.Common;
using JetBrains.Annotations;
using ConnectorType = CrowdControl.Common.ConnectorType;

namespace CrowdControl.Games.Packs.NovaTheSquirrel;

[UsedImplicitly]
public class NovaTheSquirrel : NESEffectPack
{
    public NovaTheSquirrel(UserRecord player, Func<CrowdControlBlock, bool> responseHandler, Action<object> statusUpdateHandler) : base(player, responseHandler, statusUpdateHandler) { }

    private const ushort ADDR_PLAYER_HEALTH = 0x004B;
    private const ushort ADDR_LEVEL = 0x00A7;
    private const ushort ADDR_NEED_ABILITY_CHANGE = 0x0390;
    private const ushort ADDR_PLAYER_ABILITY = 0x7000;

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

    public override ROMTable ROMTable => new[]
    {
        new ROMInfo("Nova the Squirrel", null, Patching.Ignore, ROMStatus.ValidPatched, s => Patching.MD5(s, "ecc08609f53c236e16731e75c857c934"))
    };

    public override EffectList Effects { get; } = (List<Effect>)
            [
                new("Full Heal", "fullheal") { Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
                new("Restore One Heart", "heal") { Alignment = (Alignment)Morality.SlightlyHelpful + Orderliness.Controlled },
                new("Take One Heart", "hurt") { Alignment = (Alignment)Morality.SlightlyHarmful + Orderliness.Controlled },
                new("Kill Player", "kill") { Alignment = (Alignment)Morality.ExtremelyHarmful + Orderliness.Controlled },
                new("One-Hit KO", "ohko") { Duration = 15, Alignment = (Alignment)Morality.ExtremelyHarmful + Orderliness.Controlled },

                new("Set Ability: None", "ability_none") { Alignment = (Alignment)Morality.SlightlyHarmful + Orderliness.Controlled },
                new("Set Ability: Blaster", "ability_blaster") { Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
                new("Set Ability: Glider", "ability_glider") { Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
                new("Set Ability: Bomb", "ability_bomb") { Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
                new("Set Ability: Fire", "ability_fire") { Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
                new("Set Ability: Firework", "ability_firework") { Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
                new("Set Ability: Ice", "ability_ice") { Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
                new("Set Ability: Boomerang", "ability_boomerang") { Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
                new("Set Ability: Mirror", "ability_mirror") { Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
                new("Set Ability: Water", "ability_water") { Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
                new("Set Ability: Fan", "ability_fan") { Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled },
                new("Set Ability: Dinner Blaster", "ability_burger") { Alignment = (Alignment)Morality.Helpful + Orderliness.Controlled }
            ];

    public override Game Game { get; } = new("Nova the Squirrel", "NovaTheSquirrel", "NES", ConnectorType.NESConnector);

    public override List<string> MetadataCommon { get; } = ["health", "location"];

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

    protected override GameState GetGameState()
    {
        if (!Connector.Read8(ADDR_PLAYER_HEALTH, out byte health)) return GameState.Unknown;
        return health is > 0 and <= 8 ? GameState.InLevel : GameState.BadPlayerState;
    }

    protected override void StartEffect(EffectRequest request)
    {
        string[] codeParams = FinalCode(request).Split('_');

        switch (codeParams[0])
        {
            case "fullheal":
                TryEffect(request,
                    condition: () => Connector.Read8(ADDR_PLAYER_HEALTH, out byte health) && health < 8,
                    action: () => Connector.Write8(ADDR_PLAYER_HEALTH, 8),
                    followUp: () => Connector.SendMessage($"{request.DisplayViewer} fully healed Nova."),
                    mutex: "health");
                return;
            case "heal":
                TryEffect(request,
                    condition: () => Connector.Read8(ADDR_PLAYER_HEALTH, out byte health) && health < 8,
                    action: () => Connector.RangeAdd8(ADDR_PLAYER_HEALTH, 1, 0, 8, false),
                    followUp: () => Connector.SendMessage($"{request.DisplayViewer} restored one of Nova's hearts."),
                    mutex: "health");
                return;
            case "hurt":
                TryEffect(request,
                    condition: () => Connector.Read8(ADDR_PLAYER_HEALTH, out byte health) && health > 1,
                    action: () => Connector.RangeAdd8(ADDR_PLAYER_HEALTH, -1, 1, 8, false),
                    followUp: () => Connector.SendMessage($"{request.DisplayViewer} took one of Nova's hearts."),
                    mutex: "health");
                return;
            case "kill":
                TryEffect(request,
                    condition: () => Connector.Read8(ADDR_PLAYER_HEALTH, out byte health) && health > 0,
                    action: () => Connector.Write8(ADDR_PLAYER_HEALTH, 0),
                    followUp: () => Connector.SendMessage($"{request.DisplayViewer} knocked out Nova."),
                    mutex: "health");
                return;
            case "ohko":
                {
                    byte restoreHealth = 0;
                    bool cond()
                    {
                        if (!Connector.Read8(ADDR_PLAYER_HEALTH, out byte health)) return false;
                        restoreHealth = Math.Max(health, restoreHealth);
                        return health > 0;
                    }

                    EffectState s = RepeatAction(request,
                        startCondition: cond,
                        startAction: () => Connector.Write8(ADDR_PLAYER_HEALTH, 1),
                        startRetry: null,
                        refreshCondition: cond,
                        refreshAction: () => Connector.Write8(ADDR_PLAYER_HEALTH, 1),
                        mutex: "health"
                    );
                    s.WhenStarted.Then(() => Connector.SendMessage($"{request.DisplayViewer} put Nova into one-hit KO.."));
                    s.WhenCompleted.Then(() => Connector.Write8(ADDR_PLAYER_HEALTH, restoreHealth));
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

    private void SetAbility(EffectRequest request, string[] codeParams)
    {
        if (codeParams.Length != 2 || !Abilities.TryGetValue(codeParams[1], out var ability))
        {
            Respond(request, EffectStatus.FailTemporary, StandardErrors.EffectUnknown, FinalCode(request));
            return;
        }

        TryEffect(request,
            condition: () => Connector.Read8(ADDR_PLAYER_ABILITY, out byte currentAbility) && currentAbility != ability.Id,
            action: () => Connector.Write8(ADDR_NEED_ABILITY_CHANGE, (byte)(0x80 | ability.Id)),
            followUp: () => Connector.SendMessage($"{request.DisplayViewer} set Nova's ability to {ability.Name}."));
    }
}