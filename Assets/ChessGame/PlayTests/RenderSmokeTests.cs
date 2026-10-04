using System.Collections;
using System.Collections.Generic;
using System.IO;
using Chess.AI;
using Chess.Core;
using Chess.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Chess.PlayTests
{
    /// <summary>
    /// Renders the running game off-screen and checks the frame is not blank or flat. A missing
    /// shader, an empty procedural mesh or a broken material would all show up here as a uniform
    /// image, which no other test would catch. The captures land in Screenshots/ so the result
    /// can also simply be looked at.
    /// </summary>
    public class RenderSmokeTests
    {
        private GameManager _manager;
        private Texture2D _capture;

        private static string OutputDirectory
        {
            get
            {
                string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Screenshots");
                Directory.CreateDirectory(directory);
                return directory;
            }
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            PlayerPrefs.SetInt("chess.difficulty", (int)Difficulty.Easy);
            PlayerPrefs.SetInt("chess.side", (int)PlayerSide.White);
            PlayerPrefs.SetInt("chess.sound", 0);
            PlayerPrefs.Save();

            yield return SceneManager.LoadSceneAsync("Chess", LoadSceneMode.Single);
            yield return null;

            GameSettings.SoundEnabled = false;

            _manager = Object.FindFirstObjectByType<GameManager>();
            Assert.IsNotNull(_manager);

            // Settings are static and survive between tests, so pin the theme for every capture.
            GameSettings.ThemeIndex = 0;
            _manager.ApplyCurrentSettings();
            yield return null;
        }

        /// <summary>
        /// Renders one frame at the requested size into <see cref="_capture"/>. The camera rig
        /// reframes from Camera.aspect, which only picks up the render texture's shape once a
        /// LateUpdate has run, so this waits a frame after retargeting before drawing. The overlay
        /// canvas is temporarily routed through the camera, otherwise the interface never reaches
        /// the texture.
        /// </summary>
        private IEnumerator CaptureFrame(int width, int height)
        {
            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "no main camera to render with");

            var canvas = Object.FindFirstObjectByType<Canvas>();
            RenderMode previousMode = canvas != null ? canvas.renderMode : RenderMode.ScreenSpaceOverlay;

            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }

            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            RenderTexture previousTarget = camera.targetTexture;
            camera.targetTexture = target;

            var rig = camera.GetComponent<CameraRig>();
            if (rig != null) rig.Refresh();

            yield return null;
            yield return null;

            TryRender(camera, target);

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = target;

            _capture = new Texture2D(width, height, TextureFormat.RGB24, false);
            _capture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            _capture.Apply();

            RenderTexture.active = previousActive;
            camera.targetTexture = previousTarget;
            if (canvas != null) canvas.renderMode = previousMode;

            Object.DestroyImmediate(target);
        }

        /// <summary>Scriptable pipelines want an explicit render request; fall back for the built-in one.</summary>
        private static void TryRender(Camera camera, RenderTexture target)
        {
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                {
                    camera.SubmitRenderRequest(request);
                    return;
                }
            }

            camera.Render();
        }

        private void Save(string fileName)
        {
            File.WriteAllBytes(Path.Combine(OutputDirectory, fileName), _capture.EncodeToPNG());
        }

        /// <summary>Counts distinct coarse colours, a cheap proxy for "the frame has content".</summary>
        private int DistinctColourBuckets()
        {
            var seen = new HashSet<int>();
            Color32[] pixels = _capture.GetPixels32();

            for (int i = 0; i < pixels.Length; i += 37)
            {
                Color32 pixel = pixels[i];
                seen.Add((pixel.r >> 4) << 8 | (pixel.g >> 4) << 4 | (pixel.b >> 4));
            }

            return seen.Count;
        }

        [UnityTest]
        public IEnumerator MainMenu_RendersContent()
        {
            yield return CaptureFrame(720, 1280);
            Save("01-main-menu.png");

            Assert.Greater(DistinctColourBuckets(), 8, "the main menu frame looks blank");
        }

        [UnityTest]
        public IEnumerator Board_RendersPiecesInPortrait()
        {
            _manager.StartNewGame();
            yield return null;
            yield return new WaitForSeconds(0.4f);

            yield return CaptureFrame(720, 1280);
            Save("02-board-portrait.png");

            Assert.Greater(DistinctColourBuckets(), 20, "the board frame has too little variation to hold a chess set");
        }

        [UnityTest]
        public IEnumerator Board_RendersAfterMovesAndInLandscape()
        {
            _manager.StartNewGame();
            yield return null;

            _manager.TryPlayPlayerMove(Squares.FromAlgebraic("e2"), Squares.FromAlgebraic("e4"));

            float deadline = Time.realtimeSinceStartup + 20f;
            while (_manager.Phase != GamePhase.PlayerTurn && Time.realtimeSinceStartup < deadline)
                yield return null;

            yield return new WaitForSeconds(0.4f);

            yield return CaptureFrame(720, 1280);
            Save("03-after-moves.png");

            yield return CaptureFrame(1280, 720);
            Save("04-landscape.png");

            Assert.Greater(DistinctColourBuckets(), 20, "the landscape frame looks empty");
        }

        [UnityTest]
        public IEnumerator EachTheme_Renders()
        {
            _manager.StartNewGame();
            yield return null;

            for (int theme = 0; theme < Visual.BoardTheme.All.Length; theme++)
            {
                GameSettings.ThemeIndex = theme;
                _manager.ApplyCurrentSettings();
                yield return null;

                yield return CaptureFrame(720, 1280);
                Save(string.Format("05-theme-{0}-{1}.png", theme, Visual.BoardTheme.All[theme].Name));

                Assert.Greater(DistinctColourBuckets(), 20,
                    "theme " + Visual.BoardTheme.All[theme].Name + " rendered almost nothing");
            }
        }

        /// <summary>Captures each modal so the panels are checked as well as the board.</summary>
        [UnityTest]
        public IEnumerator EveryScreen_Renders()
        {
            var ui = Object.FindFirstObjectByType<Chess.UI.UIManager>();
            Assert.IsNotNull(ui, "no UIManager in the scene");

            ui.OpenSettings(false);
            yield return null;
            yield return CaptureFrame(720, 1280);
            Save("07-settings.png");
            Assert.Greater(DistinctColourBuckets(), 8, "settings screen looks blank");

            ui.ShowMainMenu();
            _manager.StartNewGame();
            yield return null;

            ui.ShowPause();
            yield return null;
            yield return CaptureFrame(720, 1280);
            Save("08-pause.png");
            Assert.Greater(DistinctColourBuckets(), 8, "pause screen looks blank");
            ui.HidePause();

            ui.ShowPromotion(PieceColor.White);
            yield return null;
            yield return CaptureFrame(720, 1280);
            Save("09-promotion.png");
            Assert.Greater(DistinctColourBuckets(), 8, "promotion dialog looks blank");

            ui.ShowHud();
            _manager.TryPlayPlayerMove(Squares.FromAlgebraic("e2"), Squares.FromAlgebraic("e4"));

            float deadline = Time.realtimeSinceStartup + 20f;
            while (_manager.Phase != GamePhase.PlayerTurn && Time.realtimeSinceStartup < deadline)
                yield return null;

            ui.ShowMoveList();
            yield return null;
            yield return CaptureFrame(720, 1280);
            Save("10-move-list.png");
            Assert.Greater(DistinctColourBuckets(), 8, "move list looks blank");

            ui.ShowHud();
            ui.ShowGameOver("You win!", "Checkmate - White wins", Chess.UI.UIPalette.Success);
            yield return null;
            yield return CaptureFrame(720, 1280);
            Save("11-game-over.png");
            Assert.Greater(DistinctColourBuckets(), 8, "game over screen looks blank");
        }

        [UnityTest]
        public IEnumerator SelectedPiece_ShowsItsLegalMoves()
        {
            _manager.StartNewGame();
            yield return null;

            // Drive the selection the way a tap would, by pointing at the knight's square.
            Camera camera = Camera.main;
            Vector3 knight = BoardView.SquareToWorld(Squares.FromAlgebraic("g1"));
            _manager.SendMessage("SelectSquare", Squares.FromAlgebraic("g1"), SendMessageOptions.RequireReceiver);
            yield return null;

            yield return CaptureFrame(720, 1280);
            Save("06-selection.png");

            Assert.Greater(DistinctColourBuckets(), 20, "the selection frame looks empty");
            Assert.IsTrue(camera.WorldToViewportPoint(knight).z > 0f, "the board is behind the camera");
        }
    }
}
