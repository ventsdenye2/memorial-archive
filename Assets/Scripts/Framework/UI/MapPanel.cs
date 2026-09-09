using System;
using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    /// <summary>
    /// Displays the authored UI2.0 map and the player's position marker.
    ///
    /// The map artwork does not contain world geometry that can be sampled at
    /// runtime. Positions therefore stay as explicit scene/room entries in
    /// the prefab. An unknown scene or room deliberately leaves the marker
    /// hidden instead of guessing from a scene name or world position.
    /// </summary>
    public sealed class MapPanel : BasePanel
    {
        [Serializable]
        private sealed class MarkerPlacement
        {
            [SerializeField] private string sceneId;
            [SerializeField] private string roomId;
            [SerializeField] private Vector2 normalizedPosition;

            public string SceneId => sceneId ?? string.Empty;
            public string RoomId => roomId ?? string.Empty;
            public Vector2 NormalizedPosition => normalizedPosition;
        }

        [Header("Map marker")]
        [SerializeField] private RectTransform mapRect;
        [SerializeField] private Image markerImage;
        [SerializeField] private List<MarkerPlacement> markerPlacements = new List<MarkerPlacement>();

        private EventBus subscribedEvents;
        private string activeSceneId = string.Empty;
        private string activeRoomId = string.Empty;

        protected override void Awake()
        {
            base.Awake();
            HideMarker();
        }

        private void OnEnable()
        {
            // The panel can be disabled while a scene transition is in flight.
            // Do not let the next enable reuse the room/scene captured before
            // that transition.
            RefreshSceneState(clearRoom: true);
            TrySubscribe();
            RefreshMarker();
        }

        private void Start()
        {
            // GameRoot has a lower execution order than scene UI, but keeping
            // this retry makes the panel safe in editor previews and in scenes
            // that instantiate their UI before the root object.
            TrySubscribe();
            RefreshMarker();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public override void Open()
        {
            base.Open();
            // BasePanel.Close only hides the canvas group, so Open can be
            // called without an OnDisable/OnEnable pair.  Always resync from
            // Unity's active scene here and discard a room event from the
            // previous open.
            RefreshSceneState(clearRoom: true);
            TrySubscribe();
            RefreshMarker();
        }

        public override void Close()
        {
            HideMarker();
            base.Close();
        }

        private void TrySubscribe()
        {
            var events = GameRoot.Instance?.Context?.Events;
            if (events == null || subscribedEvents == events)
            {
                return;
            }

            Unsubscribe();
            RefreshSceneState(clearRoom: true);
            subscribedEvents = events;
            subscribedEvents.Subscribe<SceneLoadedEvent>(HandleSceneLoaded);
            subscribedEvents.Subscribe<RoomEnteredEvent>(HandleRoomEntered);
            subscribedEvents.Subscribe<RoomExitedEvent>(HandleRoomExited);
        }

        private void RefreshSceneState(bool clearRoom)
        {
            var scene = SceneManager.GetActiveScene();
            activeSceneId = scene.IsValid() ? scene.name ?? string.Empty : string.Empty;
            if (clearRoom)
            {
                activeRoomId = string.Empty;
            }
        }

        private void Unsubscribe()
        {
            if (subscribedEvents == null)
            {
                return;
            }

            subscribedEvents.Unsubscribe<SceneLoadedEvent>(HandleSceneLoaded);
            subscribedEvents.Unsubscribe<RoomEnteredEvent>(HandleRoomEntered);
            subscribedEvents.Unsubscribe<RoomExitedEvent>(HandleRoomExited);
            subscribedEvents = null;
        }

        private void HandleSceneLoaded(SceneLoadedEvent evt)
        {
            activeSceneId = evt.SceneId ?? string.Empty;
            activeRoomId = string.Empty;
            RefreshMarker();
        }

        private void HandleRoomEntered(RoomEnteredEvent evt)
        {
            activeRoomId = evt.RoomId ?? string.Empty;
            RefreshMarker();
        }

        private void HandleRoomExited(RoomExitedEvent evt)
        {
            if (string.Equals(activeRoomId, evt.RoomId, StringComparison.Ordinal))
            {
                activeRoomId = string.Empty;
                RefreshMarker();
            }
        }

        private void RefreshMarker()
        {
            if (markerImage == null)
            {
                return;
            }

            var placement = FindPlacement();
            if (placement == null)
            {
                HideMarker();
                return;
            }

            var targetRect = mapRect != null ? mapRect : transform as RectTransform;
            if (targetRect == null)
            {
                HideMarker();
                return;
            }

            var markerRect = markerImage.rectTransform;
            markerRect.SetParent(targetRect, false);
            var normalized = new Vector2(
                Mathf.Clamp01(placement.NormalizedPosition.x),
                Mathf.Clamp01(placement.NormalizedPosition.y));
            markerRect.anchorMin = normalized;
            markerRect.anchorMax = normalized;
            markerRect.anchoredPosition = Vector2.zero;
            markerRect.localScale = Vector3.one;
            markerImage.raycastTarget = false;
            markerImage.gameObject.SetActive(true);
        }

        private MarkerPlacement FindPlacement()
        {
            // An exact scene+room entry wins when a project uses room IDs for
            // two distinct rooms within a scene.
            foreach (var placement in markerPlacements)
            {
                if (placement != null &&
                    !string.IsNullOrEmpty(placement.SceneId) &&
                    !string.IsNullOrEmpty(placement.RoomId) &&
                    string.Equals(placement.SceneId, activeSceneId, StringComparison.Ordinal) &&
                    string.Equals(placement.RoomId, activeRoomId, StringComparison.Ordinal))
                {
                    return placement;
                }
            }

            // Room-only entries are useful when a room marker is shared by
            // multiple scene variants.
            foreach (var placement in markerPlacements)
            {
                if (placement != null &&
                    string.IsNullOrEmpty(placement.SceneId) &&
                    !string.IsNullOrEmpty(placement.RoomId) &&
                    string.Equals(placement.RoomId, activeRoomId, StringComparison.Ordinal))
                {
                    return placement;
                }
            }

            foreach (var placement in markerPlacements)
            {
                if (placement != null &&
                    !string.IsNullOrEmpty(placement.SceneId) &&
                    string.IsNullOrEmpty(placement.RoomId) &&
                    string.Equals(placement.SceneId, activeSceneId, StringComparison.Ordinal))
                {
                    return placement;
                }
            }

            return null;
        }

        private void HideMarker()
        {
            if (markerImage != null)
            {
                markerImage.gameObject.SetActive(false);
            }
        }
    }
}
