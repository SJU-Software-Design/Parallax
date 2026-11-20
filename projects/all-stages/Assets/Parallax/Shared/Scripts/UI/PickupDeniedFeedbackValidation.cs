using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Audio;
using ShadeLink.Tutorial;
using ShadeLink.Gallery;
using ShadeLink.Beach;

namespace ShadeLink
{
    // The entire fixture is synchronous and restored before the first iterator
    // return. No stage Update, FixedUpdate, capture or OS input sees it offsite.
    public static class PickupDeniedFeedbackValidation
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        const string StandingNotice = "서 있는 사물은 잡을 수 없습니다. 먼저 바닥이나 다른 발판으로 이동하세요.";
        const string StackedNotice = "위에 놓인 사물을 먼저 옮겨 주세요.";

        sealed class StateSnapshot
        {
            readonly object owner;
            readonly FieldInfo[] fields;
            readonly object[] values;
            readonly Dictionary<int, object[]> contents = new Dictionary<int, object[]>();
            public StateSnapshot(object value)
            {
                owner = value; fields = value.GetType().GetFields(Fields); values = new object[fields.Length];
                for (int i = 0; i < fields.Length; i++)
                {
                    values[i] = fields[i].GetValue(owner);
                    if (values[i] is IList list)
                    {
                        var copy = new object[list.Count]; list.CopyTo(copy, 0); contents[i] = copy;
                    }
                }
            }
            public void Restore()
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    if (contents.TryGetValue(i, out var copy) && values[i] is IList list)
                    {
                        if (list.IsFixedSize) { for (int j = 0; j < copy.Length; j++) list[j] = copy[j]; }
                        else if (!list.IsReadOnly) { list.Clear(); foreach (var item in copy) list.Add(item); }
                    }
                    if (!fields[i].IsInitOnly) fields[i].SetValue(owner, values[i]);
                }
            }
            public bool Matches()
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    if (!Equals(fields[i].GetValue(owner), values[i])) return false;
                    if (contents.TryGetValue(i, out var copy) && values[i] is IList list)
                    {
                        if (list.Count != copy.Length) return false;
                        for (int j = 0; j < copy.Length; j++) if (!Equals(list[j], copy[j])) return false;
                    }
                }
                return true;
            }
        }

        sealed class AudioState
        {
            readonly AudioSource source;
            readonly AudioClip clip;
            readonly AudioMixerGroup mixer;
            readonly bool enabled, playing;
            readonly float volume;
            readonly int timeSamples;
            public AudioState(AudioSource source)
            {
                this.source = source; clip = source.clip; mixer = source.outputAudioMixerGroup;
                enabled = source.enabled; playing = source.isPlaying; volume = source.volume; timeSamples = source.timeSamples;
            }
            public void Restore()
            {
                if (source == null) return;
                source.Stop(); source.clip = clip; source.outputAudioMixerGroup = mixer;
                source.volume = volume; source.enabled = enabled;
                if (clip != null && timeSamples >= 0 && timeSamples < clip.samples) source.timeSamples = timeSamples;
                if (playing && enabled) source.Play();
            }
        }

        readonly struct BodyState
        {
            readonly Vector3 position, scale, velocity, angularVelocity;
            readonly Quaternion rotation;
            readonly bool kinematic, gravity, held, ready, supported;
            readonly float mass;
            readonly int layer;
            readonly bool[] colliders;
            readonly PlacementSupport.Snapshot placement;
            public BodyState(IInteractionProp prop)
            {
                var body = prop.Body; position = body.transform.position; rotation = body.transform.rotation;
                scale = body.transform.localScale; velocity = body.linearVelocity; angularVelocity = body.angularVelocity;
                kinematic = body.isKinematic; gravity = body.useGravity; mass = body.mass; layer = body.gameObject.layer;
                held = prop.Placement.Held; ready = prop.Placement.Ready; supported = prop.Placement.Supported;
                placement = prop.Placement.Capture(); colliders = new bool[prop.Parts.Length];
                for (int i = 0; i < colliders.Length; i++) colliders[i] = prop.Parts[i].enabled;
            }
            public bool Matches(IInteractionProp prop)
            {
                var body = prop.Body; var current = prop.Placement.Capture();
                bool result = Vector3.Distance(position, body.transform.position) < .00001f && Quaternion.Angle(rotation, body.transform.rotation) < .001f &&
                    Vector3.Distance(scale, body.transform.localScale) < .00001f && Vector3.Distance(velocity, body.linearVelocity) < .00001f &&
                    Vector3.Distance(angularVelocity, body.angularVelocity) < .00001f && kinematic == body.isKinematic && gravity == body.useGravity &&
                    Mathf.Abs(mass - body.mass) < .00001f && layer == body.gameObject.layer && held == prop.Placement.Held &&
                    ready == prop.Placement.Ready && supported == prop.Placement.Supported && placement.Equals(current);
                for (int i = 0; i < colliders.Length; i++) result &= colliders[i] == prop.Parts[i].enabled;
                return result;
            }
        }

        static void Set(Component owner, string name, object value, bool required = false)
        {
            var field = owner.GetType().GetField(name, Fields);
            if (field == null) { if (required) throw new MissingFieldException(owner.GetType().Name, name); return; }
            field.SetValue(owner, value);
        }
        static string Notice(Component game)
        {
            var field = game.GetType().GetField(game is TutorialGame ? "message" : "notice", Fields);
            return field == null ? null : field.GetValue(game) as string;
        }
        static int Count(CharacterController controller) => controller.GetComponent<PickupDeniedFeedback>()?.PlayCount ?? 0;
        static string AudioDetail(PickupDeniedFeedback feedback, bool noSound)
        {
            var source = feedback == null ? null : feedback.Source;
            var clip = source == null ? null : source.clip;
            var selected = Resources.Load<AudioClip>("Preview/UI/Denied");
            var group = source == null ? null : source.outputAudioMixerGroup;
            return "requests=" + (feedback == null ? 0 : feedback.PlayCount) + " source=" + (source == null ? "<none>" : source.GetInstanceID().ToString()) +
                " playing=" + (source != null && source.isPlaying) + " timeSamples=" + (source == null ? 0 : source.timeSamples) +
                " volume=" + (source == null ? "<none>" : source.volume.ToString("F5")) +
                " spatial=" + (source == null ? "<none>" : source.spatialBlend.ToString("F5")) +
                " loop=" + (source != null && source.loop) + " playOnAwake=" + (source != null && source.playOnAwake) +
                " mixerGroup=" + (group == null ? "<none>" : group.name + "#" + group.GetInstanceID()) +
                " mixer=" + (group == null || group.audioMixer == null ? "<none>" : group.audioMixer.name) +
                " routingReady=" + global::GameShellSettingsRuntime.AudioRoutingReady +
                " clip=" + (clip == null ? "<none>" : clip.name + "#" + clip.GetInstanceID()) +
                " selected=" + (selected == null ? "<none>" : selected.name + "#" + selected.GetInstanceID()) +
                " selectedMatches=" + (selected != null && selected == clip) +
                " clipSamples=" + (clip == null ? 0 : clip.samples) + " selectedSamples=" + (selected == null ? 0 : selected.samples) +
                " clipLoadState=" + (clip == null ? "<none>" : clip.loadState.ToString()) +
                " selectedLoadState=" + (selected == null ? "<none>" : selected.loadState.ToString()) +
                " nosound=" + noSound + " (dispatch/state validation; not audible listening)";
        }
        static void MovePlayer(CharacterController controller, Camera view, Vector3 feet, Vector3 eye, Vector3 target)
        {
            controller.enabled = false; controller.transform.position = feet; controller.enabled = true;
            view.transform.position = eye; view.transform.rotation = Quaternion.LookRotation(target - eye);
            Physics.SyncTransforms();
        }
        static T Fixture<T>(Transform parent, string name, Vector3 position, Vector3 size) where T : Component, IInteractionProp
        {
            var node = new GameObject(name); node.transform.SetParent(parent, false); node.transform.position = position;
            node.AddComponent<BoxCollider>().size = size;
            var prop = node.AddComponent<T>();
            if (prop is GalleryProp gallery) gallery.Remember();
            else if (prop is BeachProp beach) beach.Remember();
            prop.InitializePhysics(); prop.Body.isKinematic = false; prop.Body.useGravity = true;
            prop.Body.linearVelocity = new Vector3(.12f, -.04f, .03f);
            if (typeof(T) == typeof(GalleryProp)) node.AddComponent<TutorialLandingFeedback>();
            return prop;
        }

        public static IEnumerator Run(Component game, Action<bool, string, string> check)
        {
            if (game is TutorialGame tutorial) Exercise(tutorial, (IObjectInteractionHost<GalleryProp>)tutorial, check);
            else if (game is GalleryGame gallery) Exercise(gallery, (IObjectInteractionHost<GalleryProp>)gallery, check);
            else if (game is BeachGame beach) Exercise(beach, (IObjectInteractionHost<BeachProp>)beach, check);
            else check(false, "Pickup refusal fixture receives one of the three real stage hosts", game == null ? "null" : game.GetType().FullName);
            yield break;
        }

        static void Exercise<T>(Component game, IObjectInteractionHost<T> host, Action<bool, string, string> check) where T : Component, IInteractionProp
        {
            string label = game.GetType().Name;
            bool noSound = Array.Exists(Environment.GetCommandLineArgs(), arg => string.Equals(arg, "-nosound", StringComparison.OrdinalIgnoreCase));
            var controller = host.Controller; var view = host.Camera;
            check(controller != null && view != null, label + " pickup refusal has a real player and camera", "native fixture");
            if (controller == null || view == null) return;
            var state = new StateSnapshot(game);
            Vector3 controllerPosition = controller.transform.localPosition, controllerScale = controller.transform.localScale;
            Quaternion controllerRotation = controller.transform.localRotation;
            Vector3 cameraPosition = view.transform.localPosition, cameraScale = view.transform.localScale;
            Quaternion cameraRotation = view.transform.localRotation;
            bool controllerEnabled = controller.enabled;
            var originalFeedback = controller.GetComponent<PickupDeniedFeedback>();
            var originalSource = originalFeedback == null ? null : originalFeedback.Source;
            var feedbackState = originalFeedback == null ? null : new StateSnapshot(originalFeedback);
            var audioState = originalSource == null ? null : new AudioState(originalSource);
            GameObject fixtureRoot = null;
            ObjectInteraction<T> interaction = null;
            T lower = null;
            try
            {
                // Disable unrelated special puzzle adapters, not the real shared
                // pickup lifecycle, so the fixture cannot trigger a puzzle event.
                foreach (string name in new[] { "mirror", "aimedMirror", "bottleBalance", "portraitGate", "shadowHarbor", "harborFinale", "counterweightFinale", "castleRoute" }) Set(game, name, null);
                Set(game, "active", true); Set(game, "won", false); Set(game, "paused", false); Set(game, "climbArrivalPlaying", false);
                fixtureRoot = new GameObject("Pickup denied · synchronous native fixture");
                Vector3 origin = new Vector3(4096, 1024, 4096);
                lower = Fixture<T>(fixtureRoot.transform, "Lower fixture prop", origin + Vector3.up * .5f, new Vector3(3, 1, 3));
                var upper = Fixture<T>(fixtureRoot.transform, "Upper fixture prop", origin + Vector3.right * 8 + Vector3.up, Vector3.one * .7f);
                Set(game, game is TutorialGame ? "<Props>k__BackingField" : "props", new[] { lower, upper }, true);
                host.Held = null; host.Aimed = null;
                interaction = new ObjectInteraction<T>(host, autoUprightOnPickup: true);
                Set(game, "interaction", interaction, true);
                if (originalSource != null) originalSource.Stop();

                MovePlayer(controller, view, origin + new Vector3(0, 1.03f, 0), origin + new Vector3(0, 3, 2), lower.WorldBounds.center);
                int before = Count(controller);
                interaction.UpdateAim();
                check(host.Aimed == lower && Count(controller) == before,
                    label + " hovering a blocked prop does not request an error cue", "aim=" + (host.Aimed == lower) + " count=" + Count(controller));
                bool supported = Physics.Raycast(controller.transform.position + Vector3.up * .15f, Vector3.down,
                    out var support, .3f, host.InteractionMask, QueryTriggerInteraction.Ignore) && support.collider.GetComponentInParent<T>() == lower;
                check(supported, label + " player-standing fixture reaches the production support ray", "support=" + supported);
                var lowerBefore = new BodyState(lower);
                interaction.Interact();
                var feedback = controller.GetComponent<PickupDeniedFeedback>();
                var source = feedback == null ? null : feedback.Source;
                check(host.Held == null && Notice(game) == StandingNotice && Count(controller) == before + 1,
                    label + " standing on a prop refuses pickup with its original notice and one cue", AudioDetail(feedback, noSound));
                var selected = Resources.Load<AudioClip>("Preview/UI/Denied");
                check(source != null && selected != null && source.clip == selected && selected.samples > 0 &&
                    Mathf.Abs(source.volume - .20f) < .00001f && source.spatialBlend == 0 && !source.loop && !source.playOnAwake &&
                    source.outputAudioMixerGroup != null && source.outputAudioMixerGroup.name == "Sfx" &&
                    (noSound || source.isPlaying),
                    label + " pickup refusal dispatches the selected nonspatial clip through Sfx at authored gain", AudioDetail(feedback, noSound));
                check(lowerBefore.Matches(lower), label + " refused standing pickup preserves prop physics and placement", "unchanged=" + lowerBefore.Matches(lower));

                MovePlayer(controller, view, origin + new Vector3(0, 0, -4), origin + new Vector3(0, 1.5f, -4), lower.WorldBounds.center);
                upper.transform.position = origin + new Vector3(0, 1.36f, 0); upper.Body.position = upper.transform.position;
                Physics.SyncTransforms();
                var bounds = upper.WorldBounds;
                bool stacked = Physics.Raycast(new Vector3(bounds.center.x, bounds.min.y + .04f, bounds.center.z), Vector3.down,
                    out var below, .14f, host.InteractionMask, QueryTriggerInteraction.Ignore) && below.collider.GetComponentInParent<T>() == lower;
                check(stacked, label + " stacked fixture reaches the production upper-prop support ray", "stacked=" + stacked);
                lowerBefore = new BodyState(lower); var upperBefore = new BodyState(upper);
                before = Count(controller); var reusedSource = source;
                interaction.Interact();
                feedback = controller.GetComponent<PickupDeniedFeedback>(); source = feedback == null ? null : feedback.Source;
                check(host.Held == null && Notice(game) == StackedNotice && Count(controller) == before + 1,
                    label + " a supported upper prop refuses pickup with its original notice and one cue", AudioDetail(feedback, noSound));
                check(source != null && source == reusedSource && controller.GetComponents<PickupDeniedFeedback>().Length == 1,
                    label + " repeated pickup refusals reuse one feedback source", AudioDetail(feedback, noSound));
                check(lowerBefore.Matches(lower) && upperBefore.Matches(upper),
                    label + " refused stacked pickup preserves both props", "lower=" + lowerBefore.Matches(lower) + " upper=" + upperBefore.Matches(upper));

                upper.transform.position = origin + Vector3.right * 8 + Vector3.up; upper.Body.position = upper.transform.position;
                Physics.SyncTransforms();
                if (source != null) source.Stop();
                before = Count(controller);
                interaction.UpdateAim();
                check(host.Aimed == lower && Count(controller) == before && (source == null || !source.isPlaying),
                    label + " clearing the obstruction keeps hover silent", AudioDetail(feedback, noSound));
                interaction.Interact();
                check(host.Held == lower && lower.Placement.Held && Count(controller) == before && (source == null || !source.isPlaying),
                    label + " the same prop can be picked up after removing its blocker without an error cue", AudioDetail(feedback, noSound));
            }
            catch (Exception error)
            {
                check(false, label + " pickup refusal fixture completes without an exception", error.ToString());
            }
            finally
            {
                if (interaction != null && host.Held == lower && lower != null)
                    try { interaction.Cancel(true); } catch (Exception error) { check(false, label + " fixture hold cleanup", error.Message); }
                var currentFeedback = controller.GetComponent<PickupDeniedFeedback>();
                var currentSource = currentFeedback == null ? null : currentFeedback.Source;
                if (currentSource != null && currentSource != originalSource)
                { currentSource.Stop(); currentSource.gameObject.SetActive(false); UnityEngine.Object.Destroy(currentSource.gameObject); }
                if (originalFeedback == null && currentFeedback != null) UnityEngine.Object.Destroy(currentFeedback);
                feedbackState?.Restore(); audioState?.Restore();
                if (fixtureRoot != null) { fixtureRoot.SetActive(false); UnityEngine.Object.Destroy(fixtureRoot); }
                state.Restore();
                controller.enabled = false;
                controller.transform.localPosition = controllerPosition; controller.transform.localRotation = controllerRotation; controller.transform.localScale = controllerScale;
                view.transform.localPosition = cameraPosition; view.transform.localRotation = cameraRotation; view.transform.localScale = cameraScale;
                controller.enabled = controllerEnabled;
                Physics.SyncTransforms();
            }
            bool poseRestored = Vector3.Distance(controller.transform.localPosition, controllerPosition) < .00001f &&
                Quaternion.Angle(controller.transform.localRotation, controllerRotation) < .001f && controller.enabled == controllerEnabled &&
                Vector3.Distance(view.transform.localPosition, cameraPosition) < .00001f && Quaternion.Angle(view.transform.localRotation, cameraRotation) < .001f;
            check(state.Matches() && poseRestored, label + " pickup fixture restores host fields, caches and player pose before yielding", "fields=" + state.Matches() + " pose=" + poseRestored);
        }
    }
}
