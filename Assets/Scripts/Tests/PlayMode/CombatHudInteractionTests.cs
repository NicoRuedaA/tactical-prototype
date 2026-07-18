using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.PlayMode.Tests
{
    public sealed class CombatHudInteractionTests
    {
        private CombatRunner _runner;
        private PlayerInputController _input;
        private CombatHudView _hud;
        private Keyboard _testKeyboard;
        private Mouse _testMouse;
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode _editorInputBehavior;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            _backgroundBehavior = InputSystem.settings.backgroundBehavior;
            _editorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior =
                InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            if (RunManager.Instance != null)
                Object.DestroyImmediate(RunManager.Instance.gameObject);

            AsyncOperation load = SceneManager.LoadSceneAsync("Combat", LoadSceneMode.Single);
            while (load != null && !load.isDone)
                yield return null;

            float deadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < deadline)
            {
                _runner = Object.FindFirstObjectByType<CombatRunner>();
                if (_runner != null && _runner.HasCombatStarted)
                    break;
                yield return null;
            }

            Assert.That(_runner, Is.Not.Null);
            Assert.That(_runner.HasCombatStarted, Is.True);
            _runner.TurnDelay = 60f;
            _runner.CancelInvoke();
            _input = _runner.PlayerInput;
            _hud = _input.CombatHud;
            while (!_runner.Engine.IsOver && _runner.Engine.Current.Team != Team.Player)
                _runner.Engine.Pass();
            _runner.CancelInvoke();
            Assert.That(_runner.Engine.IsOver, Is.False);
            _input.SendMessage(
                "OnTurnChanged",
                _runner.Engine.Current,
                SendMessageOptions.RequireReceiver);
            _testKeyboard = InputSystem.AddDevice<Keyboard>("Combat HUD Test Keyboard");
            _testMouse = InputSystem.AddDevice<Mouse>("Combat HUD Test Mouse");
            ReleaseSyntheticInput();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            if (_runner != null)
                _runner.CancelInvoke();
            if (_testKeyboard != null && _testKeyboard.added)
                InputSystem.RemoveDevice(_testKeyboard);
            if (_testMouse != null && _testMouse.added)
                InputSystem.RemoveDevice(_testMouse);
            InputSystem.settings.backgroundBehavior = _backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInputBehavior;
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator PassShortcut_WithFocusedButtonAndNoFocus_ConsumesOneTurnEach()
        {
            EventSystem eventSystem = EventSystem.current;
            Assert.That(eventSystem, Is.Not.Null);

            eventSystem.SetSelectedGameObject(_hud.PassButton.gameObject);
            int focusedTurnBefore = _runner.Engine.TurnCount;

            _testKeyboard.MakeCurrent();
            InputSystem.QueueStateEvent(_testKeyboard, new KeyboardState(Key.Enter));
            InputSystem.Update();
            Assert.That(_testKeyboard.enterKey.isPressed, Is.True);
            _input.SendMessage("TryPassFromKeyboard", true, SendMessageOptions.RequireReceiver);
            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(focusedTurnBefore),
                "Enter must yield while a focused selectable owns uGUI Submit.");
            ExecuteEvents.Execute(
                _hud.PassButton.gameObject,
                new BaseEventData(eventSystem),
                ExecuteEvents.submitHandler);
            InputSystem.QueueStateEvent(_testKeyboard, new KeyboardState());
            InputSystem.Update();
            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(focusedTurnBefore + 1));
            yield return null;

            _runner.CancelInvoke();
            while (_runner.Engine.Current.Team != Team.Player)
                _runner.Engine.Pass();
            _runner.CancelInvoke();
            eventSystem.SetSelectedGameObject(null);
            int unfocusedTurnBefore = _runner.Engine.TurnCount;

            _testKeyboard.MakeCurrent();
            InputSystem.QueueStateEvent(_testKeyboard, new KeyboardState(Key.Enter));
            InputSystem.Update();
            Assert.That(_testKeyboard.enterKey.isPressed, Is.True);
            _input.SendMessage("TryPassFromKeyboard", true, SendMessageOptions.RequireReceiver);
            InputSystem.QueueStateEvent(_testKeyboard, new KeyboardState());
            InputSystem.Update();
            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(unfocusedTurnBefore + 1));
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator SpaceShortcut_WithFocusedButton_PassesExactlyOnce()
        {
            EventSystem eventSystem = EventSystem.current;
            Assert.That(eventSystem, Is.Not.Null);
            eventSystem.SetSelectedGameObject(_hud.PassButton.gameObject);
            int turnBefore = _runner.Engine.TurnCount;

            _testKeyboard.MakeCurrent();
            InputSystem.QueueStateEvent(_testKeyboard, new KeyboardState(Key.Space));
            InputSystem.Update();
            Assert.That(Keyboard.current, Is.SameAs(_testKeyboard));
            Assert.That(_testKeyboard.spaceKey.isPressed, Is.True);
            _input.SendMessage("TryPassFromKeyboard", false, SendMessageOptions.RequireReceiver);
            InputSystem.QueueStateEvent(_testKeyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;

            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(turnBefore + 1),
                "Focused UI must not suppress or duplicate the dedicated Space pass shortcut.");
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator SameFrameWorldMoveAndSpace_ExecutesAtMostOneAction()
        {
            Piece actor = _runner.Engine.Current;
            Axial destination = FindWorldClickableCoord(_runner.Engine.Board.Tiles
                .Select(tile => tile.Coords)
                .Where(coord => _runner.Engine.EvaluateAction(
                    CombatActionRequest.Move(actor, coord)).IsAllowed));
            int turnBefore = _runner.Engine.TurnCount;

            Physics.SyncTransforms();
            QueueSameFrameWorldClickAndSpace(destination);
            try
            {
                _input.SendMessage("Update", SendMessageOptions.RequireReceiver);

                Assert.That(actor.Coords, Is.EqualTo(destination),
                    "The real world-click route must execute before the later pass route.");
                Assert.That(_runner.Engine.TurnCount, Is.EqualTo(turnBefore + 1),
                    "A world click and Space observed by the same Update may consume only one turn.");
            }
            finally
            {
                ReleaseSyntheticInput();
            }
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator SameFrameUiSubmitAndSpace_ExecutesAtMostOneAction()
        {
            EventSystem eventSystem = EventSystem.current;
            Assert.That(eventSystem, Is.Not.Null);
            eventSystem.SetSelectedGameObject(_hud.PassButton.gameObject);
            int turnBefore = _runner.Engine.TurnCount;

            _testKeyboard.MakeCurrent();
            InputSystem.QueueStateEvent(_testKeyboard, new KeyboardState(Key.Space));
            InputSystem.Update();
            ExecuteEvents.Execute(
                _hud.PassButton.gameObject,
                new BaseEventData(eventSystem),
                ExecuteEvents.submitHandler);
            _input.SendMessage("Update", SendMessageOptions.RequireReceiver);
            ReleaseSyntheticInput();

            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(turnBefore + 1),
                "uGUI Submit and the Update shortcut in one frame may consume only one turn.");
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator SameFrameAbilityClickAndSpace_ExecutesAtMostOneAction()
        {
            Piece actor = _runner.Engine.Current;
            var abilities = actor.Abilities
                .Where(ability => ability.AbilityType == AbilityType.Active)
                .ToList();
            var presenter = new CombatHudPresenter();
            int abilityIndex = Enumerable.Range(0, abilities.Count)
                .First(index => presenter.CanUseAbility(
                    _runner.Engine, actor, abilities[index]));
            Axial target = FindWorldClickableCoord(
                presenter.GetLegalAbilityTargetCoords(
                    _runner.Engine, actor, abilities[abilityIndex]));
            bool abilityUsed = false;
            _runner.Engine.AbilityUsed += (_, _, _) => abilityUsed = true;
            int turnBefore = _runner.Engine.TurnCount;

            ExecuteEvents.Execute(
                _hud.AbilityButtons[abilityIndex].gameObject,
                new BaseEventData(EventSystem.current),
                ExecuteEvents.submitHandler);
            Physics.SyncTransforms();
            QueueSameFrameWorldClickAndSpace(target);
            try
            {
                _input.SendMessage("Update", SendMessageOptions.RequireReceiver);

                Assert.That(abilityUsed, Is.True,
                    "The uGUI-selected ability must execute through the real world-click route.");
                Assert.That(_runner.Engine.TurnCount, Is.EqualTo(turnBefore + 1),
                    "Ability execution and Space in one frame may consume only one turn.");
            }
            finally
            {
                ReleaseSyntheticInput();
            }
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator ActiveFeedback_IgnoresKeyboardAndConsumesSkipBeforeNextWorldClick()
        {
            Piece actor = _runner.Engine.Current;
            Axial origin = actor.Coords;
            Axial destination = FindWorldClickableCoord(_runner.Engine.Board.Tiles
                .Select(tile => tile.Coords)
                .Where(coord => _runner.Engine.EvaluateAction(
                    CombatActionRequest.Move(actor, coord)).IsAllowed));
            PieceView actorView = _runner.CombatView.GetPieceView(actor);
            actorView.MoveDuration = 60f;
            actorView.SetCompleteAnimationsImmediately(false);
            actorView.OnMove(actorView.transform.position);
            int turnBefore = _runner.Engine.TurnCount;

            Assert.That(_runner.CombatView.HasActiveFeedback, Is.True);

            _testKeyboard.MakeCurrent();
            InputSystem.QueueStateEvent(_testKeyboard, new KeyboardState(Key.Space));
            InputSystem.Update();
            _input.SendMessage("Update", SendMessageOptions.RequireReceiver);

            Assert.That(_runner.CombatView.HasActiveFeedback, Is.True,
                "Keyboard input must not fast-forward presentation feedback.");
            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(turnBefore));
            Assert.That(actor.Coords, Is.EqualTo(origin));

            InputSystem.QueueStateEvent(_testKeyboard, new KeyboardState());
            InputSystem.Update();
            QueueWorldPointerState(destination, true);
            _input.SendMessage("Update", SendMessageOptions.RequireReceiver);

            Assert.That(_runner.CombatView.HasActiveFeedback, Is.False);
            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(turnBefore),
                "The skip click must not spend the current action opportunity.");
            Assert.That(actor.Coords, Is.EqualTo(origin));

            QueueWorldPointerState(destination, false);
            _input.SendMessage("Update", SendMessageOptions.RequireReceiver);
            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(turnBefore));
            yield return null;

            QueueWorldPointerState(destination, true);
            _input.SendMessage("Update", SendMessageOptions.RequireReceiver);

            Assert.That(actor.Coords, Is.EqualTo(destination));
            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(turnBefore + 1),
                "A later click must execute normally after the skip gesture was consumed.");
            ReleaseSyntheticInput();
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator BusyPassButtonClick_SkipsFeedbackWithoutPassing()
        {
            Piece actor = _runner.Engine.Current;
            PieceView actorView = _runner.CombatView.GetPieceView(actor);
            actorView.HitDuration = 60f;
            actorView.SetCompleteAnimationsImmediately(false);
            actorView.OnHeal(1);
            int turnBefore = _runner.Engine.TurnCount;
            Vector2 buttonCenter = RectTransformUtility.WorldToScreenPoint(
                null, _hud.PassButton.transform.position);

            QueuePointerState(buttonCenter, true);
            _input.SendMessage("Update", SendMessageOptions.RequireReceiver);
            Assert.That(_runner.CombatView.HasActiveFeedback, Is.False);

            QueuePointerState(buttonCenter, false);
            ExecutePointerClick(_hud.PassButton);
            _input.SendMessage("Update", SendMessageOptions.RequireReceiver);

            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(turnBefore),
                "The Pass callback must not receive the click that skipped feedback.");
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator BusyAbilityButtonClick_SkipsFeedbackWithoutSelectingAbility()
        {
            Piece actor = _runner.Engine.Current;
            var abilities = actor.Abilities
                .Where(ability => ability.AbilityType == AbilityType.Active)
                .ToList();
            var presenter = new CombatHudPresenter();
            int abilityIndex = Enumerable.Range(0, abilities.Count)
                .First(index => presenter.CanUseAbility(
                    _runner.Engine, actor, abilities[index]));
            PieceView actorView = _runner.CombatView.GetPieceView(actor);
            actorView.HitDuration = 60f;
            actorView.SetCompleteAnimationsImmediately(false);
            actorView.OnBuffChanged(1);
            int turnBefore = _runner.Engine.TurnCount;
            Vector2 buttonCenter = RectTransformUtility.WorldToScreenPoint(
                null, _hud.AbilityButtons[abilityIndex].transform.position);

            QueuePointerState(buttonCenter, true);
            _input.SendMessage("Update", SendMessageOptions.RequireReceiver);
            Assert.That(_runner.CombatView.HasActiveFeedback, Is.False);

            QueuePointerState(buttonCenter, false);
            _input.SendMessage("Update", SendMessageOptions.RequireReceiver);
            ExecutePointerClick(_hud.AbilityButtons[abilityIndex]);

            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(turnBefore));
            Assert.That(_runner.Engine.Board.Tiles.Any(tile =>
                _runner.CombatView.GetTileView(tile.Coords).CurrentHighlight ==
                TileHighlight.AbilityRange), Is.False,
                "The ability callback must not receive the click that skipped feedback.");
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator HeldMouseAndKeyboardSubmit_DoesNotSkipActiveFeedback()
        {
            Piece actor = _runner.Engine.Current;
            PieceView actorView = _runner.CombatView.GetPieceView(actor);
            actorView.HitDuration = 60f;
            actorView.SetCompleteAnimationsImmediately(false);
            actorView.OnHeal(1);
            int turnBefore = _runner.Engine.TurnCount;
            EventSystem eventSystem = EventSystem.current;
            eventSystem.SetSelectedGameObject(_hud.PassButton.gameObject);

            QueuePointerState(new Vector2(-500f, -500f), true);
            Assert.That(_testMouse.leftButton.isPressed, Is.True);
            ExecuteEvents.Execute(
                _hud.PassButton.gameObject,
                new BaseEventData(eventSystem),
                ExecuteEvents.submitHandler);

            Assert.That(_runner.CombatView.HasActiveFeedback, Is.True,
                "Submit must remain keyboard-originated even while a mouse button is held.");
            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(turnBefore));
            QueuePointerState(new Vector2(-500f, -500f), false);
            _runner.CombatView.CompleteActiveFeedbackImmediately();
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator TerminalDeathFeedback_CanBeSkippedThroughRealInputUpdate()
        {
            foreach (Piece enemy in _runner.Engine.AliveOf(Team.Enemy).ToArray())
                enemy.TakeDamage(enemy.Hp);
            _runner.Engine.ResolvePendingDeaths();

            Assert.That(_runner.Engine.IsOver, Is.True);
            Assert.That(_runner.CombatView.HasActiveFeedback, Is.True);

            QueuePointerState(new Vector2(-500f, -500f), true);
            _input.SendMessage("Update", SendMessageOptions.RequireReceiver);

            Assert.That(_runner.CombatView.HasActiveFeedback, Is.False,
                "Terminal feedback must be skippable before the IsOver early return.");
            QueuePointerState(new Vector2(-500f, -500f), false);
            _input.SendMessage("Update", SendMessageOptions.RequireReceiver);
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator WorldClick_IsBlockedWhenPointerIsOverHud()
        {
            Vector2 buttonCenter = RectTransformUtility.WorldToScreenPoint(
                null, _hud.PassButton.transform.position);
            InputSystem.QueueDeltaStateEvent(_testMouse.position, buttonCenter);
            InputSystem.Update();

            Assert.That(_input.IsPointerOverUi(), Is.True);
            Assert.That(_input.TryHandleWorldClick(), Is.False);
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator TerminalResult_UsesExistingHudWithoutCreatingBannerCanvas()
        {
            int canvasCountBefore = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            _hud.ShowFeedback("Temporary feedback", CombatFeedbackTone.Invalid);
            Assert.That(_hud.FeedbackToast.activeSelf, Is.True);

            foreach (Piece enemy in _runner.Engine.AliveOf(Team.Enemy).ToArray())
                enemy.TakeDamage(enemy.Hp);
            _runner.Engine.ResolvePendingDeaths();

            Canvas[] canvases = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            CombatHudView[] huds = Object.FindObjectsByType<CombatHudView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            Assert.That(_runner.Engine.Winner, Is.EqualTo(Team.Player));
            Assert.That(canvases, Has.Length.EqualTo(canvasCountBefore));
            Assert.That(canvases.Count(canvas => canvas.name == "Combat HUD"), Is.EqualTo(1));
            Assert.That(GameObject.Find("BannerCanvas"), Is.Null);
            Assert.That(huds, Has.Length.EqualTo(1));
            Assert.That(huds[0].ActiveUnitText.text, Does.Contain("Combat won"));
            Assert.That(huds[0].PassButton.interactable, Is.False);
            Assert.That(huds[0].AbilityButtons.All(button => !button.interactable), Is.True);
            Assert.That(huds[0].FeedbackToast.activeSelf, Is.False);
            Assert.That(huds[0].FeedbackText.text, Is.Empty);
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator FeedbackToast_IsNonBlockingAndRestartsItsAutoHideTimer()
        {
            _hud.FeedbackDuration = 0.1f;
            Assert.That(_hud.FeedbackBackground.raycastTarget, Is.False);
            Assert.That(_hud.FeedbackText.raycastTarget, Is.False);

            _hud.ShowFeedback("First", CombatFeedbackTone.Invalid);
            yield return new WaitForSecondsRealtime(0.06f);
            _hud.ShowFeedback("Second", CombatFeedbackTone.Cancelled);
            yield return new WaitForSecondsRealtime(0.06f);

            Assert.That(_hud.FeedbackToast.activeSelf, Is.True,
                "A second message must restart, not share, the previous hide timer.");
            Assert.That(_hud.FeedbackText.text, Is.EqualTo("Second"));

            yield return new WaitForSecondsRealtime(0.06f);
            Assert.That(_hud.FeedbackToast.activeSelf, Is.False);
            Assert.That(_hud.FeedbackText.text, Is.Empty);
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator PreviewHighlights_ShowSelectedMoveAndOccupiedAttackIndicators()
        {
            Piece actor = _runner.Engine.Current;
            PieceView actorView = _runner.CombatView.GetPieceView(actor);
            Axial reachable = _runner.Engine.Board.Tiles
                .Select(tile => tile.Coords)
                .First(coord => _runner.Engine.EvaluateAction(
                    CombatActionRequest.Move(actor, coord)).IsAllowed);

            Assert.That(actorView.CurrentHighlight, Is.EqualTo(PieceHighlight.Selected));
            Assert.That(_runner.CombatView.GetTileView(actor.Coords).CurrentHighlight,
                Is.EqualTo(TileHighlight.Selected));
            Assert.That(_runner.CombatView.GetTileView(reachable).CurrentHighlight,
                Is.EqualTo(TileHighlight.Reachable));

            actor.AddBonusAttackRange(100);
            _runner.CombatView.ClearHighlights();
            _input.SendMessage("ShowMoveAndAttackHighlights", SendMessageOptions.RequireReceiver);
            Piece target = _runner.Engine.AliveOf(Team.Enemy).First();

            Assert.That(_runner.Engine.EvaluateAction(
                CombatActionRequest.Attack(actor, target)).IsAllowed, Is.True);
            Assert.That(_runner.CombatView.GetTileView(target.Coords).CurrentHighlight,
                Is.EqualTo(TileHighlight.Attackable));
            Assert.That(_runner.CombatView.GetPieceView(target).CurrentHighlight,
                Is.EqualTo(PieceHighlight.Attackable));
            Assert.That(_runner.CombatView.GetPieceView(target).transform
                .Find("Target Indicator"), Is.Not.Null);
            Renderer indicator = _runner.CombatView.GetPieceView(target).transform
                .Find("Target Indicator").GetComponent<Renderer>();
            Material expectedIndicatorMaterial = _runner.CombatView.PieceAttackHighlight != null
                ? _runner.CombatView.PieceAttackHighlight
                : _runner.CombatView.TileAttackable;
            Assert.That(indicator.sharedMaterial, Is.SameAs(expectedIndicatorMaterial));
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator FriendlyTargetRejection_ShowsTypedToastAndPreservesTurnAndResources()
        {
            Piece actor = _runner.Engine.Current;
            Piece ally = _runner.Engine.AliveOf(Team.Player)
                .First(piece => piece != actor);
            int turnBefore = _runner.Engine.TurnCount;
            int manaBefore = actor.Mana;

            bool executed = ClickPiece(ally);

            Assert.That(executed, Is.False);
            Assert.That(_hud.FeedbackToast.activeSelf, Is.True);
            Assert.That(_hud.FeedbackText.text, Is.EqualTo("Cannot attack an ally."));
            Assert.That(_hud.LastFeedbackTone, Is.EqualTo(CombatFeedbackTone.Invalid));
            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(turnBefore));
            Assert.That(actor.Mana, Is.EqualTo(manaBefore));
            Assert.That(_runner.CombatView.GetPieceView(ally).CurrentHighlight,
                Is.EqualTo(PieceHighlight.Invalid));
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator LegalAttack_ExecutesFromOccupiedTileSurface()
        {
            Piece actor = _runner.Engine.Current;
            Piece target = _runner.Engine.AliveOf(Team.Enemy).First();
            actor.AddBonusAttackRange(100);
            _input.SendMessage("ShowMoveAndAttackHighlights", SendMessageOptions.RequireReceiver);
            Assert.That(_runner.Engine.EvaluateAction(
                CombatActionRequest.Attack(actor, target)).IsAllowed, Is.True);

            Collider pieceCollider = _runner.CombatView.GetPieceView(target)
                .GetComponent<Collider>();
            int hpBefore = target.Hp;
            int turnBefore = _runner.Engine.TurnCount;
            pieceCollider.enabled = false;
            Physics.SyncTransforms();

            bool executed = ClickTile(target.Coords);

            pieceCollider.enabled = true;
            Assert.That(executed, Is.True);
            Assert.That(target.Hp, Is.LessThan(hpBefore));
            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(turnBefore + 1));
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator LegalAttack_ExecutesFromPieceSurface()
        {
            Piece actor = _runner.Engine.Current;
            Piece target = _runner.Engine.AliveOf(Team.Enemy).First();
            actor.AddBonusAttackRange(100);
            int hpBefore = target.Hp;
            int turnBefore = _runner.Engine.TurnCount;

            bool executed = ClickPiece(target);

            Assert.That(executed, Is.True);
            Assert.That(target.Hp, Is.LessThan(hpBefore));
            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(turnBefore + 1));
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator AbilityCancelAndInsufficientMana_ShowDistinctFeedbackWithoutActing()
        {
            Piece actor = _runner.Engine.Current;
            var activeAbilities = actor.Abilities
                .Where(ability => ability.AbilityType == AbilityType.Active)
                .ToList();
            int abilityIndex = Enumerable.Range(0, activeAbilities.Count)
                .First(index => new CombatHudPresenter().CanUseAbility(
                    _runner.Engine, actor, activeAbilities[index]));
            int turnBefore = _runner.Engine.TurnCount;
            int manaBefore = actor.Mana;

            _input.SendMessage("SelectAbilityAtIndex", abilityIndex, SendMessageOptions.RequireReceiver);
            Assert.That(_runner.Engine.Board.Tiles.Any(tile =>
                _runner.CombatView.GetTileView(tile.Coords).CurrentHighlight ==
                TileHighlight.AbilityRange), Is.True);

            _input.SendMessage("CancelAbility", SendMessageOptions.RequireReceiver);

            Assert.That(_hud.FeedbackText.text, Is.EqualTo("Ability cancelled."));
            Assert.That(_hud.LastFeedbackTone, Is.EqualTo(CombatFeedbackTone.Cancelled));
            Assert.That(_runner.CombatView.GetPieceView(actor).CurrentHighlight,
                Is.EqualTo(PieceHighlight.Cancelled));
            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(turnBefore));
            Assert.That(actor.Mana, Is.EqualTo(manaBefore));

            actor.SpendMana(actor.Mana);
            _input.SendMessage("RefreshHud", SendMessageOptions.RequireReceiver);

            Assert.That(_hud.AbilityButtons[abilityIndex].interactable, Is.True);
            ExecuteEvents.Execute(
                _hud.AbilityButtons[abilityIndex].gameObject,
                new BaseEventData(EventSystem.current),
                ExecuteEvents.submitHandler);

            Assert.That(_hud.FeedbackText.text, Is.EqualTo("Not enough mana."));
            Assert.That(_hud.LastFeedbackTone, Is.EqualTo(CombatFeedbackTone.Invalid));
            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(turnBefore));
            Assert.That(actor.Mana, Is.Zero);
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator PendingResolutionAndWrongTurn_RejectionsDoNotAdvanceTurn()
        {
            Piece actor = _runner.Engine.Current;
            Piece pendingEnemy = _runner.Engine.AliveOf(Team.Enemy)
                .First(piece => !piece.IsQueen);
            Axial destination = _runner.Engine.Board.Tiles
                .Select(tile => tile.Coords)
                .First(coord => !_runner.Engine.Board.IsOccupied(coord));
            int turnBefore = _runner.Engine.TurnCount;
            pendingEnemy.TakeDamage(pendingEnemy.Hp);

            bool executed = ClickTile(destination);

            Assert.That(executed, Is.False);
            Assert.That(_hud.FeedbackText.text, Is.EqualTo("Resolving combat..."));
            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(turnBefore));
            Assert.That(_runner.CombatView.GetTileView(destination).CurrentHighlight,
                Is.EqualTo(TileHighlight.Invalid));

            _runner.Engine.ResolvePendingDeaths();
            _runner.CombatView.CompleteActiveFeedbackImmediately();
            _runner.CancelInvoke();
            while (!_runner.Engine.IsOver && _runner.Engine.Current.Team == Team.Player)
                _runner.Engine.Pass();
            _runner.CancelInvoke();
            if (_runner.Engine.IsOver)
                Assert.Ignore("The fallback combat ended while arranging an enemy turn.");
            int enemyTurn = _runner.Engine.TurnCount;

            _input.SendMessage("PassTurn", SendMessageOptions.RequireReceiver);

            Assert.That(_hud.FeedbackText.text, Is.EqualTo("Not your turn."));
            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(enemyTurn));
            Assert.That(actor.Mana, Is.GreaterThanOrEqualTo(0));
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator RayMissAndUnavailableAction_ShowConsistentFeedback()
        {
            int turnBefore = _runner.Engine.TurnCount;
            _testMouse.MakeCurrent();
            InputSystem.QueueDeltaStateEvent(
                _testMouse.position,
                new Vector2(-500f, -500f));
            InputSystem.Update();

            bool executed = _input.TryHandleWorldClick();

            Assert.That(executed, Is.False);
            Assert.That(_hud.FeedbackText.text, Is.EqualTo("Nothing to target."));
            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(turnBefore));

            _input.SendMessage("SelectAbilityAtIndex", 99, SendMessageOptions.RequireReceiver);
            Assert.That(_hud.FeedbackText.text, Is.EqualTo("Action unavailable."));
            Assert.That(_runner.Engine.TurnCount, Is.EqualTo(turnBefore));
            yield return null;
        }

        private bool ClickPiece(Piece piece)
        {
            PieceView view = _runner.CombatView.GetPieceView(piece);
            return ClickWorld(view.GetComponent<Collider>().bounds.center);
        }

        private bool ClickTile(Axial coord)
        {
            TileView view = _runner.CombatView.GetTileView(coord);
            return ClickWorld(view.GetComponent<Collider>().bounds.center);
        }

        private bool ClickWorld(Vector3 worldPosition)
        {
            _testMouse.MakeCurrent();
            Vector3 screen = _input.TargetCamera.WorldToScreenPoint(worldPosition);
            InputSystem.QueueDeltaStateEvent(
                _testMouse.position,
                new Vector2(screen.x, screen.y));
            InputSystem.Update();
            Physics.SyncTransforms();
            return _input.TryHandleWorldClick();
        }

        private void QueueSameFrameWorldClickAndSpace(Axial coord)
        {
            TileView view = _runner.CombatView.GetTileView(coord);
            Vector3 screen = _input.TargetCamera.WorldToScreenPoint(
                view.GetComponent<Collider>().bounds.center);
            _testMouse.MakeCurrent();
            _testKeyboard.MakeCurrent();
            MouseState mouseState = new MouseState
            {
                position = new Vector2(screen.x, screen.y),
            }.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(_testMouse, mouseState);
            InputSystem.QueueStateEvent(_testKeyboard, new KeyboardState(Key.Space));
            InputSystem.Update();
            _testMouse.MakeCurrent();
            _testKeyboard.MakeCurrent();
        }

        private void QueueWorldPointerState(Axial coord, bool pressed)
        {
            TileView view = _runner.CombatView.GetTileView(coord);
            Vector3 screen = _input.TargetCamera.WorldToScreenPoint(
                view.GetComponent<Collider>().bounds.center);
            QueuePointerState(new Vector2(screen.x, screen.y), pressed);
        }

        private void QueuePointerState(Vector2 position, bool pressed)
        {
            _testMouse.MakeCurrent();
            MouseState state = new MouseState
            {
                position = position,
            };
            if (pressed)
                state = state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(_testMouse, state);
            InputSystem.Update();
            _testMouse.MakeCurrent();
        }

        private static void ExecutePointerClick(Button button)
        {
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
            };
            ExecuteEvents.Execute(
                button.gameObject,
                pointer,
                ExecuteEvents.pointerClickHandler);
        }

        private void ReleaseSyntheticInput()
        {
            _testMouse.MakeCurrent();
            _testKeyboard.MakeCurrent();
            InputSystem.QueueStateEvent(_testMouse, new MouseState());
            InputSystem.QueueStateEvent(_testKeyboard, new KeyboardState());
            InputSystem.Update();
            _testMouse.MakeCurrent();
            _testKeyboard.MakeCurrent();
        }

        private Axial FindWorldClickableCoord(IEnumerable<Axial> candidates)
        {
            EventSystem eventSystem = EventSystem.current;
            foreach (Axial coord in candidates)
            {
                TileView view = _runner.CombatView.GetTileView(coord);
                Vector3 screen = _input.TargetCamera.WorldToScreenPoint(
                    view.GetComponent<Collider>().bounds.center);
                if (screen.z <= 0f
                    || screen.x < 0f || screen.x > Screen.width
                    || screen.y < 0f || screen.y > Screen.height)
                    continue;

                var pointer = new PointerEventData(eventSystem)
                {
                    position = new Vector2(screen.x, screen.y),
                };
                var hits = new List<RaycastResult>();
                eventSystem.RaycastAll(pointer, hits);
                if (hits.Count == 0)
                    return coord;
            }

            Assert.Fail("No legal world target was visible outside the serialized HUD.");
            return default;
        }

    }
}
