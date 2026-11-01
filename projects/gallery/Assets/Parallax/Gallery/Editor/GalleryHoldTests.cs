using System;
using System.Collections.Generic;
using ShadeLink.Gallery;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class GalleryHoldTests
    {
        static void Require(bool condition, string message)
        { if (!condition) throw new Exception("GALLERY_HOLD: " + message); }

        public static void Run()
        {
            var errors = new List<string>();
            Check(errors, "tilted pickup", new Vector3(20, 37, 25), Vector3.one, false);
            Check(errors, "vertical forward pickup", new Vector3(90, 37, 0), Vector3.one, false);
            Check(errors, "upside down pickup", new Vector3(0, 37, 180), Vector3.one, false);
            Check(errors, "nonuniform compound shape", new Vector3(15, 37, 10), new Vector3(1, 1.5f, .7f), false);
            Check(errors, "fallen object on floor", new Vector3(90, 37, 0), Vector3.one, true);
            Check(errors, "upside down floor plinth", new Vector3(0, 37, 180), Vector3.one, true, new Vector3(1.25f, .45f, 1.25f));
            Check(errors, "fallen full size painting", new Vector3(90, 0, 0), Vector3.one, true, new Vector3(2.4f, 2.8f, .2f));
            if (errors.Count > 0) throw new Exception(string.Join("\n", errors));
            Debug.Log("GALLERY_HOLD_TESTS_OK 7 scenarios");
        }

        static void Check(List<string> errors, string name, Vector3 euler, Vector3 scale, bool onFloor, Vector3? size = null)
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var game = GalleryWorld.Build();
                game.active = true;
                game.SyncView();
                var root = new GameObject("Hold fixture");
                var prop = root.AddComponent<GalleryProp>();
                root.transform.SetPositionAndRotation(new Vector3(0, 2.5f, 8), Quaternion.Euler(euler));
                root.transform.localScale = scale;
                var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.transform.SetParent(root.transform, false);
                part.transform.localScale = size ?? new Vector3(.6f, .8f, .5f);
                var detail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                detail.transform.SetParent(root.transform, false);
                detail.transform.localPosition = new Vector3(.38f, .15f, .05f);
                detail.transform.localScale = new Vector3(.25f, .4f, .3f);
                prop.Remember();
                if (onFloor) root.transform.position += Vector3.up * (.008f - prop.WorldBounds.min.y);
                prop.Remember();
                prop.InitializePhysics();
                game.props = new[] { prop };
                game.lighting.Rebuild();
                Physics.SyncTransforms();
                Vector3 position = root.transform.position;
                Quaternion rotation = root.transform.rotation;
                Vector3 offset = detail.transform.localPosition, detailScale = detail.transform.localScale;
                Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
                if (forward.sqrMagnitude < .0001f) forward = Vector3.Cross(rotation * Vector3.right, Vector3.up);
                Grab(game, part.transform.position);
                Require(Vector3.Dot(root.transform.up, Vector3.up) > .99999f, name + ": pickup is still tilted");
                Require(Vector3.Dot(root.transform.forward, forward.normalized) > .99999f, name + ": horizontal facing changed");
                CheckShape(game, scale, offset, detailScale, detail.transform);
                float firstScale = root.transform.localScale.x;
                bool resized=false;
                foreach (float pitch in new[] { -.35f, .25f })
                {
                    game.pitch = pitch; game.SyncView(); game.UpdateHeldPose();
                    Require(Vector3.Dot(root.transform.up, Vector3.up) > .99999f, name + ": camera movement tilts held object");
                    CheckShape(game, scale, offset, detailScale, detail.transform);
                    resized|=Mathf.Abs(firstScale-root.transform.localScale.x)>.01f;
                }
                Require(resized, name + ": fixture did not exercise distance scaling");
                game.Cancel();
                Require(Vector3.Distance(root.transform.position, position) < .0001f &&
                    Quaternion.Angle(root.transform.rotation, rotation) < .01f && root.transform.localScale == scale,
                    name + ": cancel did not restore original pose and proportions");
                Grab(game, part.transform.position);
                position = root.transform.position; rotation = root.transform.rotation; scale = root.transform.localScale;
                Require(game.Drop(), name + ": drop rejected");
                Require(Vector3.Distance(root.transform.position, position) < .0001f &&
                    Quaternion.Angle(root.transform.rotation, rotation) < .01f && root.transform.localScale == scale,
                    name + ": drop changed preview shape or pose");
                Require(!prop.body.isKinematic && prop.body.useGravity, name + ": drop did not restore physics");
                UnityEngine.Object.DestroyImmediate(root);
                Debug.Log("GALLERY_HOLD_PASS " + name);
            }
            catch (Exception e) { errors.Add(name + ": " + e.Message); Debug.Log("GALLERY_HOLD_FAIL " + name + ": " + e.Message); }
        }

        static void Grab(GalleryGame game, Vector3 target)
        {
            Vector3 direction = target - game.view.transform.position;
            game.yaw = Mathf.Atan2(direction.x, direction.z);
            game.pitch = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude);
            game.SyncView(); game.Interact();
            Require(game.held == game.props[0], "Camera ray did not pick up the fixture");
            game.AdvanceHeld(.8f);
        }

        static void CheckShape(GalleryGame game, Vector3 original, Vector3 offset, Vector3 detailScale, Transform detail)
        {
            var prop = game.held;
            Vector3 current = prop.transform.localScale;
            Require(Mathf.Abs(current.y / current.x - original.y / original.x) < .0001f &&
                Mathf.Abs(current.z / current.x - original.z / original.x) < .0001f, "Pickup deformed original proportions");
            Require(detail.localPosition == offset && detail.localScale == detailScale, "Compound part geometry changed");
            Require(game.ValidPlacement(prop, out var reason), "Held shape overlaps geometry: " + reason);
            Require(prop.body.isKinematic, "Held shape is not kinematic");
            if (!game.PlacementBlocked)
                Require(Vector3.Distance(prop.transform.TransformPoint(game.GrabLocalPoint),
                    game.view.transform.position + game.view.transform.forward * game.holdDistance) < .0005f,
                    "Grabbed point drifted off camera ray");
        }
    }
}
