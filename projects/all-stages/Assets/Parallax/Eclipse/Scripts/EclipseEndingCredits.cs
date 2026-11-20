using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ShadeLink.Eclipse
{
    public enum EndingCreditSection { Inactive, Intro, Team, Assets, Thanks, Return, Finished }

    [Serializable]
    public sealed class EclipseCreditEntry
    {
        public string id, usage, displayUsage, title, creator, license, url, role;
    }

    [Serializable]
    public sealed class EclipseCreditDocument
    {
        public int schemaVersion;
        public EclipseCreditEntry[] team, assets;
    }

    // Ground text and the cinematic use the same clock as the input contract.
    // Only the asset section accepts fast-forward or skip.
    public sealed class EclipseEndingCredits : MonoBehaviour
    {
        public const float IntroDuration = 7f;
        public const float ThanksDuration = 8f;
        public const float ReturnDuration = 4f;
        public const float FastForwardMultiplier = 5f;
        public const float NormalRollSpeed = 1.15f;
        const float ScrollSpeed = .42f, MaximumTextScale = 1.4f, CardGap = .20f;
        public EndingCreditSection Section { get; private set; }
        public float Elapsed { get; private set; }
        public float SectionElapsed { get; private set; }
        public float TeamDuration { get; private set; }
        public float AssetsDuration { get; private set; }
        public bool TeamComplete { get; private set; }
        public bool ThanksComplete { get; private set; }
        public bool Active => Section != EndingCreditSection.Inactive && Section != EndingCreditSection.Finished;
        public bool CanControlCredits => Section == EndingCreditSection.Assets;
        public float AppliedSpeed { get; private set; } = 1;
        public Vector3 LockedMoonPosition { get; private set; }
        public Quaternion LockedMoonRotation { get; private set; }
        public Vector3 LockedMoonScale { get; private set; }
        public Vector3 GameplayMoonPosition { get; private set; }
        public Quaternion GameplayMoonRotation { get; private set; }
        public Vector3 GameplayMoonScale { get; private set; }
        public bool AdjustedForClearRoute { get; private set; }
        public Vector3 ReturnMoonPosition => GameplayMoonPosition;
        public bool ReturnSkyPoseApplied => AdjustedForClearRoute;
        public Vector3 LockedCameraPosition { get; private set; }
        public float LockedFieldOfView { get; private set; }
        public Vector3 RaisedCameraPosition { get; private set; }
        public float CreditPitchDegrees { get; private set; }
        public float CameraLiftProgress { get; private set; }
        public float PresentedMoonDistance { get; private set; }
        public Vector3 LockedSunPosition { get; private set; }
        // Uses the same .97 lunar silhouette margin as gameplay coverage.
        public float LockedMoonSunAngularRatio { get; private set; }
        public float PresentedMoonSunAngularRatio { get; private set; }
        public const float MaximumCinematicMoonRadiusDegrees = 11.5f;
        public float LockedMoonOuterRadius { get; private set; }
        public float CinematicMoonAngularRadiusDegrees { get; private set; }
        public float CinematicMoonSunAngularRatio { get; private set; }
        public bool CinematicReframedMoon { get; private set; }
        public float PresentedMoonSurfaceClearance { get; private set; }
        public float PresentedMoonFloorClearance { get; private set; }
        public float PresentedSunSurfaceGap { get; private set; }
        public string CurrentText => string.Join("\n", VisibleRows);
        public string[] VisibleRows { get; private set; } = Array.Empty<string>();
        public string[] PresentedTeamLines => seenTeam.ToArray();
        public int AssetCount => assetCards.Count;
        public bool HasUsageMetadata => assetCards.Count > 0 && usageEntries == assetCards.Count;

        sealed class Card
        {
            public string id, text;
            public float at, width, depth;
        }
        sealed class Row
        {
            public TextMesh text;
            public MeshRenderer renderer;
            public string cached;
            public float width, depth;
        }

        EclipseFinaleGame game;
        readonly List<Card> teamCards = new List<Card>();
        readonly List<Card> assetCards = new List<Card>();
        readonly List<string> seenTeam = new List<string>();
        readonly Row[] rows = new Row[10];
        Font bodyFont, titleFont;
        Material bodyMaterial, titleMaterial;
        Transform floorRoot;
        Quaternion startRotation, creditRotation, returnRotation;
        float creditYaw;
        Vector3 originalSunDirection;
        int usageEntries;
        bool ready;
        readonly List<string> visible = new List<string>();
        readonly Color ivory = new Color(.92f, .90f, .82f);

        public void Initialize(EclipseFinaleGame owner)
        {
            game = owner;
            bodyFont = Resources.Load<Font>("Preview/BodyFont");
            titleFont = Resources.Load<Font>("Preview/TitleFont");
            if (bodyFont == null) bodyFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 80);
            if (titleFont == null) titleFont = bodyFont;
            Shader shader = Resources.Load<Shader>("Eclipse/FloorCreditsShader");
            if (shader == null) shader = Shader.Find("GUI/Text Shader");
            bodyMaterial = new Material(shader) { name = "Eclipse credits / body font" };
            titleMaterial = new Material(shader) { name = "Eclipse credits / title font" };
            floorRoot = new GameObject("ENDING / projected floor credits").transform;
            floorRoot.SetParent(transform, false);
            for (int i = 0; i < rows.Length; i++)
            {
                var item = new GameObject("Floor credit row " + i);
                item.transform.SetParent(floorRoot, false);
                item.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var text = item.AddComponent<TextMesh>();
                text.font = bodyFont; text.fontSize = 80; text.characterSize = 1;
                text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
                text.lineSpacing = 1.1f; text.richText = false;
                var renderer = item.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                rows[i] = new Row { text = text, renderer = renderer };
                item.SetActive(false);
            }
            LoadDocument();
            Font.textureRebuilt += FontRebuilt;
            RefreshFontTextures();
            ready = true;
            ResetSequence();
        }

        void LoadDocument()
        {
            var file = Resources.Load<TextAsset>("Eclipse/EndingCredits");
            EclipseCreditDocument doc = null;
            if (file != null)
            {
                try { doc = JsonUtility.FromJson<EclipseCreditDocument>(file.text); }
                catch (Exception e) { Debug.LogError("Eclipse credits document: " + e.Message); }
            }
            if (doc == null || doc.team == null || doc.team.Length != 4 || doc.assets == null || doc.assets.Length == 0)
            {
                Debug.LogError("Eclipse ending requires the verified team and asset-use credits document.");
                return;
            }
            float at = 0;
            foreach (var entry in doc.team)
            {
                string name = entry.title ?? "";
                string role = entry.role ?? entry.usage ?? "";
                string text = name + "\n" + Wrap(role, 34);
                teamCards.Add(MakeCard(entry.id, text, at, true));
                at += Mathf.Max(6f, CountLines(text) * 1.25f);
            }
            SpaceCards(teamCards, 6f, 1.25f);
            TeamDuration = (teamCards.Count > 0 ? teamCards[teamCards.Count - 1].at : 0) + 17;
            at = 0;
            foreach (var entry in doc.assets)
            {
                string use = !string.IsNullOrWhiteSpace(entry.displayUsage) ? entry.displayUsage : entry.usage ?? "";
                string title = entry.title ?? "";
                string author = entry.creator ?? "";
                string license = entry.license ?? "";
                string attribution = string.IsNullOrWhiteSpace(author) ? license :
                    string.IsNullOrWhiteSpace(license) ? author : author + " · " + license;
                string line = Wrap(use + " — " + title, 46);
                if (!string.IsNullOrWhiteSpace(attribution)) line += "\n" + Wrap(attribution, 48);
                assetCards.Add(MakeCard(entry.id, line, at, false));
                if (!string.IsNullOrWhiteSpace(use)) usageEntries++;
                at += Mathf.Max(2.8f, CountLines(line) * .82f);
            }
            SpaceCards(assetCards, 2.8f, .82f);
            AssetsDuration = (assetCards.Count > 0 ? assetCards[assetCards.Count - 1].at : 0) + 17;
        }

        static void SpaceCards(List<Card> cards, float minimumReadTime, float secondsPerLine)
        {
            if (cards.Count == 0) return;
            cards[0].at = 0;
            for (int i = 1; i < cards.Count; i++)
            {
                Card previous = cards[i - 1], next = cards[i];
                // The ground stream moves at a constant world speed. Reserve
                // room for both cards at the largest supported text size.
                float clearance = ((previous.depth + next.depth) * .5f * MaximumTextScale + CardGap) / ScrollSpeed;
                float readTime = Mathf.Max(minimumReadTime, CountLines(previous.text) * secondsPerLine);
                next.at = previous.at + Mathf.Max(clearance, readTime);
            }
        }

        static Card MakeCard(string id, string text, float at, bool team)
        {
            int longest = 0;
            foreach (string line in text.Split('\n')) longest = Mathf.Max(longest, Cells(line));
            return new Card { id = id, text = text, at = at,
                width = Mathf.Clamp(longest * (team ? .16f : .145f), 2.8f, 7f),
                depth = CountLines(text) * (team ? .56f : .50f) };
        }

        static int CountLines(string value) { return value.Split('\n').Length; }
        static int Cells(string value)
        {
            int length = 0;
            foreach (char ch in value) length += ch >= '\u2e80' ? 2 : 1;
            return length;
        }
        static string Wrap(string value, int maximum)
        {
            var output = new StringBuilder(); int line = 0;
            foreach (char ch in value)
            {
                if (ch == '\n') { output.Append(ch); line = 0; continue; }
                int cells = ch >= '\u2e80' ? 2 : 1;
                if (line + cells > maximum) { output.Append('\n'); line = 0; if (ch == ' ') continue; }
                output.Append(ch); line += cells;
            }
            return output.ToString().Trim();
        }

        void FontRebuilt(Font changed) { if (changed == bodyFont || changed == titleFont) RefreshFontTextures(); }
        void RefreshFontTextures()
        {
            if (bodyMaterial != null && bodyFont != null) bodyMaterial.mainTexture = bodyFont.material.mainTexture;
            if (titleMaterial != null && titleFont != null) titleMaterial.mainTexture = titleFont.material.mainTexture;
        }

        public void ResetSequence()
        {
            if(game!=null && game.world!=null)game.world.ResetExitRoute();
            Section = EndingCreditSection.Inactive; Elapsed = SectionElapsed = 0;
            AppliedSpeed = 1; CameraLiftProgress = 0; PresentedMoonSunAngularRatio = 0;
            CinematicMoonAngularRadiusDegrees = CinematicMoonSunAngularRatio = PresentedMoonSurfaceClearance = PresentedMoonFloorClearance = PresentedSunSurfaceGap = 0;
            CinematicReframedMoon = AdjustedForClearRoute = false; TeamComplete = ThanksComplete = false; seenTeam.Clear();
            VisibleRows = Array.Empty<string>();
            if (ready) foreach (var row in rows) row.text.gameObject.SetActive(false);
        }

        public bool Begin()
        {
            if (!ready || Active || Section == EndingCreditSection.Finished || teamCards.Count != 4 || assetCards.Count == 0) return false;
            LockedMoonPosition = game.moon.position; LockedMoonRotation = game.moon.rotation; LockedMoonScale = game.moon.localScale;
            LockedCameraPosition = game.view.transform.position; LockedFieldOfView = game.view.fieldOfView;
            startRotation = game.view.transform.rotation;
            LockedMoonOuterRadius = game.MoonRadius;
            var filter = game.moon.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                    LockedMoonOuterRadius = Mathf.Max(LockedMoonOuterRadius,
                        Vector3.Scale(vertex, LockedMoonScale).magnitude);
            LockedSunPosition = EclipseFinaleGame.SunPosition;
            originalSunDirection = (LockedSunPosition - LockedCameraPosition).normalized;
            float originalSunAngle = Mathf.Asin(Mathf.Clamp01(EclipseFinaleGame.SunRadius / Vector3.Distance(LockedSunPosition, LockedCameraPosition)));
            float originalMoonAngle = Mathf.Asin(Mathf.Clamp01(game.MoonRadius * .97f / Vector3.Distance(LockedMoonPosition, LockedCameraPosition)));
            LockedMoonSunAngularRatio = originalMoonAngle / Mathf.Max(.00001f, originalSunAngle);
            PrepareGameplayMoonPose();
            creditYaw = Mathf.Atan2(originalSunDirection.x, originalSunDirection.z) * Mathf.Rad2Deg;
            Vector3 forward = Quaternion.Euler(0, creditYaw, 0) * Vector3.forward;
            // A maximum-size moon can need a slightly higher crane to remain
            // above the deck and completely in front of the physical sun. Solve
            // those actual sphere-envelope constraints before choosing the view.
            float baseHeight = Mathf.Max(5.2f, LockedCameraPosition.y + 3.2f);
            float height = baseHeight;
            RaisedCameraPosition = LockedCameraPosition;
            for (int attempt = 0; attempt < 80; attempt++)
            {
                float back = 2.8f + (height - baseHeight) * .5f;
                RaisedCameraPosition = LockedCameraPosition - forward * back;
                RaisedCameraPosition = new Vector3(RaisedCameraPosition.x, height, RaisedCameraPosition.z);
                Vector3 targetRay = EclipseFinaleGame.CinematicSunPosition - RaisedCameraPosition;
                float distance = targetRay.magnitude;
                float maximumDepth = distance - EclipseFinaleGame.SunRadius - LockedMoonOuterRadius - 1;
                float minimumFloorDepth = Mathf.Max(0, (LockedMoonOuterRadius + .02f - height) /
                    Mathf.Max(.001f, targetRay.normalized.y));
                float minimumFrameDepth = LockedMoonOuterRadius / Mathf.Sin(MaximumCinematicMoonRadiusDegrees * Mathf.Deg2Rad);
                if (maximumDepth > Mathf.Max(minimumFloorDepth, minimumFrameDepth) + .10f) break;
                height += .25f;
            }
            Vector3 sun = EclipseFinaleGame.CinematicSunPosition - RaisedCameraPosition;
            float elevation = Mathf.Asin(sun.normalized.y) * Mathf.Rad2Deg;
            float finalSunAngle = Mathf.Asin(Mathf.Clamp01(EclipseFinaleGame.SunRadius / sun.magnitude));
            PresentedMoonDistance = MoonDistanceForSunAngle(finalSunAngle, RaisedCameraPosition, sun);
            CinematicMoonAngularRadiusDegrees = Mathf.Asin(Mathf.Clamp01(LockedMoonOuterRadius / PresentedMoonDistance)) * Mathf.Rad2Deg;
            CinematicMoonSunAngularRatio = Mathf.Asin(Mathf.Clamp01(game.MoonRadius * .97f / PresentedMoonDistance)) /
                Mathf.Max(.00001f, finalSunAngle);
            CinematicReframedMoon = Mathf.Abs(CinematicMoonSunAngularRatio - LockedMoonSunAngularRatio) > .0001f;
            CreditPitchDegrees = Mathf.Clamp(LockedFieldOfView * .5f - elevation - CinematicMoonAngularRadiusDegrees - 2, -8, 14);
            creditRotation = Quaternion.Euler(CreditPitchDegrees, creditYaw, 0);
            Vector3 floor = game.body.transform.position; floor.y = .035f;
            floorRoot.SetPositionAndRotation(floor, Quaternion.Euler(0, creditYaw, 0));
            Section = EndingCreditSection.Intro; Elapsed = SectionElapsed = 0; AppliedSpeed = 1;
            Render();
            return true;
        }

        public void TickCredits(float dt, bool spaceHeld, bool escapePressed)
        {
            if (!Active || game == null || game.paused) return;
            dt = Mathf.Clamp(dt, 0, .25f);
            float rollSpeed = Section == EndingCreditSection.Team || Section == EndingCreditSection.Assets ? NormalRollSpeed : 1;
            AppliedSpeed = rollSpeed * (CanControlCredits && spaceHeld ? FastForwardMultiplier : 1);
            Elapsed += dt;
            if (CanControlCredits && escapePressed)
            {
                EnterThanks(); ApplyCinematicPose(); game.UpdateEclipse(dt); Render(); return;
            }
            SectionElapsed += dt * AppliedSpeed;
            ApplyCinematicPose();
            game.UpdateEclipse(dt);
            switch (Section)
            {
                case EndingCreditSection.Intro:
                    if (SectionElapsed >= IntroDuration) Enter(EndingCreditSection.Team);
                    break;
                case EndingCreditSection.Team:
                    Present(teamCards, SectionElapsed, true);
                    if (SectionElapsed >= TeamDuration && seenTeam.Count == 4)
                    { TeamComplete = true; Enter(EndingCreditSection.Assets); }
                    break;
                case EndingCreditSection.Assets:
                    if (SectionElapsed >= AssetsDuration) EnterThanks();
                    break;
                case EndingCreditSection.Thanks:
                    if (SectionElapsed >= ThanksDuration)
                    {
                        ThanksComplete = true;
                        returnRotation = Quaternion.LookRotation(game.exitSign.TransformPoint(new Vector3(0, .55f, 0)) - LockedCameraPosition);
                        Enter(EndingCreditSection.Return);
                    }
                    break;
                case EndingCreditSection.Return:
                    if (SectionElapsed >= ReturnDuration)
                    {
                        game.world.SetExitRouteReturnProgress(1);
                        Section = EndingCreditSection.Finished; HideRows();
                        game.world.SetSunPresentationPosition(LockedSunPosition);
                        game.view.transform.position = LockedCameraPosition;
                        game.view.fieldOfView = LockedFieldOfView;
                        game.moon.SetPositionAndRotation(GameplayMoonPosition, GameplayMoonRotation);
                        game.moon.localScale = GameplayMoonScale; CameraLiftProgress = 0;
                        game.SetEndingRotation(returnRotation);
                        game.UpdateEclipse(0);
                        game.CompleteCredits(); return;
                    }
                    break;
            }
            Render();
        }

        void Enter(EndingCreditSection section)
        {
            Section = section; SectionElapsed = 0;
            AppliedSpeed = section == EndingCreditSection.Team || section == EndingCreditSection.Assets ? NormalRollSpeed : 1;
            HideRows();
        }
        void EnterThanks()
        {
            if (!TeamComplete) return;
            Enter(EndingCreditSection.Thanks);
        }
        void ApplyCinematicPose()
        {
            float lift = 1;
            Quaternion rotation = creditRotation;
            if (Section == EndingCreditSection.Intro)
            {
                lift = Mathf.SmoothStep(0, 1, Mathf.Clamp01((SectionElapsed - 1.2f) / 3.5f));
                rotation = Quaternion.Slerp(startRotation, creditRotation, lift);
            }
            else if (Section == EndingCreditSection.Return)
            {
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(SectionElapsed / ReturnDuration));
                game.world.SetExitRouteReturnProgress(t);
                lift = 1 - t; rotation = Quaternion.Slerp(creditRotation, returnRotation, t);
            }
            CameraLiftProgress = lift;
            Vector3 eye = Vector3.Lerp(LockedCameraPosition, RaisedCameraPosition, lift);
            game.view.transform.position = eye; game.view.fieldOfView = LockedFieldOfView;
            game.SetEndingRotation(rotation);
            // Bring the eclipse into the readable floor-credit composition.
            // As the visual sun moves, preserve the same lunar/solar angular
            // ratio rather than keeping a distance that would expose a crescent.
            game.world.SetSunPresentationPosition(Vector3.Lerp(LockedSunPosition, EclipseFinaleGame.CinematicSunPosition, lift));
            Vector3 sunRay = EclipseFinaleGame.SunPosition - eye;
            float sunAngle = Mathf.Asin(Mathf.Clamp01(EclipseFinaleGame.SunRadius / sunRay.magnitude));
            float targetDistance = MoonDistanceForSunAngle(sunAngle, eye, sunRay);
            Vector3 currentSunDirection = sunRay.normalized;
            Quaternion parallax = Quaternion.FromToRotation(originalSunDirection, currentSunDirection);
            Vector3 originalMoonRay = (Section == EndingCreditSection.Return ? GameplayMoonPosition : LockedMoonPosition) - LockedCameraPosition;
            float centreBlend = Mathf.SmoothStep(0, 1, Mathf.Clamp01(lift * 2));
            Vector3 direction = Vector3.Slerp(parallax * originalMoonRay.normalized, currentSunDirection, centreBlend).normalized;
            float distance = Mathf.Lerp(originalMoonRay.magnitude, targetDistance, lift);
            Vector3 moonPosition = ConstrainPresentationPosition(eye + direction * distance, eye, sunRay);
            game.moon.position = moonPosition;
            game.moon.rotation = LockedMoonRotation; game.moon.localScale = LockedMoonScale;
            PresentedMoonDistance = Vector3.Distance(moonPosition, eye);
            PresentedMoonSunAngularRatio = Mathf.Asin(Mathf.Clamp01(game.MoonRadius * .97f / PresentedMoonDistance)) / Mathf.Max(.00001f, sunAngle);
            PresentedMoonSurfaceClearance = PresentedMoonDistance - LockedMoonOuterRadius;
            PresentedMoonFloorClearance = moonPosition.y - LockedMoonOuterRadius;
            PresentedSunSurfaceGap = Vector3.Distance(EclipseFinaleGame.SunPosition, moonPosition) - EclipseFinaleGame.SunRadius - LockedMoonOuterRadius;
        }
        float MoonDistanceForSunAngle(float sunAngle, Vector3 eye, Vector3 sunRay)
        {
            // Keep ordinary accepted ratios where feasible, but frame oversized
            // moons and close an accepted annular sliver for the cinematic only.
            // Physical mesh/scale and the exact gameplay pose are never changed.
            float maximumEffectiveAngle = Mathf.Asin(.97f * game.MoonRadius / LockedMoonOuterRadius *
                Mathf.Sin(MaximumCinematicMoonRadiusDegrees * Mathf.Deg2Rad));
            float effectiveAngle = Mathf.Clamp(sunAngle * LockedMoonSunAngularRatio,
                sunAngle * 1.05f, maximumEffectiveAngle);
            float distance = game.MoonRadius * .97f / Mathf.Max(.00001f, Mathf.Sin(effectiveAngle));
            float floorDistance = Mathf.Max(0, (LockedMoonOuterRadius + .02f - eye.y) / Mathf.Max(.001f, sunRay.normalized.y));
            float outsideDistance = LockedMoonOuterRadius + Mathf.Max(.15f, game.view.nearClipPlane + .10f);
            float maximumDistance = sunRay.magnitude - EclipseFinaleGame.SunRadius - LockedMoonOuterRadius - 1;
            return Mathf.Min(Mathf.Max(distance, Mathf.Max(floorDistance, outsideDistance)), maximumDistance);
        }
        Vector3 ConstrainPresentationPosition(Vector3 at, Vector3 eye, Vector3 sunRay)
        {
            Vector3 axis = sunRay.normalized;
            Vector3 horizontal = Vector3.ProjectOnPlane(axis, Vector3.up);
            float horizontalLength = horizontal.magnitude;
            if (horizontalLength > .001f) horizontal /= horizontalLength;
            float maximumDepth = sunRay.magnitude - EclipseFinaleGame.SunRadius - LockedMoonOuterRadius - 1;
            float outsideDistance = LockedMoonOuterRadius + Mathf.Max(.15f, game.view.nearClipPlane + .10f);
            for (int i = 0; i < 3; i++)
            {
                at.y = Mathf.Max(at.y, LockedMoonOuterRadius + .02f);
                float excess = Vector3.Dot(at - eye, axis) - maximumDepth;
                if (excess > 0 && horizontalLength > .001f) at -= horizontal * (excess / horizontalLength);
                Vector3 delta = at - eye;
                if (delta.magnitude < outsideDistance) at = eye + delta.normalized * outsideDistance;
            }
            return at;
        }
        public static bool IntersectsGameplayRoute(Vector3 centre, float radius)
        {
            // Standing and held-jump headroom through the playable central lane.
            Vector3 closest = new Vector3(Mathf.Clamp(centre.x,-4,4),
                Mathf.Clamp(centre.y,0,3.2f),Mathf.Clamp(centre.z,7,72));
            float padded = radius + .15f;
            return (centre-closest).sqrMagnitude <= padded*padded;
        }
        void PrepareGameplayMoonPose()
        {
            GameplayMoonPosition = LockedMoonPosition;
            GameplayMoonRotation = LockedMoonRotation; GameplayMoonScale = LockedMoonScale;
            AdjustedForClearRoute = IntersectsGameplayRoute(LockedMoonPosition,LockedMoonOuterRadius);
            if (!AdjustedForClearRoute) return;
            Vector3 eye=LockedCameraPosition, ray=LockedSunPosition-eye;
            float solarAngle=Mathf.Asin(Mathf.Clamp01(EclipseFinaleGame.SunRadius/ray.magnitude));
            float minimum=LockedMoonOuterRadius+Mathf.Max(.15f,game.view.nearClipPlane+.10f);
            float maximum=ray.magnitude-EclipseFinaleGame.SunRadius-LockedMoonOuterRadius-1;
            float desired=Mathf.Clamp(game.MoonRadius*.97f/Mathf.Sin(solarAngle*1.08f),minimum,maximum);
            Vector3 candidate=ConstrainPresentationPosition(eye+ray.normalized*desired,eye,ray);
            if (SkyPoseClearsRouteAndSun(candidate,eye,ray)) { GameplayMoonPosition=candidate; return; }
            // Most moons clear the lane in the distant sky. A physically small
            // close moon can instead stay before the lane while covering the Sun.
            for(int i=1;i<=64;i++)
            {
                candidate=ConstrainPresentationPosition(eye+ray.normalized*Mathf.Lerp(desired,maximum,i/64f),eye,ray);
                if(SkyPoseClearsRouteAndSun(candidate,eye,ray)){GameplayMoonPosition=candidate;return;}
            }
            for(int i=1;i<=64;i++)
            {
                candidate=ConstrainPresentationPosition(eye+ray.normalized*Mathf.Lerp(desired,minimum,i/64f),eye,ray);
                if(SkyPoseClearsRouteAndSun(candidate,eye,ray)){GameplayMoonPosition=candidate;return;}
            }
            // Geometry remains the priority if an unusual future scale/observer
            // cannot fit both conditions. Gameplay's raw successful snapshot is
            // independent of this display transform and remains untouched.
            GameplayMoonPosition=ConstrainPresentationPosition(eye+ray.normalized*maximum,eye,ray);
        }
        bool SkyPoseClearsRouteAndSun(Vector3 at,Vector3 eye,Vector3 sunRay)
        {
            if(IntersectsGameplayRoute(at,LockedMoonOuterRadius))return false;
            Vector3 moonRay=at-eye;
            float lunar=Mathf.Asin(Mathf.Clamp01(game.MoonRadius*.97f/moonRay.magnitude));
            float solar=Mathf.Asin(Mathf.Clamp01(EclipseFinaleGame.SunRadius/sunRay.magnitude));
            return lunar >= solar+Vector3.Angle(moonRay,sunRay)*Mathf.Deg2Rad;
        }
        void HideRows()
        {
            foreach (var row in rows) row.text.gameObject.SetActive(false);
            VisibleRows = Array.Empty<string>();
        }
        void Render()
        {
            HideRows(); visible.Clear();
            if (Section == EndingCreditSection.Intro && SectionElapsed > 4.2f)
            {
                float alpha = Mathf.SmoothStep(0, 1, (SectionElapsed - 4.2f) / 1.2f);
                Show(rows[0], "PARALLAX\n세종대학교 SW기초설계 프로젝트\n3조 Parallax", 5f, 6.8f, 2.1f, alpha, true);
            }
            else if (Section == EndingCreditSection.Team) Present(teamCards, SectionElapsed, true);
            else if (Section == EndingCreditSection.Assets) Present(assetCards, SectionElapsed, false);
            else if (Section == EndingCreditSection.Thanks)
            {
                float alpha = Mathf.SmoothStep(0, 1, SectionElapsed / 1.3f) *
                    (1 - Mathf.SmoothStep(0, 1, (SectionElapsed - ThanksDuration + 1.2f) / 1.2f));
                Show(rows[0], "THANK YOU\nFOR PLAYING", 5f, 6.3f, 2.4f, alpha, true);
            }
            VisibleRows = visible.ToArray();
        }
        void Present(List<Card> cards, float time, bool team)
        {
            int rowIndex = 0;
            for (int i = cards.Count - 1; i >= 0 && rowIndex < rows.Length; i--)
            {
                var card = cards[i]; float age = time - card.at;
                if (age < 0 || age > 17) continue;
                float z = 3.8f + age * ScrollSpeed;
                float alpha = Mathf.SmoothStep(0, 1, age / 1.2f) * (1 - Mathf.SmoothStep(0, 1, (age - 10) / 6));
                if (alpha <= .002f) continue;
                Show(rows[rowIndex++], card.text, z, card.width, card.depth, alpha, false);
                if (team && age >= 2 && alpha > .7f && !seenTeam.Contains(card.text)) seenTeam.Add(card.text);
            }
        }
        void Show(Row row, string value, float z, float width, float depth, float opacity, bool title)
        {
            float textScale = Mathf.Clamp(GameShellSettingsRuntime.TextScale, .8f, MaximumTextScale);
            width = Mathf.Min(8.4f, width * textScale); depth *= textScale;
            row.text.gameObject.SetActive(true);
            var font = title ? titleFont : bodyFont;
            row.text.font = font;
            row.renderer.sharedMaterial = title ? titleMaterial : bodyMaterial;
            if (row.cached != value || !Mathf.Approximately(row.width, width) || !Mathf.Approximately(row.depth, depth))
            {
                row.cached = value; row.width = width; row.depth = depth;
                font.RequestCharactersInTexture(value, 80, FontStyle.Normal);
                row.text.text = value;
                row.text.transform.localScale = Vector3.one;
                Bounds bounds = row.renderer.localBounds;
                float x = Mathf.Max(.001f, bounds.size.x), y = Mathf.Max(.001f, bounds.size.y);
                row.text.transform.localScale = new Vector3(width / x, depth / y, 1);
            }
            row.text.transform.localPosition = new Vector3(0, .008f, z);
            row.text.color = new Color(ivory.r, ivory.g, ivory.b, opacity);
            if (opacity > .08f) visible.Add(value);
            RefreshFontTextures();
        }
        void OnDestroy()
        {
            Font.textureRebuilt -= FontRebuilt;
            if (bodyMaterial != null) Destroy(bodyMaterial);
            if (titleMaterial != null) Destroy(titleMaterial);
        }
    }
}
