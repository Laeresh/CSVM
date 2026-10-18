using System;
using CSVM.Flight.Ai;
using Godot;
using Xunit;

namespace CSVM.Tests;

public class ZvzBotObjectiveTests
{
    private const float Attack = 2000f;

    // The bot sits at the origin flying north (-Z), so its tail points south (+Z).
    private static readonly Vector3 Nose = Vector3.Forward;

    private static readonly object Gasbag = "gasbag1";
    private static readonly object Cannon = "lbroad1";
    private static readonly object FarCannon = "rbroad3";
    private static readonly object Turret = "gun1";
    private static readonly object Engine = "leng11";

    [Fact]
    public void GunsTakeTheNearestCannonThenTurretsThenEngines()
    {
        var none = Array.Empty<EnemyPilot>();
        Assert.Same(Cannon, Pick(false, none, Hull()));
        Assert.Same(Turret, Pick(false, none, Hull(cannons: false)));
        Assert.Same(Engine, Pick(false, none, Hull(cannons: false, turrets: false)));

        // ABLE-TO-FAIL CONTROL: with nothing standing there is no hull target, and the gunner ranks.
        var dead = Array.ConvertAll(Hull(), p => p with { Live = false });
        Assert.Null(Pick(false, none, dead));
    }

    [Fact]
    public void ACannonUnderAttackIsKeptWhileItStands()
    {
        var none = Array.Empty<EnemyPilot>();
        Assert.Same(FarCannon, ZvzBotObjective.Pick(Vector3.Zero, Nose, Attack, false, none, Hull(), FarCannon, out _));

        // ABLE-TO-FAIL CONTROL: a held target the rule no longer offers gives way to the nearest one.
        Assert.Same(Cannon, ZvzBotObjective.Pick(Vector3.Zero, Nose, Attack, false, none, Hull(), Engine, out _));
    }

    [Fact]
    public void AReadyTorpedoTakesAGasBagAndALockedOutOneGunsTheHull()
    {
        var none = Array.Empty<EnemyPilot>();
        Assert.Same(Gasbag, Pick(true, none, Hull()));

        // ABLE-TO-FAIL CONTROL: guns never aim at a bag, which only a torpedo damages. A torpedo bot
        // between launches takes the gun order like any other bot.
        Assert.Same(Cannon, Pick(false, none, Hull()));

        // The step the torpedo is ready again, the bag beats the cannon the bot was gunning.
        Assert.Same(Gasbag, ZvzBotObjective.Pick(Vector3.Zero, Nose, Attack, true, none, Hull(), Cannon, out _));
    }

    [Fact]
    public void AnEnemyOnItsHullOutranksAnEnemyOnItsTailWhichOutranksTheHull()
    {
        object raider = "raider", chaser = "chaser", passer = "passer";
        var onHull = new EnemyPilot(raider, new Vector3(0, 0, -5000), Vector3.Zero, true);
        var onTail = new EnemyPilot(chaser, new Vector3(0, 0, 300), new Vector3(0, 0, -100), false);
        var ahead = new EnemyPilot(passer, new Vector3(0, 0, -100), new Vector3(0, 0, 100), false);

        Assert.Same(raider, Pick(true, new[] { ahead, onTail, onHull }, Hull()));
        Assert.Same(chaser, Pick(true, new[] { ahead, onTail }, Hull()));

        // ABLE-TO-FAIL CONTROL: an enemy ahead flying at the bot is no pursuer, and the hull stands.
        Assert.Same(Gasbag, Pick(true, new[] { ahead }, Hull()));

        // A pursuer past the attack radius is not on the tail either.
        var far = onTail with { Position = new Vector3(0, 0, Attack + 100f) };
        Assert.Same(Gasbag, Pick(true, new[] { far }, Hull()));
    }

    private static HullPart[] Hull(bool cannons = true, bool turrets = true) => new[]
    {
        new HullPart(Gasbag, HullPartKind.Gasbag, new Vector3(0, 0, -300), true),
        new HullPart(Engine, HullPartKind.Engine, new Vector3(0, 0, -200), true),
        new HullPart(Turret, HullPartKind.Turret, new Vector3(0, 0, -250), turrets),
        new HullPart(FarCannon, HullPartKind.Cannon, new Vector3(0, 0, -900), cannons),
        new HullPart(Cannon, HullPartKind.Cannon, new Vector3(0, 0, -400), cannons),
    };

    private static object? Pick(bool torpedoReady, EnemyPilot[] enemies, HullPart[] hull) =>
        ZvzBotObjective.Pick(Vector3.Zero, Nose, Attack, torpedoReady, enemies, hull, null, out _);
}
