using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Tutorial
{
    public sealed partial class TutorialGame
    {
        void BuildFinishes()
        {
            var shader = Resources.Load<Shader>("Tutorial/BrushedSteel");
            var wallGrain = new Vector4(36f, 5f, 36f, 0f);
            var floorGrain = new Vector4(24f, 18f, 7f, 0f);
            wallSeam = SteelFinish(shader, new Color(.37f, .40f, .41f), .24f, .34f, wallGrain, .5f);
            steel = SteelFinish(shader, new Color(.77f, .77f, .74f), .26f, .44f, wallGrain, .64f);
            paleSteel = SteelFinish(shader, new Color(.80f, .80f, .77f), .26f, .44f, wallGrain, .64f);
            frame = SteelFinish(shader, new Color(.31f, .36f, .40f), .36f, .46f, wallGrain, .48f);
            floorPlate = SteelFinish(shader, new Color(.61f, .62f, .62f), .16f, .31f, floorGrain, .82f);
            paleFloorPlate = SteelFinish(shader, new Color(.64f, .64f, .64f), .16f, .31f, floorGrain, .82f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.75f, .75f, .74f);
            RenderSettings.ambientEquatorColor = new Color(.65f, .65f, .64f);
            RenderSettings.ambientGroundColor = new Color(.49f, .48f, .47f);
        }

        static Material SteelFinish(Shader shader, Color color, float metallic, float smoothness, Vector4 grain, float wear)
        {
            var material = new Material(shader) { color = color };
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            material.SetVector("_GrainScale", grain);
            material.SetFloat("_Wear", wear);
            return material;
        }

        void BuildWallPanels(Vector3 center, Vector3 inward, float span, float wallHeight)
        {
            const float trimHeight = .26f;
            Vector3 along = Vector3.Cross(inward, Vector3.up);
            Vector3 lengthAxis = new Vector3(Mathf.Abs(along.x), 0, Mathf.Abs(along.z));
            Vector3 depthAxis = new Vector3(Mathf.Abs(inward.x), 0, Mathf.Abs(inward.z));
            int count = Mathf.CeilToInt(span / 2.8f);
            float pitch = span / count;
            float height = wallHeight - trimHeight - .04f;
            for (int i = 0; i < count; i++)
            {
                var position = center + along * ((i + .5f) * pitch - span * .5f) + inward * .016f;
                position.y = trimHeight + height * .5f;
                Box("Brushed steel wall panel", position, lengthAxis * (pitch - .038f) + depthAxis * .04f + Vector3.up * height,
                    i % 3 == 1 ? paleSteel : steel, false);
            }
            Box("Steel skirting", center + inward * .031f + Vector3.up * (trimHeight * .5f),
                lengthAxis * span + depthAxis * .08f + Vector3.up * trimHeight, frame, false);
        }

        void BuildFloor(Bounds room, string name)
        {
            Vector3 size = room.size;
            Box(name, new Vector3(room.center.x, -.25f, room.center.z), new Vector3(size.x, .5f, size.z), wallSeam);
            int rows = Mathf.CeilToInt(size.x / 4f), columns = Mathf.CeilToInt(size.z / 4f);
            float dx = size.x / rows, dz = size.z / columns;
            Vector3 min = room.min;
            // Only the continuous slab collides. Dice can settle directly over a seam.
            for (int x = 0; x < rows; x++)
                for (int z = 0; z < columns; z++)
                    Box("Steel floor plate", new Vector3(min.x + (x + .5f) * dx, -.008f, min.z + (z + .5f) * dz),
                        new Vector3(dx - .032f, .024f, dz - .032f), (x + z) % 3 == 1 ? paleFloorPlate : floorPlate, false);
        }

        void BuildRoomLights(Bounds room, Vector3 forward)
        {
            var diffuser = new Material(guideShader) { color = new Color(.86f, .88f, .88f) };
            Vector3 across = Vector3.Cross(forward, Vector3.up);
            float depth = Mathf.Abs(Vector3.Dot(room.size, forward));
            float halfWidth = Mathf.Abs(Vector3.Dot(room.extents, across));
            int rows = Mathf.CeilToInt(depth / 13f);
            float spacing = depth / rows;
            var stripAxis = new Vector3(forward.x, 0, forward.z);
            var stripAcross = new Vector3(Mathf.Abs(across.x), 0, Mathf.Abs(across.z));
            for (int i = 0; i < rows; i++)
            {
                Vector3 center = room.center + forward * ((i + .5f) * spacing - depth * .5f);
                center.y = room.max.y - .09f;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 position = center + across * (side * halfWidth * .48f);
                    Box("Recessed light housing", position, stripAxis * 6.2f + stripAcross * .68f + Vector3.up * .19f, frame, false);
                    Box("Frosted ceiling diffuser", position - Vector3.up * .092f,
                        stripAxis * 5.9f + stripAcross * .46f + Vector3.up * .027f, diffuser, false);
                }
                center.y = room.max.y * .71f;
                var light = RoomLight(center, LightType.Spot, center.y + 13f, 1.65f);
                light.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                light.spotAngle = 112f;
                light.innerSpotAngle = 76f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = .56f;
            }
        }

        Light RoomLight(Vector3 position, LightType type, float range, float intensity)
        {
            var light = new GameObject("Training room light").AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.transform.localPosition = position;
            light.type = type;
            light.range = range;
            light.intensity = intensity;
            light.renderMode = LightRenderMode.ForcePixel;
            light.color = new Color(1f, .99f, .97f);
            return light;
        }
    }
}
