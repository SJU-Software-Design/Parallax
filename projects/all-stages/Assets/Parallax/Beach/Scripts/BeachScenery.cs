using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Beach
{
    // Fixed beach-life clusters sit outside the central puzzle and castle sightline.
    // Their rendered meshes also supply the normal BeachLighting shadow queries.
    public static class BeachScenery
    {
        static Material Palette(string name, Color fallback)
        {
            var material = BeachWorld.Materials.Find(m => m != null && m.name == name);
            if (material != null) return material;
            material = new Material(Shader.Find("Parallax/BeachSurface")) { name = name, color = fallback };
            material.SetFloat("_Glossiness", .16f);
            material.EnableKeyword("_EMISSION");
            BeachWorld.Materials.Add(material);
            return material;
        }

        static GameObject Primitive(string name, PrimitiveType shape, Transform parent,
            Vector3 at, Vector3 size, Material material, bool solid, bool shadow = true)
        {
            var item = GameObject.CreatePrimitive(shape);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = at;
            item.transform.localScale = size;
            var renderer = item.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (!solid) UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            return item;
        }

        static GameObject Model(string key, string name, Transform parent,
            Func<string, GameObject> assets, Vector3 at, float scale, float yaw, bool solid)
        {
            var item = UnityEngine.Object.Instantiate(assets(key));
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = at;
            item.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            item.transform.localScale = Vector3.one * scale;
            foreach (var renderer in item.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
            if (solid)
                foreach (var mesh in item.GetComponentsInChildren<MeshFilter>())
                    mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
            return item;
        }

        static void Sign(Transform parent, Vector3 at, float yaw, string caption,
            Material timber, Material canvas, Color ink)
        {
            var stand = new GameObject("Beach rental sign");
            stand.transform.SetParent(parent, false);
            stand.transform.localPosition = at;
            stand.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            Primitive("Sign board", PrimitiveType.Cube, stand.transform,
                new Vector3(0, 1.05f, 0), new Vector3(1.32f, .96f, .10f), canvas, true);
            foreach (float side in new[] { -1f, 1f })
                Primitive("Sign timber leg", PrimitiveType.Cube, stand.transform,
                    new Vector3(side * .48f, .57f, .10f), new Vector3(.09f, 1.14f, .10f), timber, true);
            var text = new GameObject("Rental lettering");
            text.transform.SetParent(stand.transform, false);
            text.transform.localPosition = new Vector3(0, 1.05f, -.057f);
            var label = text.AddComponent<BeachLabel>();
            label.caption = caption;
            label.size = .18f;
            label.color = ink;
        }

        static void Grass(Transform parent, Vector3 at, Material grass)
        {
            var tuft = new GameObject("Dune grass");
            tuft.transform.SetParent(parent, false);
            tuft.transform.localPosition = at;
            for (int i = 0; i < 4; i++)
            {
                float angle = 43 + i * 91;
                float height = .30f + i * .065f;
                var blade = Primitive("Dune grass blade", PrimitiveType.Cube, tuft.transform,
                    new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * .045f, height * .48f, Mathf.Cos(angle * Mathf.Deg2Rad) * .045f),
                    new Vector3(.025f, height, .045f), grass, false, false);
                blade.transform.localRotation = Quaternion.Euler(15 + i * 3, angle, i % 2 == 0 ? 11 : -11);
            }
        }

        static Transform Cluster(Transform parent, string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        static void Deck(Transform parent, Vector3 at, Vector2 size, Material timber, Material seams)
        {
            Primitive("Low timber deck", PrimitiveType.Cube, parent, at,
                new Vector3(size.x, .08f, size.y), timber, true);
            int count = Mathf.FloorToInt(size.y / .32f);
            for (int i = 1; i < count; i++)
                Primitive("Deck plank seam", PrimitiveType.Cube, parent,
                    at + new Vector3(0, .042f, -size.y * .5f + i * size.y / count),
                    new Vector3(size.x, .004f, .012f), seams, false, false);
        }

        static void Crate(Transform parent, Vector3 at, Vector3 size, Material timber, Material bands)
        {
            Primitive("Harbor supply crate", PrimitiveType.Cube, parent,
                at + Vector3.up * size.y * .5f, size, timber, true);
            foreach (float side in new[] { -1f, 1f })
            {
                Primitive("Crate top band", PrimitiveType.Cube, parent,
                    at + new Vector3(side * size.x * .29f, size.y + .008f, 0),
                    new Vector3(.035f, .016f, size.z + .016f), bands, false, false);
                foreach (float face in new[] { -1f, 1f })
                    Primitive("Crate front band", PrimitiveType.Cube, parent,
                        at + new Vector3(side * size.x * .29f, size.y * .5f, face * (size.z * .5f + .008f)),
                        new Vector3(.035f, size.y, .016f), bands, false, false);
            }
        }

        static void RopeCoil(Transform parent, Vector3 at, Material rope)
        {
            for (int ring = 0; ring < 2; ring++)
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * 30f, radius = .17f + ring * .045f;
                    var segment = Primitive("Coiled mooring rope", PrimitiveType.Cube, parent,
                        at + new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * radius, .012f,
                            Mathf.Cos(angle * Mathf.Deg2Rad) * radius),
                        new Vector3(.026f, .024f, radius * .53f), rope, false, false);
                    segment.transform.localRotation = Quaternion.Euler(0, angle + 90, 0);
                }
        }

        static void Buoy(Transform parent, Vector3 at, Material body, Material stripe)
        {
            Primitive("Stored harbor buoy", PrimitiveType.Sphere, parent,
                at + Vector3.up * .24f, new Vector3(.44f, .48f, .44f), body, false);
            Primitive("Buoy marking", PrimitiveType.Cylinder, parent,
                at + Vector3.up * .25f, new Vector3(.446f, .055f, .446f), stripe, false, false);
            Primitive("Buoy handle", PrimitiveType.Cylinder, parent,
                at + Vector3.up * .54f, new Vector3(.065f, .075f, .065f), stripe, false, false);
        }

        static void FlowerBed(Transform parent, Vector3 at, Material sand, Material grass,
            Material petals, Material centers)
        {
            Primitive("Festival flower bed", PrimitiveType.Cube, parent,
                at + Vector3.up * .10f, new Vector3(.85f, .20f, 1.65f), sand, false, false);
            for (int i = 0; i < 3; i++)
            {
                var center = at + new Vector3(i % 2 == 0 ? -.16f : .16f, .21f, -.48f + i * .48f);
                Grass(parent, center, grass);
                Primitive("Beach flower petals", PrimitiveType.Sphere, parent,
                    center + Vector3.up * .34f, new Vector3(.22f, .10f, .22f), petals, false, false);
                Primitive("Beach flower center", PrimitiveType.Sphere, parent,
                    center + Vector3.up * .395f, new Vector3(.085f, .035f, .085f), centers, false, false);
            }
        }

        static void Bunting(Transform parent, Vector3 at, float width,
            Material timber, Material first, Material second, Material third)
        {
            foreach (float side in new[] { -1f, 1f })
                Primitive("Festival flag pole", PrimitiveType.Cylinder, parent,
                    at + new Vector3(side * width * .5f, 1.2f, 0), new Vector3(.075f, 1.2f, .075f), timber, true);
            Primitive("Festival flag cord", PrimitiveType.Cube, parent,
                at + Vector3.up * 2.32f, new Vector3(width, .018f, .018f), timber, false, false);
            for (int i = 0; i < 7; i++)
            {
                var flag = Primitive("Colorful festival pennant", PrimitiveType.Cube, parent,
                    at + new Vector3(-width * .40f + i * width * .80f / 6, 2.15f, 0),
                    new Vector3(.23f, .30f, .025f), i % 3 == 0 ? first : i % 3 == 1 ? second : third, false, false);
                flag.transform.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? -9 : 9);
            }
        }

        static void BeachActivity(Transform root, Func<string, GameObject> assets,
            Material timber, Material canvas, Material coral, Material navy, Material grass)
        {
            var court = Cluster(root, "02 / beach games before the tidal crossing");
            // The court stays on the first bank. The whole channel at z33..40 stays open.
            foreach (float x in new[] { -12.65f, -8.35f })
                Primitive("Beach court sideline", PrimitiveType.Cube, court,
                    new Vector3(x, .012f, 29.35f), new Vector3(.045f, .018f, 4.7f), canvas, false, false);
            foreach (float z in new[] { 27f, 31.7f })
                Primitive("Beach court end line", PrimitiveType.Cube, court,
                    new Vector3(-10.5f, .012f, z), new Vector3(4.3f, .018f, .045f), canvas, false, false);
            foreach (float z in new[] { 27.75f, 30.95f })
                Primitive("Beach volleyball net post", PrimitiveType.Cylinder, court,
                    new Vector3(-10.5f, .90f, z), new Vector3(.075f, .90f, .075f), coral, true);
            for (int row = 0; row < 5; row++)
                Primitive("Beach net horizontal cord", PrimitiveType.Cube, court,
                    new Vector3(-10.5f, .74f + row * .20f, 29.35f), new Vector3(.018f, .018f, 3.2f), canvas, false, false);
            for (int column = 0; column <= 10; column++)
                Primitive("Beach net vertical cord", PrimitiveType.Cube, court,
                    new Vector3(-10.5f, 1.14f, 27.75f + column * .32f), new Vector3(.018f, .8f, .018f), canvas, false, false);
            Model("volleyball", "Volleyball beside the court", court, assets,
                new Vector3(-9.1f, .02f, 30.5f), 1.5f, 0, false);
            Model("ball", "Colorful beach game ball", court, assets,
                new Vector3(-11.9f, .02f, 28f), .75f, 28, false);
            Model("surfboard", "Beach club surfboard", court, assets,
                new Vector3(-12.4f, .02f, 29.5f), .7f, 28, true);
            Sign(court, new Vector3(-8.8f, .02f, 27.3f), 0,
                "BEACH CLUB\n비치 게임", timber, canvas, new Color(.06f, .24f, .29f));

            var rest = Cluster(root, "02 / far-bank pier picnic");
            Deck(rest, new Vector3(10.75f, .04f, 43.75f), new Vector2(3.5f, 4.4f), timber, navy);
            Model("umbrella", "Pier picnic parasol", rest, assets,
                new Vector3(11.05f, .085f, 44.55f), 1, 20, true);
            Model("chair", "Chair overlooking the tidal channel", rest, assets,
                new Vector3(9.8f, .085f, 43f), 1.2f, -110, true);
            Model("mat", "Pier picnic towel", rest, assets,
                new Vector3(11.3f, .087f, 42.25f), .85f, 8, false);
            Model("cooler", "Pier picnic cooler", rest, assets,
                new Vector3(10.85f, .085f, 42.4f), 1, -18, true);
            Model("driftwood", "Weathered timber beside the pier", rest, assets,
                new Vector3(11.2f, .01f, 46.75f), .65f, 12, true);
            RopeCoil(rest, new Vector3(12f, .085f, 43.2f), canvas);
            Grass(rest, new Vector3(12.8f, .02f, 45.4f), grass);
            Grass(rest, new Vector3(12.75f, .02f, 41.1f), grass);
        }

        static void WorkingHarbor(Transform root, Func<string, GameObject> assets,
            Material timber, Material canvas, Material coral, Material navy, Material gold)
        {
            var source = Cluster(root, "03 / harbor dispatch and cargo sorting");
            Model("hirehut", "Harbor dispatch office", source, assets,
                new Vector3(-10.7f, .02f, 49.7f), .65f, 90, true);
            Deck(source, new Vector3(-10.45f, .04f, 52.25f), new Vector2(4.1f, 2.1f), timber, navy);
            Crate(source, new Vector3(-11.5f, .085f, 52.25f), new Vector3(.85f, .75f, .8f), timber, canvas);
            Crate(source, new Vector3(-10.55f, .085f, 52.45f), new Vector3(.7f, .55f, .65f), timber, navy);
            Crate(source, new Vector3(-11.5f, .835f, 52.25f), new Vector3(.6f, .45f, .58f), timber, coral);
            Model("boardrack", "Surf cargo ready for dispatch", source, assets,
                new Vector3(-8.7f, .02f, 50.25f), .65f, -10, true);
            RopeCoil(source, new Vector3(-9.35f, .085f, 52.45f), canvas);
            Buoy(source, new Vector3(-12f, .085f, 53f), coral, canvas);
            Buoy(source, new Vector3(-9.15f, .085f, 53f), gold, navy);
            Sign(source, new Vector3(-8.55f, .02f, 48.8f), 0,
                "HARBOR\n그림자 조선소", timber, canvas, new Color(.06f, .24f, .29f));
            foreach (float x in new[] { -12.3f, -8.65f })
            {
                Primitive("Mooring bollard", PrimitiveType.Cylinder, source,
                    new Vector3(x, .24f, 54f), new Vector3(.16f, .24f, .16f), navy, true);
                Primitive("Mooring bollard cap", PrimitiveType.Cube, source,
                    new Vector3(x, .48f, 54f), new Vector3(.32f, .07f, .13f), gold, true);
            }

            // Receiving supplies stay low and left, clear of the raised right-hand
            // current walk and too low to become a ladder over the z71.8 water gate.
            var arrival = Cluster(root, "03 / receiving cove supply landing");
            Deck(arrival, new Vector3(-10.7f, .04f, 68.55f), new Vector2(3.5f, 1.9f), timber, navy);
            Crate(arrival, new Vector3(-11.65f, .085f, 68.6f), new Vector3(.75f, .5f, .7f), timber, coral);
            Crate(arrival, new Vector3(-10.8f, .085f, 68.8f), new Vector3(.65f, .42f, .6f), timber, canvas);
            Model("cooler", "Delivered harbor supplies", arrival, assets,
                new Vector3(-9.65f, .085f, 68.5f), .8f, -14, true);
            RopeCoil(arrival, new Vector3(-10.2f, .085f, 68.1f), canvas);
            foreach (float x in new[] { -12.1f, -9.3f })
                Primitive("Receiving cove paint mark", PrimitiveType.Cube, arrival,
                    new Vector3(x, .086f, 67.9f), new Vector3(.14f, .008f, .55f), gold, false, false);
        }

        static void CastleFestival(Transform root, Func<string, GameObject> assets,
            Material sand, Material timber, Material canvas, Material coral, Material navy,
            Material grass, Material gold, Material blue)
        {
            var exhibition = Cluster(root, "04 / sand sculpture festival and shell garden");
            // Side exhibitions frame the real castle. The approach terraces and
            // every castle climbing ledge at z90..101 keep their original space.
            var stall = Cluster(exhibition, "Open sand sculpture festival stall");
            foreach (float x in new[] { -12.55f, -9.65f })
                foreach (float z in new[] { 78.85f, 81.35f })
                    Primitive("Festival stall timber post", PrimitiveType.Cylinder, stall,
                        new Vector3(x, 1.23f, z), new Vector3(.09f, 1.23f, .09f), timber, true);
            Primitive("Festival stall canvas roof", PrimitiveType.Cube, stall,
                new Vector3(-11.1f, 2.48f, 80.1f), new Vector3(3.2f, .12f, 2.8f), canvas, true);
            for (int i = 0; i < 5; i++)
                Primitive("Festival awning stripe", PrimitiveType.Cube, stall,
                    new Vector3(-12.36f + i * .63f, 2.547f, 80.1f), new Vector3(.25f, .014f, 2.8f), coral, false, false);
            Primitive("Festival stall counter", PrimitiveType.Cube, stall,
                new Vector3(-11.1f, .48f, 78.9f), new Vector3(2.65f, .96f, .62f), timber, true);
            Model("sandcastle", "Festival counter display sculpture", stall, assets,
                new Vector3(-11.55f, .965f, 78.9f), .34f, -12, false);
            Model("volleyball", "Round painted festival display", stall, assets,
                new Vector3(-10.35f, .965f, 78.9f), .9f, 0, false);
            Sign(exhibition, new Vector3(-10.6f, .02f, 82.15f), 0,
                "SAND FEST\n모래 조각 축제", timber, canvas, new Color(.06f, .24f, .29f));
            Bunting(exhibition, new Vector3(-11.1f, .02f, 83.7f), 2.6f, timber, coral, canvas, blue);
            foreach (var at in new[] { new Vector3(-11.7f, 0, 85.15f), new Vector3(-10.35f, 0, 87.6f) })
            {
                Primitive("Sand sculpture display plinth", PrimitiveType.Cylinder, exhibition,
                    at + Vector3.up * .09f, new Vector3(1.85f, .09f, 1.85f), sand, true);
                Model("sandcastle", "Beach festival miniature castle", exhibition, assets,
                    at + Vector3.up * .18f, at.z < 86 ? .9f : .7f, at.z < 86 ? 18 : -24, true);
            }
            FlowerBed(exhibition, new Vector3(-12.6f, .02f, 87.6f), sand, grass, coral, gold);
            FlowerBed(exhibition, new Vector3(-9.8f, .02f, 84.6f), sand, grass, canvas, gold);

            var viewing = Cluster(root, "04 / castle viewing picnic and flowers");
            Bunting(viewing, new Vector3(11.85f, .02f, 78.25f), 2.05f, timber, blue, coral, canvas);
            Model("chair", "Castle viewing chair one", viewing, assets,
                new Vector3(11.8f, .02f, 79.6f), 1.1f, -95, true);
            Model("umbrella", "Castle viewing parasol", viewing, assets,
                new Vector3(12f, .02f, 82f), .8f, -18, true);
            Model("chair", "Castle viewing chair two", viewing, assets,
                new Vector3(11.7f, .02f, 83f), 1.15f, -105, true);
            Model("mat", "Castle viewing striped towel", viewing, assets,
                new Vector3(11.85f, .025f, 84.8f), .75f, 70, false);
            Model("cooler", "Castle festival picnic cooler", viewing, assets,
                new Vector3(12.1f, .02f, 80.7f), .85f, 15, true);
            Model("sandcastle", "Garden sand sculpture", viewing, assets,
                new Vector3(11.85f, .02f, 87.1f), .85f, -8, true);
            FlowerBed(viewing, new Vector3(11.1f, .02f, 85.75f), sand, grass, coral, gold);
            FlowerBed(viewing, new Vector3(12.6f, .02f, 88.1f), sand, grass, canvas, gold);
        }

        static void GardenPlanting(Transform root, Material timber, Material grass,
            Material coral, Material canvas, Material gold)
        {
            var garden = Cluster(root, "02 / planted S-shaped beach garden");
            for (int bed = 0; bed < 2; bed++)
            {
                float left = bed == 0 ? -8f : 0f, height = bed == 0 ? 1f : 1.1f;
                float z = bed == 0 ? 42.5f : 46.2f;
                foreach (float side in new[] { -1f, 1f })
                    Primitive("Garden timber edging", PrimitiveType.Cube, garden,
                        new Vector3(left + 4, height + .025f, z + side * .72f),
                        new Vector3(8, .05f, .08f), timber, false, false);
                for (int i = 0; i < 6; i++)
                {
                    var at = new Vector3(left + .7f + i * 1.3f, height, z + (i % 2 == 0 ? -.22f : .22f));
                    Grass(garden, at, grass);
                    Primitive("Beach garden bloom", PrimitiveType.Sphere, garden,
                        at + Vector3.up * .37f, new Vector3(.27f, .10f, .27f),
                        i % 2 == 0 ? coral : canvas, false, false);
                    Primitive("Beach garden flower center", PrimitiveType.Sphere, garden,
                        at + Vector3.up * .425f, new Vector3(.09f, .035f, .09f), gold, false, false);
                }
            }
        }

        public static void Build(Transform root, Func<string, GameObject> assets)
        {
            var scenery = new GameObject("Beach entrance / rental and picnic");
            scenery.transform.SetParent(root, false);
            var parent = scenery.transform;
            var sand = Palette("Sand", new Color(.80f, .69f, .47f));
            var timber = Palette("Weathered timber", new Color(.54f, .32f, .16f));
            var canvas = Palette("Warm canvas", new Color(.95f, .93f, .82f));
            var coral = Palette("Coral marker", new Color(.92f, .28f, .19f));
            var navy = Palette("Deep navy", new Color(.08f, .17f, .22f));
            var grass = Palette("Entry dune grass", new Color(.38f, .48f, .22f));
            var gold = Palette("Signal amber", new Color(1, .67f, .1f));
            var blue = Palette("Lagoon blue", new Color(.04f, .46f, .67f));

            // Rental counter faces the playable beach, to the left of the mirror.
            Primitive("Rental deck", PrimitiveType.Cube, parent,
                new Vector3(-11.1f, .025f, 11.0f), new Vector3(4.15f, .05f, 4.25f), timber, true);
            Model("hirehut", "Surf rental hut", parent, assets,
                new Vector3(-11.15f, .055f, 11.0f), .82f, -90, true);
            Model("boardrack", "Boards ready for hire", parent, assets,
                new Vector3(-10.7f, .018f, 6.45f), .76f, -45, true);
            Sign(parent, new Vector3(-8.95f, .02f, 12.6f), -90,
                "SURF\n서핑보드 대여", timber, canvas, new Color(.06f, .24f, .29f));
            for (int i = 0; i < 7; i++)
                Primitive("Rental approach plank", PrimitiveType.Cube, parent,
                    new Vector3(-10.55f, .024f, 7.45f + i * .23f),
                    new Vector3(2.2f, .048f, .19f), timber, true);

            // An optional rest scene stays beyond the puddle and away from the gate.
            Model("umbrella", "Picnic shade umbrella", parent, assets,
                new Vector3(11.25f, .015f, 16.2f), 1.05f, -25, true);
            Model("chair", "Picnic chair toward the sea", parent, assets,
                new Vector3(10.8f, .015f, 14.7f), 1.15f, 95, true);
            Model("chair", "Picnic companion chair", parent, assets,
                new Vector3(11.6f, .015f, 17.2f), 1.10f, 125, true);
            Model("mat", "Picnic striped towel", parent, assets,
                new Vector3(9.7f, .018f, 16.0f), 1.1f, 12, false);
            Model("cooler", "Picnic cooler", parent, assets,
                new Vector3(10.05f, .018f, 14.25f), 1, -20, true);
            Primitive("Sunscreen bottle", PrimitiveType.Cylinder, parent,
                new Vector3(10.05f, .615f, 14.25f), new Vector3(.075f, .075f, .075f), canvas, false, false);
            Primitive("Sunscreen cap", PrimitiveType.Cylinder, parent,
                new Vector3(10.05f, .70f, 14.25f), new Vector3(.054f, .018f, .054f), coral, false, false);
            Primitive("Picnic sandals left", PrimitiveType.Sphere, parent,
                new Vector3(9.17f, .025f, 16.9f), new Vector3(.15f, .05f, .29f), coral, false, false);
            Primitive("Picnic sandals right", PrimitiveType.Sphere, parent,
                new Vector3(9.38f, .025f, 16.92f), new Vector3(.15f, .05f, .29f), navy, false, false);

            // Low dune details frame the entrance without covering the distant castle.
            foreach (float side in new[] { -1f, 1f })
                foreach (float z in new[] { 3.0f, 7.0f, 17.0f, 21.5f })
                {
                    Primitive("Soft dune bank", PrimitiveType.Sphere, parent,
                        new Vector3(side * 15.15f, -.25f, z), new Vector3(4.3f, 1.05f, 3.0f), sand, false, false);
                    Grass(parent, new Vector3(side * 13.25f, .02f, z + .15f), grass);
                }
            foreach (var at in new[] { new Vector3(12.2f,.035f,5.0f), new Vector3(-12.5f,.035f,18.2f), new Vector3(11.9f,.035f,20.8f) })
                Primitive("Shore shell", PrimitiveType.Sphere, parent, at,
                    new Vector3(.20f,.07f,.15f), canvas, false, false);

            BeachActivity(root, assets, timber, canvas, coral, navy, grass);
            GardenPlanting(root, timber, grass, coral, canvas, gold);
            WorkingHarbor(root, assets, timber, canvas, coral, navy, gold);
            CastleFestival(root, assets, sand, timber, canvas, coral, navy, grass, gold, blue);
        }
    }
}
