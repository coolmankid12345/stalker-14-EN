using Content.Server._Stalker_EN.CollisionDamage;
using Content.Shared._Stalker_EN.CollisionDamage;
using Content.Shared.Damage;
using Content.Shared.Mind.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Stalker_EN.CollisionDamage;

[TestFixture]
[TestOf(typeof(STCollisionDamageSystem))]
[TestOf(typeof(STCollisionDamageComponent))]
public sealed class STCollisionDamageTests
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: damageType
  id: STTestBlunt
  name: damage-type-blunt

- type: damageContainer
  id: STTestDamageContainer
  supportedTypes:
    - STTestBlunt

- type: entity
  id: STTestCollisionDamager
  components:
  - type: Physics
    bodyType: Dynamic
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeCircle
          radius: 0.35
        hard: true
        layer:
        - MobMask
        mask:
        - MobMask
  - type: STCollisionDamage
    damage:
      types:
        STTestBlunt: 10
    onlyMindedEntities: false
    deleteOnHit: false
    maxTargets: null

- type: entity
  id: STTestCollisionDamagerSingleHit
  parent: STTestCollisionDamager
  components:
  - type: STCollisionDamage
    damage:
      types:
        STTestBlunt: 10
    onlyMindedEntities: false
    deleteOnHit: true
    maxTargets: 1

- type: entity
  id: STTestCollisionDamagerMindOnly
  parent: STTestCollisionDamager
  components:
  - type: STCollisionDamage
    damage:
      types:
        STTestBlunt: 10
    onlyMindedEntities: true
    deleteOnHit: false
    maxTargets: null

- type: entity
  id: STTestCollisionDamagerMultiTarget
  parent: STTestCollisionDamager
  components:
  - type: STCollisionDamage
    damage:
      types:
        STTestBlunt: 10
    onlyMindedEntities: false
    deleteOnHit: true
    maxTargets: 3

- type: entity
  id: STTestDamageTarget
  components:
  - type: Physics
    bodyType: Dynamic
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeCircle
          radius: 0.35
        hard: true
        layer:
        - MobMask
        mask:
        - MobMask
  - type: Damageable
    damageContainer: STTestDamageContainer

- type: entity
  id: STTestDamageTargetWithMind
  parent: STTestDamageTarget
  components:
  - type: MindContainer
";

    /// <summary>
    /// Verifies that the component initializes correctly with YAML-defined values.
    /// </summary>
    [Test]
    public async Task TestComponentInitialization()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var damager = entMan.SpawnEntity("STTestCollisionDamager", testMap.MapCoords);
            var comp = entMan.GetComponent<STCollisionDamageComponent>(damager);

            Assert.Multiple(() =>
            {
                Assert.That(comp.Damage.DamageDict.ContainsKey("STTestBlunt"), Is.True,
                    "Component should have damage type from YAML");
                Assert.That(comp.OnlyMindedEntities, Is.False,
                    "OnlyMindedEntities should be false as defined in YAML");
                Assert.That(comp.DeleteOnHit, Is.False,
                    "DeleteOnHit should be false as defined in YAML");
                Assert.That(comp.MaxTargets, Is.Null,
                    "MaxTargets should be null (unlimited) as defined in YAML");
                Assert.That(comp.TargetsHit, Is.EqualTo(0),
                    "TargetsHit should start at 0");
                Assert.That(comp.PendingDeletion, Is.False,
                    "PendingDeletion should start as false");
            });

            entMan.DeleteEntity(damager);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verifies that the single-hit variant initializes correctly.
    /// </summary>
    [Test]
    public async Task TestSingleHitComponentInitialization()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var damager = entMan.SpawnEntity("STTestCollisionDamagerSingleHit", testMap.MapCoords);
            var comp = entMan.GetComponent<STCollisionDamageComponent>(damager);

            Assert.Multiple(() =>
            {
                Assert.That(comp.DeleteOnHit, Is.True,
                    "DeleteOnHit should be true for single-hit variant");
                Assert.That(comp.MaxTargets, Is.EqualTo(1),
                    "MaxTargets should be 1 for single-hit variant");
            });

            entMan.DeleteEntity(damager);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verifies that the mind-only variant initializes correctly.
    /// </summary>
    [Test]
    public async Task TestMindOnlyComponentInitialization()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var damager = entMan.SpawnEntity("STTestCollisionDamagerMindOnly", testMap.MapCoords);
            var comp = entMan.GetComponent<STCollisionDamageComponent>(damager);

            Assert.Multiple(() =>
            {
                Assert.That(comp.OnlyMindedEntities, Is.True,
                    "OnlyMindedEntities should be true for mind-only variant");
            });

            entMan.DeleteEntity(damager);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verifies that the multi-target variant initializes correctly.
    /// </summary>
    [Test]
    public async Task TestMultiTargetComponentInitialization()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var damager = entMan.SpawnEntity("STTestCollisionDamagerMultiTarget", testMap.MapCoords);
            var comp = entMan.GetComponent<STCollisionDamageComponent>(damager);

            Assert.Multiple(() =>
            {
                Assert.That(comp.MaxTargets, Is.EqualTo(3),
                    "MaxTargets should be 3 for multi-target variant");
                Assert.That(comp.DeleteOnHit, Is.True,
                    "DeleteOnHit should be true for multi-target variant");
            });

            entMan.DeleteEntity(damager);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verifies that the damage target initializes with the correct damageable component.
    /// </summary>
    [Test]
    public async Task TestDamageTargetInitialization()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var target = entMan.SpawnEntity("STTestDamageTarget", testMap.MapCoords);
            var damageable = entMan.GetComponent<DamageableComponent>(target);

            Assert.That(damageable.TotalDamage.Float(), Is.EqualTo(0),
                "Target should start with 0 damage");

            entMan.DeleteEntity(target);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verifies that the target with mind component initializes correctly.
    /// </summary>
    [Test]
    public async Task TestMindTargetHasMindContainer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var target = entMan.SpawnEntity("STTestDamageTargetWithMind", testMap.MapCoords);

            Assert.That(entMan.HasComponent<MindContainerComponent>(target), Is.True,
                "Target with mind should have MindContainerComponent");
            Assert.That(entMan.HasComponent<DamageableComponent>(target), Is.True,
                "Target with mind should still have DamageableComponent");

            entMan.DeleteEntity(target);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verifies that the system is registered and can be resolved.
    /// </summary>
    [Test]
    public async Task TestSystemRegistration()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var system = entMan.System<STCollisionDamageSystem>();
            Assert.That(system, Is.Not.Null,
                "STCollisionDamageSystem should be registered");
        });

        await pair.CleanReturnAsync();
    }
}
