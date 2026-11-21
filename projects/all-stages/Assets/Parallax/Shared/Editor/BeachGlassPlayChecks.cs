using System;
using System.Reflection;
using GlassTrial;
using ShadeLink.Beach;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ShadeLink.Editor
{
    [InitializeOnLoad]
    public static class BeachGlassPlayChecks
    {
        const string Key = "Parallax.BeachGlassPlayChecks";
        static double started;
        static int frames;
        static BeachGame game;
        static BeachGlassHud glass;
        static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        static void Set(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
        static void UpdateGlass() => typeof(BeachGlassHud).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(glass, null);
        static void Require(bool value, string message) { if (!value) throw new Exception("BEACH_GLASS_PLAY_FAILED: " + message); }
        static BeachGlassPlayChecks()
        {
            if (SessionState.GetBool(Key, false)) { started = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; }
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Parallax/Beach/Scenes/Beach.unity");
            SessionState.SetBool(Key, true); started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 90) throw new Exception("play check timeout");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                if (game == null)
                {
                    game = UnityEngine.Object.FindFirstObjectByType<BeachGame>();
                    if (game == null || game.view == null) return;
                    glass = game.GetComponent<BeachGlassHud>();
                    if (glass == null) return;
                    game.automated = true; game.Resume(); game.safe = true; game.exposure = 0;
                }
                if (++frames < 6) return;
                var capture = game.view.GetComponent<GlassCameraCapture>();
                Require(capture != null && capture.captureEnabled, "capture enabled");
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    var previous = game.view.targetTexture; var frame = new RenderTexture(640, 360, 24);
                    try { game.view.targetTexture = frame; game.view.Render(); UpdateGlass(); }
                    finally { game.view.targetTexture = previous; frame.Release(); UnityEngine.Object.DestroyImmediate(frame); }
                    Require(glass.Ready, "glass capture ready");
                    Require(Field<Text>(glass, "caption").text == "그늘 · 안전", "safe default shown");
                    var earlyCommon = Field<MuseumCommonNoticeState>(glass, "commonNotice");
                    var pending = Field<System.Collections.Generic.Queue<MuseumStageGuide.Notice>>(glass, "queuedNotices");
                    game.Tell("02 구역 저장 · R 구역 재시도", 6);
                    Require(earlyCommon.Active && earlyCommon.Current.Kind == MuseumStageGuide.NoticeKind.Save && earlyCommon.Current.Text == "02 구역 저장" && earlyCommon.Current.Seconds == 2.5f, "save routes immediately");
                    game.Tell("위에 놓인 사물을 먼저 옮겨 주세요.");
                    Require(earlyCommon.Active && earlyCommon.Current.Kind == MuseumStageGuide.NoticeKind.Warning && earlyCommon.Current.Seconds == 3f && pending.Count == 1, "warning preempts but retains save");
                    UpdateGlass();
                    var amber = Field<Image>(glass, "dot").color;
                    Require(Mathf.Abs(amber.r - 251f / 255f) < .01f && Mathf.Abs(amber.g - 191f / 255f) < .01f && Mathf.Abs(amber.b - 36f / 255f) < .01f, "warning indicator is amber");
                    var actionDie = game.props[0];
                    var fixtureObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    fixtureObject.name = "Isolated standing-warning fixture";
                    fixtureObject.transform.position = new Vector3(0, 400, 0);
                    fixtureObject.transform.localScale = new Vector3(3, 1, 3);
                    var fixtureProp = fixtureObject.AddComponent<BeachProp>();
                    fixtureProp.Remember(); fixtureProp.InitializePhysics();
                    Physics.SyncTransforms();
                    var beforeFeet = game.controller.transform.position;
                    var beforeView = game.view.transform.rotation;
                    var bounds = fixtureObject.GetComponent<Collider>().bounds;
                    game.controller.enabled = false;
                    game.controller.transform.position = new Vector3(bounds.center.x, bounds.max.y + .025f, bounds.center.z);
                    game.controller.enabled = true;
                    game.view.transform.position = game.controller.transform.position + Vector3.up * 1.55f;
                    game.view.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                    Physics.SyncTransforms();
                    game.Interact();
                    Require(game.held == null && earlyCommon.Current.Kind == MuseumStageGuide.NoticeKind.Warning && earlyCommon.Current.Text == "서 있는 사물은 잡을 수 없습니다." && earlyCommon.Current.Seconds == 2, "actual supported prop pickup warning");
                    game.controller.enabled = false; game.controller.transform.position = beforeFeet; game.controller.enabled = true;
                    game.view.transform.rotation = beforeView; game.SyncView();
                    UnityEngine.Object.DestroyImmediate(fixtureObject); Physics.SyncTransforms();
                    game.Tell("현재 구역의 사물과 위치를 초기화했습니다. 이전 구역은 유지됩니다.");
                    Require(earlyCommon.Active && earlyCommon.Current.Kind == MuseumStageGuide.NoticeKind.Reset && earlyCommon.Current.Seconds == 3f && pending.Count == 0, "reset replaces warning and stale save");
                    earlyCommon.Update(1000000, true);
                    game.safe = false; game.exposure = .4f; UpdateGlass();
                    Require(Mathf.Abs(Field<Material>(glass, "exposureMaterial").GetFloat("_Fill") - .4f) < .001f && Field<Image>(glass, "exposureSun").color.a > 0, "sun gauge uses actual exposure");
                    game.Tell("빛·물 위험에서 현재 쉼터로 복귀했습니다. 놓은 사물 배치는 유지됩니다.");
                    Require(earlyCommon.Active && earlyCommon.Current.Kind == MuseumStageGuide.NoticeKind.Reset, "danger recovery retained while sun gauge takes priority");
                    game.safe = true; game.exposure = 0;
                    earlyCommon.Update(1000000, true);
                    game.Tell("두 수신기 연결 완료 · 게이트가 열렸습니다. 그늘을 이어 모래성으로 가세요.", 7); UpdateGlass();
                    Require(Field<string>(glass, "gameplayNotice") == "수신기가 활성화 되었어요", "receiver notice mapped");
                    game.Tell("바다에 떨어진 바위를 원래 해안가로 돌려놓았어요.", 4); UpdateGlass();
                    Require(Field<MuseumCommonNoticeState>(glass, "commonNotice").Current.Text == "바위가 떨어지지 않게 조심해요", "rock warning mapped");
                    var hover = game.GetComponent<BeachGlassHover>();
                    Require(hover != null, "hover component installed");
                    game.aimed = game.props[0];
                    typeof(BeachGlassHover).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hover, null);
                    Require(hover.NeedsCapture && !string.IsNullOrEmpty(Field<Text>(hover, "label").text), "aimed prop hover");
                    game.aimed = null;
                    var common = Field<MuseumCommonNoticeState>(glass, "commonNotice");
                    common.Update(1000000, true); Field<System.Collections.Generic.Queue<MuseumStageGuide.Notice>>(glass, "queuedNotices").Clear();
                    Set(glass, "gameplayNoticeRemaining", 0f);
                    game.checkpoint = 2;
                    game.view.transform.position = game.shadowHarbor.target.position + Vector3.back * 4;
                    typeof(BeachShadowHarbor).GetProperty("HullCoverage").GetSetMethod(true).Invoke(game.shadowHarbor, new object[] { .42f });
                    typeof(BeachShadowHarbor).GetProperty("CanopyCoverage").GetSetMethod(true).Invoke(game.shadowHarbor, new object[] { .42f });
                    typeof(BeachShadowHarbor).GetProperty("Coverage").GetSetMethod(true).Invoke(game.shadowHarbor, new object[] { .43f });
                    typeof(BeachShadowHarbor).GetProperty("Clearance").GetSetMethod(true).Invoke(game.shadowHarbor, new object[] { .44f });
                    UpdateGlass();
                    Require(Mathf.Abs(Field<Material>(glass, "exposureMaterial").GetFloat("_PuzzleMode") - 1f) < .001f && Mathf.Abs(Field<Material>(glass, "exposureMaterial").GetFloat("_Fill") - .350625f) < .001f, "shadow puzzle shows computed gauge");
                    typeof(BeachShadowHarbor).GetProperty("HullCoverage").GetSetMethod(true).Invoke(game.shadowHarbor, new object[] { .84f });
                    typeof(BeachShadowHarbor).GetProperty("CanopyCoverage").GetSetMethod(true).Invoke(game.shadowHarbor, new object[] { 0f });
                    typeof(BeachShadowHarbor).GetProperty("Clearance").GetSetMethod(true).Invoke(game.shadowHarbor, new object[] { .88f });
                    UpdateGlass();
                    Require(Mathf.Abs(Field<Material>(glass, "exposureMaterial").GetFloat("_Fill") - .425f) < .001f, "one matched region raises gauge");
                }
                var finale = game.counterweightFinale;
                Require(finale != null && game.shadowHarbor != null && game.skyShip != null, "latest harbor and ship present");
                game.checkpoint = 3;
                Set(finale, "loadLatched", true); Set(finale, "ready", false); Set(finale, "liftProgress", .45f);
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    Field<MuseumCommonNoticeState>(glass, "commonNotice").Update(1000000, true);
                    Field<System.Collections.Generic.Queue<MuseumStageGuide.Notice>>(glass, "queuedNotices").Clear();
                    Set(glass, "gameplayNoticeRemaining", 0f); game.safe = true; game.exposure = 0;
                    UpdateGlass();
                    Require(Mathf.Abs(Field<Material>(glass, "exposureMaterial").GetFloat("_Fill") - .45f) < .001f, "hoist gauge uses actual lift progress");
                    Set(finale, "ready", true); UpdateGlass();
                    Require(Field<string>(glass, "gameplayNotice") == "인양을 마쳤어요" && Mathf.Abs(Field<float>(glass, "gameplayNoticeRemaining") - 3f) < .01f, "hoist completion notice lasts three seconds");
                }
                game.ResetZone();
                Require(game.checkpoint == 3 && !finale.LoadLatched && Mathf.Approximately(finale.LiftProgress, 0), "R reset behavior remains");
                Require(Field<MuseumCommonNoticeState>(glass, "commonNotice").Current.Kind == MuseumStageGuide.NoticeKind.Reset, "R triggers reset presentation");
                game.held = null;
                game.aimed = null;
                game.Pause(); Require(!game.active, "pause remains"); game.Resume(); Require(game.active, "resume remains");
                Debug.Log("BEACH_GLASS_PLAY_CHECKS_PASSED");
                SessionState.EraseBool(Key); EditorApplication.update -= Tick; EditorApplication.isPlaying = false; EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e); SessionState.EraseBool(Key); EditorApplication.update -= Tick; EditorApplication.isPlaying = false; EditorApplication.Exit(1);
            }
        }
    }
}