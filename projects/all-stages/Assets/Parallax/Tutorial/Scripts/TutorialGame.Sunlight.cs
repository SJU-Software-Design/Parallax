using UnityEngine;
using UnityEngine.Rendering;
using ShadeLink.Gallery;

namespace ShadeLink.Tutorial
{
    public sealed partial class TutorialGame
    {
        static readonly Vector3 SunlightOrigin = new Vector3(ClimbWallX + ClimbLandingDepth, ClimbDoorHeight, DoorCenter - 9.5f);
        static readonly Vector3 SunlightToSun = new Vector3(.75f, 1f, -1.1f).normalized;
        static readonly Vector3[] SunlightStops =
        {
            new Vector3(2.2f, .03f, 9.5f), new Vector3(15.5f, .03f, 9.5f),
            new Vector3(26.7f, .03f, 9.5f), new Vector3(27f, .03f, 26.5f)
        };
        static Bounds SunlightBounds => new Bounds(SunlightOrigin + new Vector3(19f, 7f, 16.5f), new Vector3(38f, 14f, 33f));
        readonly RaycastHit[] sunlightHits = new RaycastHit[64];
        Transform sunlightRoot;
        GalleryProp[] sunlightDice;
        Light sunlight;
        Light[] trainingLights;
        AudioSource sunlightWarning;
        AudioClip sunlightWarningClip;
        bool inSunlightRoom, sunlightComplete;
        int sunlightCheckpoint;
        float sunlightExposure, sunlightNextWarning;
        float trainingShadowDistance;
        ShadowQuality trainingShadowQuality;

        void BuildSunlightCourse()
        {
            trainingLights = GetComponentsInChildren<Light>();
            trainingShadowDistance = QualitySettings.shadowDistance;
            trainingShadowQuality = QualitySettings.shadows;
            sunlightRoot = new GameObject("03 · Sunlight courtyard draft").transform;
            sunlightRoot.SetParent(transform, false);
            sunlightRoot.localPosition = SunlightOrigin;
            SunlightFloor(new Bounds(new Vector3(19, 0, 7), new Vector3(38, 0, 14)));
            SunlightFloor(new Bounds(new Vector3(31, 0, 22), new Vector3(14, 0, 16)));
            SunlightFloor(new Bounds(new Vector3(27, 0, 31.5f), new Vector3(3, 0, 3)));

            var glass = new Material(surfaceShader)
                { name = "Courtyard frosted glass", color = new Color(.58f, .64f, .65f, .68f) };
            glass.SetFloat("_Mode", 2);
            glass.SetFloat("_Glossiness", .18f);
            glass.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            glass.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            glass.SetInt("_ZWrite", 0);
            glass.EnableKeyword("_ALPHABLEND_ON");
            glass.renderQueue = 3000;
            // Full-height glazing on the sunward boundaries avoids a permanent safe strip along a solid wall.
            SunlightWall(new Vector3(19, 0, 0), Vector3.forward, 38, glass, true);
            SunlightWall(new Vector3(38, 0, 15), Vector3.left, 30, glass, true);
            SunlightWall(new Vector3(12, 0, 14), Vector3.back, 24, steel);
            SunlightWall(new Vector3(24, 0, 22), Vector3.right, 16, steel);
            SunlightWall(new Vector3(0, 0, 4.15f), Vector3.right, 8.3f, steel);
            SunlightWall(new Vector3(0, 0, 12.35f), Vector3.right, 3.3f, steel);
            SunlightWall(new Vector3(24.75f, 0, 30), Vector3.back, 1.5f, steel);
            SunlightWall(new Vector3(33.25f, 0, 30), Vector3.back, 9.5f, steel);
            SunBox("Exit lintel wall", new Vector3(27, 4, 30), new Vector3(3, 2, .15f), steel);
            SunBox("Exit passage back", new Vector3(27, 1.5f, 33.1f), new Vector3(3.3f, 3, .2f), frame);
            foreach (float x in new[] { 25.4f, 28.6f })
                SunBox("Exit passage side", new Vector3(x, 1.5f, 31.5f), new Vector3(.2f, 3, 3), frame);

            SunlightCanopy("Entry shelter", new Vector3(3.5f, 0, 9.5f), new Vector2(7, 7));
            SunlightCanopy("First rest shelter", SunlightStops[1], new Vector2(5, 7));
            SunlightCanopy("Corner shelter", new Vector3(26.8f, 0, 9.7f), new Vector2(7.6f, 7));
            SunlightCanopy("Exit shelter", new Vector3(27, 0, 28), new Vector2(6, 10));

            sunlightDice = new GalleryProp[3];
            for (int i = 0; i < sunlightDice.Length; i++)
            {
                float side = i == 0 ? .85f : i == 1 ? .95f : 1.15f;
                var position = new Vector3(i == 0 ? 4.2f : i == 1 ? 15.4f : 26.6f, 0, 8.2f);
                SunBox("Die pedestal " + (i + 1), position + Vector3.up * .32f, new Vector3(1.45f, .64f, 1.35f), frame);
                SunBox("Pedestal top", position + Vector3.up * .65f, new Vector3(1.5f, .06f, 1.4f), paleSteel);
                var prop = BuildDie("Sunlight die " + (i + 1), SunlightOrigin + position + Vector3.up * (.68f + side * .5f), side);
                prop.transform.SetParent(sunlightRoot, true);
                prop.Remember();
                prop.InitializePhysics();
                prop.placement.PreviewUntilSettled = false;
                prop.placement.SetPlayer(Controller);
                AttachLandingFeedback(prop);
                sunlightDice[i] = prop;
            }

            var enamel = new Material(surfaceShader) { color = new Color(.86f, .87f, .83f) };
            Color ink = new Color(.035f, .055f, .06f);
            Color accent = new Color(.14f, .36f, .31f);
            SunlightSign(0, "햇빛을 가려 길 만들기",
                "햇빛 아래에서는 오래 버틸 수 없어요.\n그늘로 들어오면 다시 안전해져요.\n\n주사위를 가까이서 집어 크게 놓아 보세요.\n물체 뒤에 생긴 그늘을 따라\n다음 지붕 아래로 이동해요.", enamel, ink, accent);
            SunlightSign(1, "그늘과 그늘을 이어 보세요",
                "여기에는 새 주사위가 하나 더 있어요.\n앞에서 놓은 주사위도 그대로 남아 있어요.\n\n크기와 위치를 조절해서\n햇빛이 남은 틈을 가려 보세요.\n한 개로 해결해도 괜찮아요.", enamel, ink, accent);
            SunlightSign(2, "꺾인 길도 그늘로 연결해요",
                "마지막 주사위가 준비되어 있어요.\n햇빛은 계속 같은 방향에서 들어와요.\n\n물체를 햇빛이 오는 쪽에 놓고\n그늘이 출구까지 이어지게 해 보세요.\nR을 누르면 이 쉼터로 돌아와요.", enamel, ink, accent);

            foreach (float x in new[] { 25.55f, 28.45f })
                SunBox("Exit steel jamb", new Vector3(x, 1.6f, 29.55f), new Vector3(.18f, 3.2f, .22f), frame);
            SunBox("Exit steel header", new Vector3(27, 3.13f, 29.55f), new Vector3(3.1f, .18f, .22f), frame);
            SunBox("Sunlight exit indicator", new Vector3(27, 3.4f, 29.5f), new Vector3(.8f, .09f, .08f), unlockedLamp, false);
            var exitLabel = SignText(sunlightRoot, "Sunlight exit label", new Vector3(27, 2.85f, 29.48f), .012f, Color.white);
            exitLabel.anchor = TextAnchor.MiddleCenter;
            exitLabel.text = "출구";

            sunlight = new GameObject("Fixed courtyard sunlight").AddComponent<Light>();
            sunlight.transform.SetParent(sunlightRoot, false);
            sunlight.transform.rotation = Quaternion.LookRotation(-SunlightToSun);
            sunlight.type = LightType.Directional;
            sunlight.intensity = 1.04f;
            sunlight.color = new Color(1, .94f, .82f);
            sunlight.shadows = LightShadows.Soft;
            sunlight.shadowStrength = 1f;
            sunlight.shadowBias = .018f;
            sunlight.shadowNormalBias = .12f;
            sunlight.shadowResolution = LightShadowResolution.VeryHigh;
            sunlight.enabled = false;

            sunlightWarning = sunlightRoot.gameObject.AddComponent<AudioSource>();
            sunlightWarning.spatialBlend = 0;
            sunlightWarning.volume = .13f;
            const int samples = 6615;
            var wave = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / 44100f;
                wave[i] = Mathf.Sin(t * 430f * Mathf.PI * 2) * Mathf.Sin(i / (float)samples * Mathf.PI) * .5f;
            }
            sunlightWarningClip = AudioClip.Create("Sunlight warning", samples, 1, 44100, false);
            sunlightWarningClip.SetData(wave, 0);
            ResetSunlightCourse();
        }

        GameObject SunBox(string name, Vector3 position, Vector3 size, Material material, bool solid = true)
        {
            var box = Box(name, position, size, material, solid);
            box.transform.SetParent(sunlightRoot, false);
            return box;
        }

        void SunlightFloor(Bounds bounds)
        {
            int first = transform.childCount;
            BuildFloor(bounds, "Sunlight floor");
            for (int i = transform.childCount - 1; i >= first; i--)
                transform.GetChild(i).SetParent(sunlightRoot, false);
        }

        void SunlightWall(Vector3 center, Vector3 inward, float span, Material material, bool glazed = false)
        {
            Vector3 along = Vector3.Cross(inward, Vector3.up);
            Vector3 axes = new Vector3(Mathf.Abs(along.x), 0, Mathf.Abs(along.z));
            Vector3 depth = new Vector3(Mathf.Abs(inward.x), 0, Mathf.Abs(inward.z));
            float height = glazed ? 5.8f : 5f;
            var wall = SunBox(glazed ? "Sunward glazing" : "Courtyard metal wall", center + Vector3.up * height * .5f,
                axes * span + Vector3.up * height + depth * .15f, material);
            if (glazed) wall.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            int panels = Mathf.CeilToInt(span / 3.2f);
            for (int i = 0; i <= panels; i++)
                SunBox("Courtyard panel joint", center + along * ((i / (float)panels - .5f) * span) + Vector3.up * height * .5f,
                    axes * (glazed ? .065f : .035f) + Vector3.up * height + depth * .18f, frame);
        }

        void SunlightCanopy(string name, Vector3 shadeCenter, Vector2 size)
        {
            const float height = 3.2f;
            // Offset the physical roof toward the sun so its floor shadow covers the rest bay.
            var center = new Vector3(shadeCenter.x + .75f * height, height, shadeCenter.z - 1.1f * height);
            SunBox(name + " roof", center, new Vector3(size.x, .24f, size.y), frame);
            SunBox(name + " roof inset", center + Vector3.up * .14f, new Vector3(size.x - .16f, .06f, size.y - .16f), steel);
            foreach (float x in new[] { -.5f, .5f })
                foreach (float z in new[] { -.5f, .5f })
                {
                    var foot = center + new Vector3(x * (size.x - .35f), -height * .5f, z * (size.y - 1.15f));
                    SunBox(name + " upright", foot, new Vector3(.15f, height, .15f), frame);
                }
        }

        void SunlightSign(int index, string title, string body, Material material, Color ink, Color accent)
        {
            var sign = BuildPlacard("03 / " + (index + 1) + "   햇빛 훈련", title, body, material, ink, accent).transform;
            sign.SetParent(sunlightRoot, false);
            sign.localPosition = new Vector3(SunlightStops[index].x + 1.1f, 0, 12.6f);
            sign.localRotation = Quaternion.Euler(0, 90, 0);
            sign.localScale = Vector3.one * .92f;
        }

        void SetSunlightLighting(bool active)
        {
            if (!active) ClearShadowPreview();
            if (sunlight == null) return;
            sunlight.enabled = active;
            foreach (var light in trainingLights) if (light != null) light.enabled = !active;
            RenderSettings.ambientSkyColor = active ? new Color(.48f, .54f, .58f) : new Color(.75f, .75f, .74f);
            RenderSettings.ambientEquatorColor = active ? new Color(.35f, .39f, .42f) : new Color(.65f, .65f, .64f);
            RenderSettings.ambientGroundColor = active ? new Color(.25f, .27f, .28f) : new Color(.49f, .48f, .47f);
            Camera.backgroundColor = active ? new Color(.62f, .71f, .77f) : steel.color;
            if (active)
            {
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowDistance = 85f;
            }
            else
            {
                QualitySettings.shadows = trainingShadowQuality;
                QualitySettings.shadowDistance = trainingShadowDistance;
            }
        }

        void EnterSunlightRoom()
        {
            ClearLandingFeedback();
            if (Held != null) interaction.Cancel(true);
            if (Aimed != null) Aimed.Highlight(false, false);
            Aimed = null;
            inClimbRoom = false;
            climbArrivalPlaying = false;
            inSunlightRoom = true;
            Props = sunlightDice;
            motor.MantleReachFor = null;
            sunlightExposure = 0;
            SetSunlightLighting(true);
            firstSign.SetActive(false); shrinkSign.SetActive(false); secondSign.SetActive(false);
            primaryTarget.gameObject.SetActive(false); tabletopTarget.gameObject.SetActive(false); secondaryTarget.gameObject.SetActive(false);
            Tell("햇빛이 닿으면 그늘로 돌아오세요. 주사위를 크게 놓아 다음 지붕까지 그늘을 만들어 봐요.", 9f);
        }

        void ResetSunlightCourse()
        {
            sunlightCheckpoint = 0;
            sunlightExposure = 0;
            sunlightComplete = false;
            if (sunlightDice != null) foreach (var prop in sunlightDice) prop.Restore();
        }

        void MoveToSunlightCheckpoint()
        {
            Controller.enabled = false;
            Controller.transform.position = SunlightOrigin + SunlightStops[sunlightCheckpoint];
            Controller.enabled = true;
            motor.Reset(); controls.Reset();
        }

        void RespawnSunlight()
        {
            ClearLandingFeedback();
            if (Held != null) interaction.Cancel(true);
            if (Aimed != null) Aimed.Highlight(false, false);
            Aimed = null;
            sunlightExposure = 0;
            MoveToSunlightCheckpoint();
            Tell("마지막 쉼터로 돌아왔어요. 놓아 둔 주사위는 그대로예요. 크기나 위치를 조금 바꿔 봐요.", 5f);
        }

        bool SunlightSafe(Vector3 feet)
        {
            // shortcut: the die's box includes its small bevel; keep that forgiving edge for this draft.
            int count = Physics.RaycastNonAlloc(feet + Vector3.up * .035f, SunlightToSun, sunlightHits, 100f,
                1 << 0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var shape = sunlightHits[i].collider;
                if (shape == null || shape == Controller) continue;
                var owner = shape.GetComponentInParent<GalleryProp>();
                if (owner != null)
                {
                    if (owner == Held || owner.placement.Held) continue;
                    return true;
                }
                var renderer = shape.GetComponent<Renderer>();
                if (renderer != null && renderer.shadowCastingMode != ShadowCastingMode.Off) return true;
            }
            return false;
        }

        void TickSunlight(float dt)
        {
            Physics.SyncTransforms();
            Vector3 feet = Controller.transform.position;
            Vector3 local = feet - SunlightOrigin;
            foreach (var prop in sunlightDice)
                if (prop != Held && prop.transform.position.y < SunlightOrigin.y - 3f)
                {
                    prop.GetComponent<TutorialLandingFeedback>().ClearPending();
                    prop.Restore();
                    Tell("떨어진 주사위는 원래 받침대에 돌아왔어요.");
                }
            if (local.y < -2.2f || local.x < -2.5f) { RespawnSunlight(); return; }
            if (sunlightComplete) return;
            bool safe = SunlightSafe(feet);
            sunlightExposure = Mathf.Clamp01(sunlightExposure + Mathf.Min(dt, .05f) * (safe ? -2f : .92f));
            if (sunlightExposure > .28f && Time.time >= sunlightNextWarning)
            {
                sunlightWarning.PlayOneShot(sunlightWarningClip);
                sunlightNextWarning = Time.time + .65f;
            }
            if (sunlightExposure >= 1f) { RespawnSunlight(); return; }
            if (!safe || local.y > .18f) return;
            if (sunlightCheckpoint < 1 && local.x > 13.5f && local.x < 17.5f && Mathf.Abs(local.z - 9.5f) < 2f)
            {
                sunlightCheckpoint = 1;
                Tell("첫 쉼터에 도착했어요. 새 주사위로 다음 그늘을 이어 보세요.", 6f);
            }
            if (sunlightCheckpoint < 2 && local.x > 24f && local.x < 29.8f && Mathf.Abs(local.z - 9.5f) < 2f)
            {
                sunlightCheckpoint = 2;
                Tell("모퉁이 쉼터예요. 왼쪽으로 꺾인 길을 보고, 햇빛이 오는 쪽에 주사위를 놓아 보세요.", 7f);
            }
            if (local.z > 30.8f && local.x > 25.5f && local.x < 28.5f)
            {
                sunlightComplete = true;
                sunlightExposure = 0;
                Debug.Log("TUTORIAL_SUNLIGHT_COMPLETE");
            }
        }

        void DrawSunlightExposure(float width, float height)
        {
            if (sunlightExposure <= .03f) return;
            Color previous = GUI.color;
            GUI.color = new Color(1f, .52f, .16f, sunlightExposure * .25f);
            GUI.DrawTexture(new Rect(0, 0, width, 32), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, height - 32, width, 32), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, 32, 24, height - 64), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width - 24, 32, 24, height - 64), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
