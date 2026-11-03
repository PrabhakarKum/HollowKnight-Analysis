using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hollowknight.Silksong
{
    // The grey-box start screen: four save slots, New / Continue / Delete (see StartScreenModel for the rules and
    // docs/study/13f-start-screen.md for the controls). Drawn with uGUI built in code. Input is read straight from
    // Keyboard.current / Gamepad.current (no event system, no input actions asset), so it never touches the hero
    // actions asset.
    //
    // Starting a game hides the screen and runs GameSession.StartGame with the shared world factory
    // (RoomWorldFactory). While a game runs, Escape quits back here through GameSession.Quit (a developer shortcut,
    // there is no pause menu), and the rows are re-read.
    public class StartScreen : MonoBehaviour
    {
        [SerializeField] HeroConfig config;
        [SerializeField] InputActionAsset actionsAsset;
        [SerializeField] AnimationClip[] heroClips;
        [SerializeField] Sprite heroSprite;
        [SerializeField] bool artFacesLeft = true;

        // Tests (and anything else with its own world) can replace how the world is built.
        public Func<PlayerData, RoomWorldBootstrap> WorldFactoryOverride;

        public StartScreenModel Model { get; private set; }
        public bool IsBusy { get; private set; }
        public bool IsGameRunning { get { return RoomWorldBootstrap.Instance != null; } }
        public bool IsScreenVisible { get { return canvasGo != null && canvasGo.activeSelf; } }
        public GameStartResult LastResult { get; private set; }

        public HeroConfig Config { get { return config; } }
        public InputActionAsset ActionsAsset { get { return actionsAsset; } }
        public Sprite HeroSprite { get { return heroSprite; } }
        public AnimationClip[] HeroClips { get { return heroClips; } }

        GameObject canvasGo, cameraGo;
        Text listText, messageText, helpText;
        float stickCooldownUntil;

        public void Configure(HeroConfig heroConfig, InputActionAsset inputActions, Sprite sprite, AnimationClip[] clips)
        {
            config = heroConfig;
            actionsAsset = inputActions;
            heroSprite = sprite;
            heroClips = clips;
        }

        void Awake()
        {
            Model = new StartScreenModel();
            BuildUi();
            Stubs.NotImplemented("Start screen: art, sound and the options/settings menu (plain text list only)");
            ShowScreen();
        }

        // ---------- building ----------

        void BuildUi()
        {
            cameraGo = new GameObject("StartScreenCamera");
            cameraGo.transform.SetParent(transform, false);
            var cam = cameraGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
            cam.orthographic = true;

            canvasGo = new GameObject("StartScreenCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            var font = ResolveFont();
            MakeText("Title", new Vector2(0.1f, 0.78f), new Vector2(0.9f, 0.95f), 56, TextAnchor.MiddleCenter, font).text = "SILKSONG (grey-box)";
            listText = MakeText("Slots", new Vector2(0.1f, 0.38f), new Vector2(0.9f, 0.76f), 30, TextAnchor.UpperLeft, font);
            messageText = MakeText("Message", new Vector2(0.1f, 0.25f), new Vector2(0.9f, 0.37f), 28, TextAnchor.MiddleLeft, font);
            messageText.color = new Color(1f, 0.85f, 0.4f);
            helpText = MakeText("Help", new Vector2(0.1f, 0.04f), new Vector2(0.9f, 0.22f), 22, TextAnchor.LowerLeft, font);
            helpText.color = new Color(0.7f, 0.7f, 0.75f);
            helpText.text = "Up/Down: choose slot    Enter / Space / A: continue or new game\n"
                          + "N / X: new game    Delete / Y: delete    Esc / B: cancel    (in game) Esc: back to this screen";
        }

        Text MakeText(string name, Vector2 anchorMin, Vector2 anchorMax, int size, TextAnchor anchor, Font font)
        {
            var go = new GameObject(name);
            go.transform.SetParent(canvasGo.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static Font ResolveFont()
        {
            foreach (var name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try { var f = Resources.GetBuiltinResource<Font>(name); if (f != null) return f; }
                catch (Exception) { /* not valid in this Unity version; try the next */ }
            }
            try { return Font.CreateDynamicFontFromOSFont("Arial", 16); }
            catch (Exception) { return null; }
        }

        // ---------- showing ----------

        void ShowScreen()
        {
            canvasGo.SetActive(true);
            cameraGo.SetActive(true);
            Model.Refresh();
            Redraw();
        }

        void HideScreen()
        {
            canvasGo.SetActive(false);
            cameraGo.SetActive(false);
        }

        // The text the list shows, for tests and for the Text component.
        public string ListText()
        {
            var sb = new StringBuilder();
            foreach (var row in Model.Rows)
            {
                sb.Append(row.Slot == Model.Selected ? "> " : "   ");
                sb.Append(row.Title).Append("   ").Append(row.Detail).Append('\n');
            }
            return sb.ToString();
        }

        void Redraw()
        {
            if (listText == null) return;
            listText.text = ListText();
            messageText.text = Model.Message;
        }

        // ---------- commands (keys call these; tests call them too) ----------

        public void MoveUp() { if (CanAct()) { Model.MoveUp(); Redraw(); } }
        public void MoveDown() { if (CanAct()) { Model.MoveDown(); Redraw(); } }
        public void Submit() { if (CanAct()) Handle(Model.Submit()); }
        public void RequestNew() { if (CanAct()) Handle(Model.RequestNew()); }
        public void RequestDelete() { if (CanAct()) { Model.RequestDelete(); Redraw(); } }
        public void Cancel() { if (CanAct()) { Model.Cancel(); Redraw(); } }

        bool CanAct() { return !IsBusy && !IsGameRunning && IsScreenVisible; }

        void Handle(StartScreenAction action)
        {
            Redraw();
            if (action == StartScreenAction.None) return;
            // Claim the screen now, so a second key press in the same frame cannot start a second game.
            IsBusy = true;
            StartCoroutine(StartSelected(action == StartScreenAction.Continue));
        }

        // Starts the selected slot. LastResult.Done is true when it ends.
        IEnumerator StartSelected(bool continueSave)
        {
            IsBusy = true;
            int slot = Model.SelectedRow.Slot;
            LastResult = new GameStartResult();
            HideScreen();
            Stubs.NotImplemented("Pause menu (Escape quits to the start screen)");

            yield return GameSession.StartGame(slot, continueSave, MakeWorld, LastResult);

            if (!LastResult.Success)
            {
                ShowScreen();
                Model.ShowMessage(string.IsNullOrEmpty(LastResult.Message) ? "The game could not be started." : LastResult.Message);
                Redraw();
            }
            IsBusy = false;
        }

        RoomWorldBootstrap MakeWorld(PlayerData data)
        {
            if (WorldFactoryOverride != null) return WorldFactoryOverride(data);
            if (config == null || actionsAsset == null)
            {
                Debug.LogError("StartScreen: hero config or input actions are not assigned. Rebuild with Silksong > Build Start Screen.", this);
                return null;
            }
            return RoomWorldFactory.Create(config, actionsAsset, heroSprite, heroClips, artFacesLeft, data);
        }

        // Leaves the running game (saving it) and shows the screen again with fresh rows.
        public IEnumerator QuitToMenu()
        {
            if (IsBusy || !IsGameRunning) yield break;
            IsBusy = true;
            yield return GameSession.Quit();
            ShowScreen();
            IsBusy = false;
        }

        // ---------- input ----------

        void Update()
        {
            if (IsBusy) return;
            var kb = Keyboard.current;
            var pad = Gamepad.current;

            if (IsGameRunning)
            {
                bool quit = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
                if (quit) StartCoroutine(QuitToMenu());
                return;
            }
            if (!IsScreenVisible) return;

            bool up = false, down = false, submit = false, cancel = false, wantNew = false, wantDelete = false;
            if (kb != null)
            {
                up |= kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
                down |= kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
                submit |= kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
                cancel |= kb.escapeKey.wasPressedThisFrame;
                wantNew |= kb.nKey.wasPressedThisFrame;
                wantDelete |= kb.xKey.wasPressedThisFrame || kb.deleteKey.wasPressedThisFrame;
            }
            if (pad != null)
            {
                up |= pad.dpad.up.wasPressedThisFrame;
                down |= pad.dpad.down.wasPressedThisFrame;
                float y = pad.leftStick.ReadValue().y;
                if (Mathf.Abs(y) > 0.6f && Time.unscaledTime >= stickCooldownUntil)
                {
                    if (y > 0f) up = true; else down = true;
                    stickCooldownUntil = Time.unscaledTime + 0.25f;
                }
                else if (Mathf.Abs(y) < 0.3f) stickCooldownUntil = 0f;
                submit |= pad.buttonSouth.wasPressedThisFrame;
                cancel |= pad.buttonEast.wasPressedThisFrame;
                wantNew |= pad.buttonWest.wasPressedThisFrame;
                wantDelete |= pad.buttonNorth.wasPressedThisFrame;
            }

            if (up) MoveUp();
            else if (down) MoveDown();
            else if (cancel) Cancel();
            else if (wantNew) RequestNew();
            else if (wantDelete) RequestDelete();
            else if (submit) Submit();
        }
    }
}
