using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ShadeLink.Gallery;

namespace ShadeLink.Tutorial
{
    // Keeps one closed cabin on screen while the two stages replace their own
    // player controllers. No new sound assets or transition fade are used.
    public sealed class TutorialMuseumElevatorFlow : MonoBehaviour
    {
        public static TutorialMuseumElevatorFlow Instance { get; private set; }
        public static bool ArrivalPending => Instance != null && Instance.IsLoading;
        public TutorialElevatorRide ActiveRide { get; private set; }
        public TutorialGame CurrentTutorial { get; private set; }
        public GalleryGame CurrentGallery { get; private set; }
        public bool IsLoading { get; private set; }
        public string LastFailure { get; private set; }

        void Awake() { SceneManager.sceneLoaded += OnSceneLoaded; }
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsLoading && ActiveRide != null && ActiveRide.DestinationReady && scene.name != "Gallery") Destroy(gameObject);
        }

        public static void Install(TutorialGame tutorial, TutorialElevatorRide ride)
        {
            if (Instance != null) Destroy(Instance.gameObject);
            var flow = new GameObject("Tutorial → Museum elevator route").AddComponent<TutorialMuseumElevatorFlow>();
            Instance = flow;
            DontDestroyOnLoad(flow.gameObject);
            flow.CurrentTutorial = tutorial;
            flow.ActiveRide = ride;
            ride.DepartureReady += flow.OnDeparture;
        }

        void Update()
        {
            if (ActiveRide == null) return;
            ActiveRide.Paused = CurrentTutorial != null && !CurrentTutorial.ElevatorDeparted && CurrentTutorial.ElevatorPaused;
        }

        void OnDeparture(TutorialElevatorRide ride)
        {
            if (IsLoading || CurrentGallery != null) return;
            StartCoroutine(Travel());
        }

        IEnumerator Travel()
        {
            if (!Application.CanStreamedLevelBeLoaded("Gallery"))
            {
                LastFailure = "Gallery is absent from build scenes";
                Debug.LogError(LastFailure);
                CurrentTutorial.RecoverElevatorDeparture();
                yield break;
            }
            IsLoading = true;
            CurrentTutorial.BeginElevatorDeparture();
            ActiveRide.CaptureTransferView();
            DontDestroyOnLoad(ActiveRide.gameObject);
            var load = SceneManager.LoadSceneAsync("Gallery", LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return null; // Gallery constructs its player and exhibition.
            CurrentGallery = UnityEngine.Object.FindFirstObjectByType<GalleryGame>();
            if (CurrentGallery == null)
            {
                LastFailure = "GalleryGame missing after elevator transfer";
                Debug.LogError(LastFailure);
                yield break;
            }
            ActiveRide.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            GalleryElevatorEntrance.Open(CurrentGallery);
            CurrentGallery.BeginElevatorArrival(ActiveRide);
            ActiveRide.BindPlayer(CurrentGallery.controller, CurrentGallery.view);
            ActiveRide.NotifyDestinationReady();
            IsLoading = false;
            Debug.Log("TUTORIAL_ELEVATOR_GALLERY_READY");
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (ActiveRide != null) ActiveRide.DepartureReady -= OnDeparture;
            if (ActiveRide != null) Destroy(ActiveRide.gameObject);
            if (Instance == this) Instance = null;
        }
    }
}
