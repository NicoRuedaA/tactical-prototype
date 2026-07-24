using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.PlayMode.Tests
{
    public sealed class RunLoopSmokeTests
    {
        private const int TestSeed = 812734;
        private const float SceneTimeoutSeconds = 8f;
        private readonly List<string> _unexpectedFailingLogs = new List<string>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = false;
            _unexpectedFailingLogs.Clear();
            Application.logMessageReceived += OnLogMessageReceived;
            if (RunManager.Instance != null)
                Object.DestroyImmediate(RunManager.Instance.gameObject);

            SceneManager.LoadScene("SampleScene");
            yield return WaitForScene("Map", SceneTimeoutSeconds);

            RunManager.Instance.StartNewRun(TestSeed);
            yield return WaitForScene("Map", SceneTimeoutSeconds);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var runner in Object.FindObjectsOfType<CombatRunner>())
                runner.CancelInvoke();

            if (RunManager.Instance != null)
                Object.DestroyImmediate(RunManager.Instance.gameObject);

            var loadedScenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                loadedScenes.Add(SceneManager.GetSceneAt(i));

            Scene cleanupScene = SceneManager.CreateScene($"PlayModeTestCleanup_{Time.frameCount}");
            SceneManager.SetActiveScene(cleanupScene);
            foreach (Scene scene in loadedScenes)
            {
                if (!scene.IsValid() || !scene.isLoaded || scene == cleanupScene)
                    continue;

                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                    yield return null;
            }

            Application.logMessageReceived -= OnLogMessageReceived;
            Assert.That(_unexpectedFailingLogs, Is.Empty, string.Join("\n", _unexpectedFailingLogs));
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator CombatScene_UsesSingleSerializedHudAndInputSystemEventSystem()
        {
            var manager = RequireRunManagerInMap();

            yield return EnterCombat(manager);
            CombatRunner runner = null;
            yield return WaitForCombatStarted(value => runner = value, SceneTimeoutSeconds);

            var huds = Object.FindObjectsByType<CombatHudView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var eventSystems = Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var inputModules = Object.FindObjectsByType<InputSystemUIInputModule>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var canvases = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            Assert.That(huds, Has.Length.EqualTo(1));
            Assert.That(huds[0].IsConfigured, Is.True);
            Assert.That(eventSystems, Has.Length.EqualTo(1));
            Assert.That(inputModules, Has.Length.EqualTo(1));
            Assert.That(canvases.Count(canvas => canvas.name == "Combat HUD"), Is.EqualTo(1));
            Assert.That(runner.PlayerInput.CombatHud, Is.SameAs(huds[0]));
            Assert.That(huds[0].ActionRuleText.text,
                Is.EqualTo("ONE ACTION: Move, Attack, Ability, or Pass."));

            int activeAbilityCount = runner.Engine.Current.Abilities
                .Count(ability => ability.AbilityType == AbilityType.Active);
            Assert.That(huds[0].AbilityButtons.Count, Is.EqualTo(activeAbilityCount));
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator VictoryRewardFlow_PreservesRunAndUsesSeededRewardWiring()
        {
            var manager = RequireRunManagerInMap();
            RunState originalRun = manager.CurrentRun;
            int originalSeed = manager.CurrentRunSeed;

            yield return EnterCombat(manager);
            CombatRunner runner = null;
            yield return WaitForCombatStarted(value => runner = value, SceneTimeoutSeconds);

            TriggerEngineVictory(runner.Engine, Team.Player);
            yield return WaitForScene("Reward", SceneTimeoutSeconds);

            Assert.That(RunManager.Instance, Is.SameAs(manager));
            Assert.That(manager.CurrentRun, Is.SameAs(originalRun));
            Assert.That(manager.CurrentRunSeed, Is.EqualTo(originalSeed));
            Assert.That(manager.CurrentCombatIndex, Is.EqualTo(1));
            Assert.That(manager.CurrentPhase, Is.EqualTo(RunManager.RunPhase.Reward));

            var rewardScreen = Object.FindObjectOfType<RewardScreen>();
            Assert.That(rewardScreen, Is.Not.Null, "RewardScreen.OnEnable must run on the real Reward scene.");

            int optionsSeed = manager.GetStreamSeed(RunRandomStream.RewardOptions);
            int recipientSeed = manager.GetStreamSeed(RunRandomStream.RewardRecipient);
            Assert.That(recipientSeed, Is.Not.EqualTo(optionsSeed));

            RewardOption[] expectedOptions = RewardScreen.GenerateRewardOptions(optionsSeed);
            RewardOption[] wiredOptions = GetPrivateField<RewardOption[]>(rewardScreen, "_currentOptions");
            Assert.That(wiredOptions, Is.Not.Null);
            Assert.That(
                wiredOptions.Select(option => option.Description),
                Is.EqualTo(expectedOptions.Select(option => option.Description)));
            Assert.That(rewardScreen.CardText0.text, Does.Contain(expectedOptions[0].Description));
            Assert.That(GetPrivateField<int>(rewardScreen, "_rewardRecipientSeed"), Is.EqualTo(recipientSeed));

            Piece expectedRecipient = RewardScreen.SelectRewardRecipient(originalRun, recipientSeed);
            Assert.That(expectedRecipient, Is.Not.Null);
            var before = originalRun.Pieces.ToDictionary(piece => piece, PieceProgress.Capture);

            SubmitButton(rewardScreen.CardButton0);
            yield return null;
            SubmitRewardRecipient(rewardScreen, expectedRecipient);
            yield return WaitForScene("Map", SceneTimeoutSeconds);

            Assert.That(RunManager.Instance, Is.SameAs(manager));
            Assert.That(manager.CurrentRun, Is.SameAs(originalRun));
            Assert.That(manager.CurrentRunSeed, Is.EqualTo(originalSeed));
            Assert.That(manager.CurrentCombatIndex, Is.EqualTo(1));
            Assert.That(manager.CurrentPhase, Is.EqualTo(RunManager.RunPhase.Map));

            AssertRewardApplied(expectedRecipient, expectedOptions[0], before[expectedRecipient]);
            foreach (Piece piece in originalRun.Pieces.Where(piece => piece != expectedRecipient))
                Assert.That(PieceProgress.Capture(piece), Is.EqualTo(before[piece]));

        }

        [UnityTest, Timeout(30000)]
        public IEnumerator CombatResult_RemainsVisibleUntilConfiguredTerminalDelay()
        {
            var manager = RequireRunManagerInMap();
            yield return EnterCombat(manager);
            CombatRunner runner = null;
            yield return WaitForCombatStarted(value => runner = value, SceneTimeoutSeconds);

            runner.CombatEndDelaySeconds = 0.4f;
            TriggerEngineVictory(runner.Engine, Team.Player);
            yield return new WaitForSecondsRealtime(0.1f);

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Combat"));
            Assert.That(runner.PlayerInput.CombatHud.ActiveUnitText.text, Is.EqualTo("VICTORY"));

            yield return WaitForScene("Reward", SceneTimeoutSeconds);
            Assert.That(RunManager.Instance, Is.SameAs(manager));
            Assert.That(manager.CurrentPhase, Is.EqualTo(RunManager.RunPhase.Reward));
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator DefeatFlow_GameOverPlayAgain_ReusesRunManagerAndStartsFreshRun()
        {
            var manager = RequireRunManagerInMap();
            RunState defeatedRun = manager.CurrentRun;

            yield return EnterCombat(manager);
            CombatRunner runner = null;
            yield return WaitForCombatStarted(value => runner = value, SceneTimeoutSeconds);

            TriggerEngineVictory(runner.Engine, Team.Enemy);
            yield return WaitForScene("GameOver", SceneTimeoutSeconds);

            Assert.That(RunManager.Instance, Is.SameAs(manager));
            Assert.That(manager.CurrentRun, Is.SameAs(defeatedRun));
            Assert.That(manager.CurrentPhase, Is.EqualTo(RunManager.RunPhase.Defeat));
            Assert.That(manager.CurrentCombatIndex, Is.Zero,
                "A defeat must not increment the completed-combat index before restart.");

            var defeatScreen = Object.FindObjectOfType<DefeatScreen>();
            Assert.That(defeatScreen, Is.Not.Null);
            Assert.That(defeatScreen.TitleText.text, Is.EqualTo("DEFEAT"));
            Assert.That(defeatScreen.MainMenuButton, Is.Not.Null);

            SubmitButton(defeatScreen.MainMenuButton);
            yield return WaitForScene("Map", SceneTimeoutSeconds);

            Assert.That(RunManager.Instance, Is.SameAs(manager));
            Assert.That(Object.FindObjectsOfType<RunManager>(), Has.Length.EqualTo(1));
            Assert.That(manager.CurrentRun, Is.Not.Null.And.Not.SameAs(defeatedRun));
            Assert.That(manager.CurrentPhase, Is.EqualTo(RunManager.RunPhase.Map));
            Assert.That(manager.CurrentCombatIndex, Is.Zero);
            Assert.That(manager.CurrentRunSeed, Is.Not.Zero);

        }

        [UnityTest, Timeout(60000)]
        public IEnumerator FullRun_ToBossVictory_UsesProductionNodeCombatRewardAndGameOverFlow()
        {
            var manager = RequireRunManagerInMap();
            RunState originalRun = manager.CurrentRun;
            int originalSeed = manager.CurrentRunSeed;
            int completedCombats = 0;
            var selectedNodeIds = new List<string>();

            for (int selection = 0; selection < 8 && !originalRun.Graph.IsComplete; selection++)
            {
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Map"));
                Assert.That(manager.CurrentRun, Is.SameAs(originalRun));
                Assert.That(manager.CurrentRunSeed, Is.EqualTo(originalSeed));
                Assert.That(manager.CurrentPhase, Is.EqualTo(RunManager.RunPhase.Map));

                string nodeId = originalRun.GetAvailableNodes()
                    .OrderBy(id => IsEncounter(originalRun.Graph.Nodes[id].Type) ? 0 : 1)
                    .ThenBy(id => id)
                    .FirstOrDefault();
                Assert.That(nodeId, Is.Not.Null, "A valid route to the boss must remain available.");

                MapNodeType nodeType = originalRun.Graph.Nodes[nodeId].Type;
                selectedNodeIds.Add(nodeId);
                manager.OnNodeSelected(nodeId);

                if (!IsEncounter(nodeType))
                {
                    yield return WaitForScene("Map", SceneTimeoutSeconds);
                    continue;
                }

                yield return WaitForScene("Combat", SceneTimeoutSeconds);
                CombatRunner runner = null;
                yield return WaitForCombatStarted(value => runner = value, SceneTimeoutSeconds);

                int previousCombatIndex = manager.CurrentCombatIndex;
                TriggerEngineVictory(runner.Engine, Team.Player);
                yield return WaitForScene("Reward", SceneTimeoutSeconds);
                completedCombats++;

                Assert.That(RunManager.Instance, Is.SameAs(manager));
                Assert.That(manager.CurrentRun, Is.SameAs(originalRun));
                Assert.That(manager.CurrentRunSeed, Is.EqualTo(originalSeed));
                Assert.That(manager.CurrentCombatIndex, Is.EqualTo(previousCombatIndex + 1));
                Assert.That(manager.CurrentPhase, Is.EqualTo(RunManager.RunPhase.Reward));

                var rewardScreen = Object.FindObjectOfType<RewardScreen>();
                Assert.That(rewardScreen, Is.Not.Null);
                SubmitButton(rewardScreen.CardButton0);
                yield return null;
                SubmitFirstRewardRecipient(rewardScreen);

                if (nodeType == MapNodeType.Boss)
                    yield return WaitForScene("GameOver", SceneTimeoutSeconds);
                else
                    yield return WaitForScene("Map", SceneTimeoutSeconds);
            }

            Assert.That(originalRun.Graph.IsComplete, Is.True);
            Assert.That(selectedNodeIds, Does.Contain(originalRun.Graph.BossNodeId));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("GameOver"));
            Assert.That(RunManager.Instance, Is.SameAs(manager));
            Assert.That(manager.CurrentRun, Is.Null);
            Assert.That(manager.CurrentRunSeed, Is.EqualTo(originalSeed));
            Assert.That(manager.CurrentCombatIndex, Is.EqualTo(completedCombats));
            Assert.That(manager.CurrentPhase, Is.EqualTo(RunManager.RunPhase.Victory));
            Assert.That(manager.LastRunWasVictory, Is.True);

            var victoryScreen = Object.FindObjectOfType<DefeatScreen>();
            Assert.That(victoryScreen, Is.Not.Null);
            Assert.That(victoryScreen.TitleText.text, Is.EqualTo("VICTORY"));
        }

        private static RunManager RequireRunManagerInMap()
        {
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Map"));
            Assert.That(RunManager.Instance, Is.Not.Null);
            Assert.That(RunManager.Instance.CurrentRun, Is.Not.Null);
            Assert.That(RunManager.Instance.CurrentRunSeed, Is.EqualTo(TestSeed));
            return RunManager.Instance;
        }

        private static bool IsEncounter(MapNodeType nodeType)
        {
            return nodeType == MapNodeType.Combat ||
                   nodeType == MapNodeType.Elite ||
                   nodeType == MapNodeType.Boss;
        }

        private static IEnumerator EnterCombat(RunManager manager)
        {
            for (int step = 0; step < 3; step++)
            {
                string combatNodeId = manager.CurrentRun.GetAvailableNodes()
                    .FirstOrDefault(id =>
                    {
                        MapNodeType type = manager.CurrentRun.Graph.Nodes[id].Type;
                        return type == MapNodeType.Combat || type == MapNodeType.Elite || type == MapNodeType.Boss;
                    });

                if (combatNodeId != null)
                {
                    manager.OnNodeSelected(combatNodeId);
                    yield return WaitForScene("Combat", SceneTimeoutSeconds);
                    yield break;
                }

                string traversalNode = manager.CurrentRun.GetAvailableNodes().FirstOrDefault();
                Assert.That(traversalNode, Is.Not.Null, "No route remains to reach a combat node.");
                manager.OnNodeSelected(traversalNode);
                yield return WaitForScene("Map", SceneTimeoutSeconds);
            }

            Assert.Fail("Could not reach a combat scene within three map selections.");
        }

        private static IEnumerator WaitForCombatStarted(
            System.Action<CombatRunner> onStarted,
            float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            CombatRunner runner = null;

            while (Time.realtimeSinceStartup < deadline)
            {
                runner = Object.FindObjectOfType<CombatRunner>();
                if (runner != null &&
                    runner.Engine != null &&
                    runner.HasCombatStarted &&
                    runner.Engine.Current != null &&
                    !runner.Engine.IsOver)
                {
                    onStarted(runner);
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                $"Timed out after {timeoutSeconds:0.0}s waiting for CombatRunner.StartCombat to call CombatEngine.Begin.");
        }

        private static void SubmitButton(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.gameObject.activeInHierarchy, Is.True,
                $"Button '{button.name}' must be active before UI submission.");
            Assert.That(button.interactable, Is.True,
                $"Button '{button.name}' must be interactable before UI submission.");

            EventSystem eventSystem = EventSystem.current;
            Assert.That(eventSystem, Is.Not.Null, "The active scene must provide an EventSystem.");
            Assert.That(eventSystem.gameObject.activeInHierarchy, Is.True);

            var inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            Assert.That(inputModule, Is.Not.Null,
                "The EventSystem must use InputSystemUIInputModule, not StandaloneInputModule.");
            Assert.That(inputModule.isActiveAndEnabled, Is.True);

            bool submitted = ExecuteEvents.Execute(
                button.gameObject,
                new BaseEventData(eventSystem),
                ExecuteEvents.submitHandler);
            Assert.That(submitted, Is.True, $"Button '{button.name}' did not handle the submit event.");
        }

        private static void SubmitRewardRecipient(RewardScreen rewardScreen, Piece expectedRecipient)
        {
            var recipients = RewardScreen.GetDeterministicAliveRecipients(
                GetPrivateField<RunState>(rewardScreen, "_runState"));
            int index = recipients.ToList().IndexOf(expectedRecipient);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Expected reward recipient must be alive.");
            var buttons = GetPrivateField<List<Button>>(rewardScreen, "_recipientButtons");
            Assert.That(buttons, Is.Not.Null);
            Assert.That(buttons.Count, Is.EqualTo(recipients.Count));
            SubmitButton(buttons[index]);
        }

        private static void SubmitFirstRewardRecipient(RewardScreen rewardScreen)
        {
            var buttons = GetPrivateField<List<Button>>(rewardScreen, "_recipientButtons");
            Assert.That(buttons, Is.Not.Null);
            Assert.That(buttons.Count, Is.GreaterThan(0), "Reward selection should expose alive recipients.");
            SubmitButton(buttons[0]);
        }

        private static void TriggerEngineVictory(CombatEngine engine, Team winner)
        {
            Team losingTeam = winner == Team.Player ? Team.Enemy : Team.Player;
            foreach (Piece piece in engine.Pieces.Where(piece => piece.Team == losingTeam).ToList())
                piece.TakeDamage(piece.Hp);

            int resolvedDeaths = engine.ResolvePendingDeaths();
            Assert.That(resolvedDeaths, Is.GreaterThan(0));
            Assert.That(engine.IsOver, Is.True);
            Assert.That(engine.Winner, Is.EqualTo(winner));
        }

        private static IEnumerator WaitForScene(string sceneName, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(
                SceneManager.GetActiveScene().name,
                Is.EqualTo(sceneName),
                $"Timed out after {timeoutSeconds:0.0}s waiting for scene '{sceneName}'.");
            yield return null;
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            return (T)target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Warning || type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                _unexpectedFailingLogs.Add($"[{type}] {condition}\n{stackTrace}");
        }

        private static void AssertRewardApplied(Piece piece, RewardOption option, PieceProgress before)
        {
            PieceProgress after = PieceProgress.Capture(piece);

            if (option.Effect == RewardEffectKind.NewAbility)
            {
                Assert.That(after.AbilityCount, Is.EqualTo(before.AbilityCount + 1));
                return;
            }

            if (option.Effect == RewardEffectKind.MaxHpBoost)
            {
                Assert.That(after.MaxHp, Is.EqualTo(before.MaxHp + option.Amount));
                Assert.That(after.Hp, Is.EqualTo(before.Hp + option.Amount));
                return;
            }

            switch (option.Stat)
            {
                case StatType.Damage:
                    Assert.That(after.Damage, Is.EqualTo(before.Damage + option.Amount));
                    break;
                case StatType.AttackRange:
                    Assert.That(after.AttackRange, Is.EqualTo(before.AttackRange + option.Amount));
                    break;
                case StatType.MoveRange:
                    Assert.That(after.MoveRange, Is.EqualTo(before.MoveRange + option.Amount));
                    break;
                default:
                    Assert.Fail($"Unhandled reward option '{option.Description}'.");
                    break;
            }
        }

        private readonly struct PieceProgress
        {
            public readonly int Hp;
            public readonly int MaxHp;
            public readonly int Damage;
            public readonly int AttackRange;
            public readonly int MoveRange;
            public readonly int AbilityCount;

            private PieceProgress(Piece piece)
            {
                Hp = piece.Hp;
                MaxHp = piece.EffectiveMaxHp;
                Damage = piece.EffectiveDamage;
                AttackRange = piece.EffectiveAttackRange;
                MoveRange = piece.EffectiveMoveRange;
                AbilityCount = piece.Abilities.Count;
            }

            public static PieceProgress Capture(Piece piece) => new PieceProgress(piece);

            public override bool Equals(object obj)
            {
                return obj is PieceProgress other &&
                       Hp == other.Hp &&
                       MaxHp == other.MaxHp &&
                       Damage == other.Damage &&
                       AttackRange == other.AttackRange &&
                       MoveRange == other.MoveRange &&
                       AbilityCount == other.AbilityCount;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Hp;
                    hash = (hash * 397) ^ MaxHp;
                    hash = (hash * 397) ^ Damage;
                    hash = (hash * 397) ^ AttackRange;
                    hash = (hash * 397) ^ MoveRange;
                    hash = (hash * 397) ^ AbilityCount;
                    return hash;
                }
            }
        }
    }
}
