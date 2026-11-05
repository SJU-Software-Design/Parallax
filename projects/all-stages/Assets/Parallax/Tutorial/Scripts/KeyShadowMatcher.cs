using ShadeLink.Gallery;
using UnityEngine;
using UnityEngine.Events;

namespace ShadeLink.Tutorial
{
    /// <summary>
    /// Searches the white wall paper for the live shadow silhouette of a key.
    /// The blue mark stays as a visual hint; it does not define the required position.
    /// </summary>
    public sealed class KeyShadowMatcher : MonoBehaviour
    {
        [SerializeField] Light source;
        [SerializeField] Transform receiver;
        [SerializeField] Transform[] casters;
        [SerializeField, Range(12, 64)] int sampleColumns = 36;
        [SerializeField, Range(8, 32)] int sampleRows = 18;
        [SerializeField, Range(.5f, 1f)] float requiredCoverage = .65f;
        [SerializeField] UnityEvent onMatched = new UnityEvent();

        const int SearchGridResolution = 56;
        const float PaperHalfWidth = 2.325f;
        const float PaperHalfHeight = 2.325f;
        const float MinimumKeyScale = .65f;
        const float MaximumKeyScale = 1.25f;
        const float KeyScaleStep = .10f;
        const float SearchPositionStep = .22f;
        const int RotationSteps = 24;
        const float HalfWidth = 2.25f;
        const float HalfHeight = 1.10f;
        const float KeyMinX = -1.82f;
        const float KeyMaxX = 1.80f;
        const float KeyMinZ = -.60f;
        const float KeyMaxZ = .86f;
        const float SurfaceOffset = .035f;
        const float ChargeDuration = .3f;
        const float MinimumCasterContribution = .10f;
        readonly RaycastHit[] rayHits = new RaycastHit[32];
        readonly bool[] litSamples = new bool[SearchGridResolution * SearchGridResolution];
        readonly bool[] shadowSamples = new bool[SearchGridResolution * SearchGridResolution];
        readonly byte[] shadowCasterSamples = new byte[SearchGridResolution * SearchGridResolution];
        Vector2[] templatePoints;
        bool[] templateShape;
        int cachedSampleColumns;
        int cachedSampleRows;
        int expectedTemplateSamples;
        float nextSampleTime;
        float matchingTime;
        bool[] insideLightVolume;
        Vector3[] startingPositions;
        Quaternion[] startingRotations;
        Vector3[] startingScales;
        bool[] adjustedByPlayer;
        bool reported;

        public float Coverage { get; private set; }
        public float TableContribution { get; private set; }
        public float DonutContribution { get; private set; }
        public int TableShadowSamples { get; private set; }
        public int DonutShadowSamples { get; private set; }
        public bool TableReady => casters != null && casters.Length > 0 && casters[0] != null && IsCasterReady(casters[0]);
        public bool DonutReady => casters != null && casters.Length > 1 && casters[1] != null && IsCasterReady(casters[1]);
        public bool BothPropsAdjusted => adjustedByPlayer != null && adjustedByPlayer.Length >= 2 && adjustedByPlayer[0] && adjustedByPlayer[1];
        public bool Matching { get; private set; }
        public bool Matched { get; private set; }
        public Vector3 MatchedPosition { get; private set; }

        public void Configure(Light keyLight, Transform wallTarget, params Transform[] shadowCasters)
        {
            source = keyLight;
            receiver = wallTarget;
            casters = shadowCasters;
        }

        void Start()
        {
            if (casters == null) return;
            insideLightVolume = new bool[casters.Length];
            startingPositions = new Vector3[casters.Length];
            startingRotations = new Quaternion[casters.Length];
            startingScales = new Vector3[casters.Length];
            adjustedByPlayer = new bool[casters.Length];
            for (int i = 0; i < casters.Length; i++)
            {
                Transform caster = casters[i];
                if (caster == null) continue;
                startingPositions[i] = caster.position;
                startingRotations[i] = caster.rotation;
                startingScales[i] = caster.lossyScale;
                var prop = caster.GetComponent<GalleryProp>();
                if (prop == null) continue;

                prop.InitializePhysics();
                GalleryProp capturedProp = prop;
                prop.placement.WaterSupport = () => capturedProp != null &&
                    capturedProp.gameObject.activeInHierarchy && InSpotlight(capturedProp.transform.position);
                insideLightVolume[i] = InSpotlight(caster.position);
            }
        }

        void FixedUpdate()
        {
            if (source == null || casters == null || insideLightVolume == null) return;
            for (int i = 0; i < casters.Length; i++)
            {
                Transform caster = casters[i];
                if (caster == null || !caster.gameObject.activeInHierarchy) continue;
                var prop = caster.GetComponent<GalleryProp>();
                if (prop == null || prop.body == null) continue;

                bool inLightVolume = InSpotlight(caster.position);
                if (prop.body.isKinematic)
                {
                    insideLightVolume[i] = inLightVolume;
                    continue;
                }

                prop.body.useGravity = !inLightVolume;
                if (inLightVolume && !insideLightVolume[i])
                {
                    prop.body.linearVelocity = Vector3.zero;
                    prop.body.angularVelocity = Vector3.zero;
                }
                insideLightVolume[i] = inLightVolume;
            }
        }

        void Update()
        {
            if (source == null || receiver == null || casters == null || casters.Length < 2 ||
                casters[0] == null || casters[1] == null ||
                Time.time < nextSampleTime) return;

            nextSampleTime = Time.time + .1f;
            SamplePaperShadows();
            UpdatePlayerAdjustments();
            if (!BothPropsAdjusted)
            {
                Matching = false;
                matchingTime = 0;
                Coverage = TableContribution = DonutContribution = 0;
                return;
            }
            Evaluate();
            matchingTime = Matching ? matchingTime + .1f : 0;
            if (!reported && matchingTime >= ChargeDuration)
            {
                reported = Matched = true;
                Debug.Log("Tutorial key-shaped shadow found on the wall paper.", this);
                onMatched.Invoke();
            }
        }

        public void ResetMatch()
        {
            matchingTime = 0;
            reported = Matched = Matching = false;
            Coverage = 0;
            TableContribution = DonutContribution = 0;
            MatchedPosition = Vector3.zero;
            if (casters == null || adjustedByPlayer == null) return;
            for (int i = 0; i < casters.Length; i++)
            {
                Transform caster = casters[i];
                if (caster == null) continue;
                startingPositions[i] = caster.position;
                startingRotations[i] = caster.rotation;
                startingScales[i] = caster.lossyScale;
                adjustedByPlayer[i] = false;
            }
        }

        void UpdatePlayerAdjustments()
        {
            if (casters == null || adjustedByPlayer == null) return;
            for (int i = 0; i < casters.Length; i++)
            {
                Transform caster = casters[i];
                if (caster == null) continue;
                adjustedByPlayer[i] = Vector3.Distance(caster.position, startingPositions[i]) > .08f ||
                    Quaternion.Angle(caster.rotation, startingRotations[i]) > 4f ||
                    Vector3.Distance(caster.lossyScale, startingScales[i]) > .04f;
            }
        }

        void Evaluate()
        {
            Matching = false;
            Coverage = 0;
            TableContribution = DonutContribution = 0;
            EnsureTemplate();

            float bestCoverage = -1;
            float bestBalancedContribution = -1;
            int scaleSteps = Mathf.RoundToInt((MaximumKeyScale - MinimumKeyScale) / KeyScaleStep);
            for (int scaleIndex = 0; scaleIndex <= scaleSteps; scaleIndex++)
            {
                float scale = MinimumKeyScale + scaleIndex * KeyScaleStep;
                for (int rotationIndex = 0; rotationIndex < RotationSteps; rotationIndex++)
                {
                    float angle = rotationIndex * (360f / RotationSteps) * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                    GetRotatedKeyBounds(cos, sin, scale,
                        out float minX, out float maxX, out float minZ, out float maxZ);

                    float minCenterX = -PaperHalfWidth - minX;
                    float maxCenterX = PaperHalfWidth - maxX;
                    float minCenterZ = -PaperHalfHeight - minZ;
                    float maxCenterZ = PaperHalfHeight - maxZ;
                    int xSteps = Mathf.CeilToInt((maxCenterX - minCenterX) / SearchPositionStep);
                    int zSteps = Mathf.CeilToInt((maxCenterZ - minCenterZ) / SearchPositionStep);
                    for (int xStep = 0; xStep <= xSteps; xStep++)
                    for (int zStep = 0; zStep <= zSteps; zStep++)
                    {
                        float centerX = Mathf.Min(minCenterX + xStep * SearchPositionStep, maxCenterX);
                        float centerZ = Mathf.Min(minCenterZ + zStep * SearchPositionStep, maxCenterZ);
                        bool matched = EvaluateCandidate(centerX, centerZ, scale, cos, sin,
                            out float coverage, out float tableContribution, out float donutContribution);
                        float balancedContribution = Mathf.Min(tableContribution, donutContribution);
                        if (coverage > bestCoverage + .0001f ||
                            (Mathf.Abs(coverage - bestCoverage) <= .0001f && balancedContribution > bestBalancedContribution))
                        {
                            bestCoverage = coverage;
                            bestBalancedContribution = balancedContribution;
                            Coverage = coverage;
                            TableContribution = tableContribution;
                            DonutContribution = donutContribution;
                        }
                        if (!matched) continue;

                        Coverage = coverage;
                        Matching = true;
                        float scaleX = Mathf.Max(.0001f, receiver.lossyScale.x);
                        float scaleZ = Mathf.Max(.0001f, receiver.lossyScale.z);
                        MatchedPosition = receiver.TransformPoint(new Vector3(
                            centerX / scaleX, .001f, centerZ / scaleZ));
                        return;
                    }
                }
            }
        }

        void EnsureTemplate()
        {
            int count = sampleColumns * sampleRows;
            if (templatePoints != null && templatePoints.Length == count &&
                cachedSampleColumns == sampleColumns && cachedSampleRows == sampleRows) return;

            templatePoints = new Vector2[count];
            templateShape = new bool[count];
            cachedSampleColumns = sampleColumns;
            cachedSampleRows = sampleRows;
            expectedTemplateSamples = 0;
            int index = 0;
            for (int y = 0; y < sampleRows; y++)
            for (int x = 0; x < sampleColumns; x++)
            {
                float px = Mathf.Lerp(-HalfWidth, HalfWidth, (x + .5f) / sampleColumns);
                float pz = Mathf.Lerp(-HalfHeight, HalfHeight, (y + .5f) / sampleRows);
                bool inside = InKeyShape(px, pz);
                templatePoints[index] = new Vector2(px, pz);
                templateShape[index] = inside;
                if (inside) expectedTemplateSamples++;
                index++;
            }
        }

        void SamplePaperShadows()
        {
            Vector3 normal = receiver.up;
            float scaleX = Mathf.Max(.0001f, receiver.lossyScale.x);
            float scaleZ = Mathf.Max(.0001f, receiver.lossyScale.z);
            TableShadowSamples = DonutShadowSamples = 0;
            for (int row = 0; row < SearchGridResolution; row++)
            for (int column = 0; column < SearchGridResolution; column++)
            {
                int index = row * SearchGridResolution + column;
                float x = Mathf.Lerp(-PaperHalfWidth, PaperHalfWidth, (column + .5f) / SearchGridResolution);
                float z = Mathf.Lerp(-PaperHalfHeight, PaperHalfHeight, (row + .5f) / SearchGridResolution);
                Vector3 point = receiver.TransformPoint(new Vector3(x / scaleX, .001f, z / scaleZ));
                litSamples[index] = InSpotlight(point);
                shadowSamples[index] = false;
                shadowCasterSamples[index] = 0;
                if (litSamples[index] && HasPuzzleShadow(point, normal, out Transform caster))
                {
                    shadowSamples[index] = true;
                    if (IsChildOf(caster, casters[0]))
                    {
                        shadowCasterSamples[index] = 1;
                        TableShadowSamples++;
                    }
                    else if (IsChildOf(caster, casters[1]))
                    {
                        shadowCasterSamples[index] = 2;
                        DonutShadowSamples++;
                    }
                }
            }
        }

        bool EvaluateCandidate(float centerX, float centerZ, float scale, float cos, float sin,
            out float coverage, out float tableContribution, out float donutContribution)
        {
            int covered = 0;
            int tableCovered = 0;
            int donutCovered = 0;

            for (int i = 0; i < templatePoints.Length; i++)
            {
                if (!templateShape[i]) continue;
                Vector2 sample = templatePoints[i];
                float x = centerX + scale * (sample.x * cos - sample.y * sin);
                float z = centerZ + scale * (sample.x * sin + sample.y * cos);
                if (!TryGetPaperSample(x, z, out int mapIndex) || !litSamples[mapIndex] ||
                    !shadowSamples[mapIndex]) continue;

                covered++;
                if (shadowCasterSamples[mapIndex] == 1) tableCovered++;
                else if (shadowCasterSamples[mapIndex] == 2) donutCovered++;
            }

            coverage = expectedTemplateSamples == 0 ? 0 : (float)covered / expectedTemplateSamples;
            tableContribution = expectedTemplateSamples == 0 ? 0 : (float)tableCovered / expectedTemplateSamples;
            donutContribution = expectedTemplateSamples == 0 ? 0 : (float)donutCovered / expectedTemplateSamples;
            return expectedTemplateSamples > 0 && coverage >= requiredCoverage &&
                tableContribution >= MinimumCasterContribution &&
                donutContribution >= MinimumCasterContribution;
        }

        static bool TryGetPaperSample(float x, float z, out int index)
        {
            if (x < -PaperHalfWidth || x >= PaperHalfWidth ||
                z < -PaperHalfHeight || z >= PaperHalfHeight)
            {
                index = -1;
                return false;
            }

            int column = Mathf.Clamp((int)((x + PaperHalfWidth) / (2 * PaperHalfWidth) * SearchGridResolution),
                0, SearchGridResolution - 1);
            int row = Mathf.Clamp((int)((z + PaperHalfHeight) / (2 * PaperHalfHeight) * SearchGridResolution),
                0, SearchGridResolution - 1);
            index = row * SearchGridResolution + column;
            return true;
        }

        static void GetRotatedKeyBounds(float cos, float sin, float scale,
            out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = minZ = float.PositiveInfinity;
            maxX = maxZ = float.NegativeInfinity;
            IncludeRotatedCorner(KeyMinX, KeyMinZ, cos, sin, scale, ref minX, ref maxX, ref minZ, ref maxZ);
            IncludeRotatedCorner(KeyMinX, KeyMaxZ, cos, sin, scale, ref minX, ref maxX, ref minZ, ref maxZ);
            IncludeRotatedCorner(KeyMaxX, KeyMinZ, cos, sin, scale, ref minX, ref maxX, ref minZ, ref maxZ);
            IncludeRotatedCorner(KeyMaxX, KeyMaxZ, cos, sin, scale, ref minX, ref maxX, ref minZ, ref maxZ);
        }

        static void IncludeRotatedCorner(float x, float z, float cos, float sin, float scale,
            ref float minX, ref float maxX, ref float minZ, ref float maxZ)
        {
            float rotatedX = scale * (x * cos - z * sin);
            float rotatedZ = scale * (x * sin + z * cos);
            minX = Mathf.Min(minX, rotatedX);
            maxX = Mathf.Max(maxX, rotatedX);
            minZ = Mathf.Min(minZ, rotatedZ);
            maxZ = Mathf.Max(maxZ, rotatedZ);
        }

        bool InSpotlight(Vector3 point)
        {
            Vector3 fromLight = point - source.transform.position;
            float distance = fromLight.magnitude;
            if (distance >= source.range || distance <= .001f) return false;
            if (source.type != LightType.Spot) return true;
            return Vector3.Angle(source.transform.forward, fromLight / distance) <= source.spotAngle * .5f;
        }

        bool HasPuzzleShadow(Vector3 surface, Vector3 normal, out Transform caster)
        {
            caster = null;
            Vector3 toLight = source.transform.position - surface;
            float distance = toLight.magnitude;
            if (distance <= SurfaceOffset || Vector3.Dot(normal, toLight) <= 0) return false;
            int count = Physics.RaycastNonAlloc(surface + normal * SurfaceOffset, toLight / distance,
                rayHits, distance - SurfaceOffset, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            Transform nearestCaster = null;
            for (int i = 0; i < count; i++)
            {
                Collider hit = rayHits[i].collider;
                Transform hitTransform = hit.transform;
                // The donut keeps a simple box for pickup/placement. Its box is
                // not a valid shadow shape because it would fill the center hole.
                if (casters.Length > 1 && casters[1] != null && IsChildOf(hitTransform, casters[1]) && hit is BoxCollider)
                    continue;

                Transform hitCaster = null;
                int hitCasterIndex = -1;
                for (int casterIndex = 0; casterIndex < casters.Length; casterIndex++)
                {
                    Transform candidate = casters[casterIndex];
                    if (candidate == null || !IsChildOf(hitTransform, candidate)) continue;
                    hitCaster = candidate;
                    hitCasterIndex = casterIndex;
                    break;
                }

                // Held or unsettled props do not cast gameplay shadows. Ignore
                // their colliders so they cannot hide a ready prop behind them.
                if (hitCasterIndex >= 0 && !IsCasterReady(hitCaster)) continue;
                if (rayHits[i].distance >= nearestDistance) continue;

                nearestDistance = rayHits[i].distance;
                nearestCaster = hitCaster;
            }
            caster = nearestCaster;
            return caster != null;
        }

        static bool IsCasterReady(Transform caster)
        {
            var support = caster.GetComponent<ShadeLink.PlacementSupport>();
            return support == null || support.Ready;
        }

        static bool IsChildOf(Transform item, Transform root) => item == root || item.IsChildOf(root);

        static bool InKeyShape(float x, float z)
        {
            const float bowX = 1.02f, bowZ = .08f, outer = .78f, inner = .32f;
            float dx = x - bowX, dz = z - bowZ, radiusSquared = dx * dx + dz * dz;
            bool bow = radiusSquared <= outer * outer && radiusSquared >= inner * inner;
            bool shaft = x >= -1.82f && x <= .53f && z >= -.255f && z <= .255f;
            bool firstTooth = x >= -1.28f && x <= -.98f && z >= -.60f && z < -.255f;
            bool secondTooth = x >= -.37f && x <= -.07f && z >= -.53f && z < -.255f;
            return bow || shaft || firstTooth || secondTooth;
        }
    }
}
