using UnityEngine;
using UnityEngine.InputSystem;

namespace CubeDash
{
    public sealed class CubeDashGame : MonoBehaviour
    {
        public enum RunState { Ready, Running, Paused, GameOver }

        [Header("Objects in the scene")]
        [SerializeField] private Transform player = null;
        [SerializeField] private Transform playerVisual = null;
        [SerializeField] private BoxCollider playerCollider = null;
        [SerializeField] private Camera gameCamera = null;
        [SerializeField] private EndlessTrack track = null;
        [SerializeField] private RunnerHud hud = null;
        [SerializeField] private CubeWake wake = null;
        [Header("Arcade feedback")]
        [SerializeField] private AudioSource collectionAudio = null;
        [SerializeField] private AudioClip collectionSound = null;
        [SerializeField, Range(0f, 1f)] private float collectionVolume = 0.65f;
        [SerializeField, Range(0.04f, 0.2f)] private float laneSmoothTime = 0.085f;

        [Header("Runner tuning")]
        [SerializeField] private CubeColor playerCubeColor = CubeColor.Red;
        [SerializeField, Min(1f)] private float startSpeed = 12f;
        [SerializeField, Range(12f, 30f)] private float maximumSpeed = 28f;
        [SerializeField, Min(0f)] private float acceleration = 0.22f;
        [SerializeField, Min(12f)] private float laneChangeSpeed = 18f;
        [Tooltip("0 gives each run a fresh seed. Use a nonzero value to reproduce a track.")]
        [SerializeField] private int fixedSeed = 0;

        public RunState State { get; private set; }
        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public int Best { get; private set; }
        public int Score { get; private set; }
        public CubeColor PlayerCubeColor => playerCubeColor;

        private const string BestKey = "CubeDash.BestColorScore";
        private Renderer playerRenderer;
        private MaterialPropertyBlock playerAppearance;
        private Color playerColor;
        private Vector3 playerOrigin;
        private Quaternion playerRotation;
        private Vector3 cameraOrigin;
        private Vector3 cameraFollow;
        private Quaternion cameraRotation;
        private float cameraFov;
        private int targetLane = 1;
        private Vector2 gestureStart;
        private bool dragging;
        private bool gestureUsed;
        private float shake;
        private Vector3 playerScale;
        private float absorptionPulse;
        private float laneVelocity;
        private float animationTime;
        private float pickupAge = 1f;
        public float CollectionPulse => absorptionPulse;
        public Transform Player => player;
        public Camera GameCamera => gameCamera;
        public EndlessTrack Track => track;

        private void Awake()
        {
            Best = Mathf.Max(0, PlayerPrefs.GetInt(BestKey, 0));
            if (player == null || playerCollider == null || gameCamera == null || track == null || hud == null)
            {
                Debug.LogError("Cube Dash needs the scene's Player, Main Camera, Track, and HUD assigned in the Inspector.", this);
                enabled = false;
                return;
            }
            playerOrigin = player.position;
            if (playerVisual == null) playerVisual = player;
            playerRotation = playerVisual.localRotation;
            playerScale = playerVisual.localScale;
            cameraOrigin = gameCamera.transform.position;
            cameraFollow = cameraOrigin;
            cameraRotation = gameCamera.transform.rotation;
            cameraFov = gameCamera.fieldOfView;
            playerRenderer = playerVisual.GetComponent<Renderer>();
            playerAppearance = new MaterialPropertyBlock();
            playerRenderer.sharedMaterial = track.ColorMaterial(playerCubeColor);
            playerColor = playerRenderer.sharedMaterial.color;
            if (wake != null) wake.SetColor(playerColor);
            track.Reset(fixedSeed == 0 ? System.Environment.TickCount : fixedSeed, playerCubeColor);
            hud.Initialize(this);
            State = RunState.Ready;
            hud.Show(State);
        }

        private void Update()
        {
            ReadInput();
            float dt = Time.deltaTime;
            if (State == RunState.Running)
            {
                float oldX = player.position.x;
                float x = Mathf.SmoothDamp(oldX, playerOrigin.x + (targetLane - 1) * track.LaneWidth,
                    ref laneVelocity, laneSmoothTime, laneChangeSpeed, dt);
                player.position = new Vector3(x, playerOrigin.y, playerOrigin.z);
                playerVisual.localRotation = Quaternion.Lerp(playerVisual.localRotation,
                    playerRotation * Quaternion.Euler(0, laneVelocity * 0.16f, Mathf.Clamp(-laneVelocity * 0.65f, -12f, 12f)),
                    1f - Mathf.Exp(-16f * dt));
                Speed = Mathf.Min(maximumSpeed, Speed + acceleration * dt);
                float travel = Speed * dt;
                Distance += travel;
                Bounds bounds = playerCollider.bounds;
                bool wrongColor = track.Advance(travel, oldX, x, Mathf.InverseLerp(startSpeed, maximumSpeed, Speed),
                    new Vector2(bounds.extents.x, bounds.extents.z), bounds.center.z, out int collected);
                Score += collected;
                if (collected > 0)
                {
                    absorptionPulse = 1f;
                    pickupAge = 0;
                    hud.NotifyCollection(collected);
                    if (collectionAudio != null && collectionSound != null)
                    {
                        collectionAudio.pitch = 1f + ((Score - 1) % 5) * 0.045f;
                        collectionAudio.PlayOneShot(collectionSound, collectionVolume);
                    }
                }
                if (wrongColor) Crash();
            }
            if (State != RunState.Paused) UpdatePlayerAnimation(dt);
            if (State != RunState.Paused) UpdateCamera(dt);
            hud.UpdateStats();
        }

        private void UpdatePlayerAnimation(float dt)
        {
            animationTime += dt;
            pickupAge += dt;
            absorptionPulse = Mathf.MoveTowards(absorptionPulse, 0, dt * 3.8f);
            float pop = pickupAge < 0.35f ? Mathf.Sin(pickupAge / 0.35f * Mathf.PI * 2f) * Mathf.Exp(-pickupAge * 9f) : 0;
            float idle = State == RunState.Ready ? Mathf.Sin(animationTime * 2.6f) * 0.025f : 0;
            playerVisual.localScale = Vector3.Scale(playerScale, new Vector3(1f - pop * 0.13f + idle,
                1f + pop * 0.19f - idle, 1f - pop * 0.13f + idle));
            // Keep the visual's feet on the road without changing the gameplay collider.
            if (playerVisual != player)
                playerVisual.localPosition = Vector3.up * ((playerVisual.localScale.y - playerScale.y) * 0.5f);
            if (State != RunState.GameOver)
                SetPlayerColor(playerColor, 0.55f + absorptionPulse * 2.8f);
        }

        private void UpdateCamera(float dt)
        {
            shake = Mathf.MoveTowards(shake, 0, dt * 0.8f);
            Vector3 offset = shake > 0 ? new Vector3(Mathf.Sin(Time.unscaledTime * 61),
                Mathf.Cos(Time.unscaledTime * 47), 0) * shake : Vector3.zero;
            Vector3 target = cameraOrigin + Vector3.right * ((player.position.x - playerOrigin.x) * 0.16f);
            cameraFollow = Vector3.Lerp(cameraFollow, target, 1f - Mathf.Exp(-8f * dt));
            gameCamera.transform.position = cameraFollow + offset;
            gameCamera.transform.rotation = cameraRotation;
            gameCamera.fieldOfView = Mathf.Lerp(gameCamera.fieldOfView,
                cameraFov + Mathf.InverseLerp(startSpeed, maximumSpeed, Speed) * 7f, 1f - Mathf.Exp(-3f * dt));
        }

        private void ReadInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame) TogglePause();
                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
                {
                    if (State == RunState.Ready || State == RunState.GameOver) StartRun();
                    else if (State == RunState.Paused) TogglePause();
                }
                if (keyboard.rKey.wasPressedThisFrame && State != RunState.Ready) StartRun();
                if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) ChangeLane(-1);
                if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) ChangeLane(1);
            }

            Touchscreen touch = Touchscreen.current;
            if (touch != null && (touch.primaryTouch.press.isPressed || touch.primaryTouch.press.wasReleasedThisFrame))
                ReadGesture(touch.primaryTouch.position.ReadValue(), touch.primaryTouch.press.wasPressedThisFrame,
                    touch.primaryTouch.press.wasReleasedThisFrame);
            else if (Mouse.current != null)
                ReadGesture(Mouse.current.position.ReadValue(), Mouse.current.leftButton.wasPressedThisFrame,
                    Mouse.current.leftButton.wasReleasedThisFrame);
        }

        private void ReadGesture(Vector2 position, bool pressed, bool released)
        {
            if (pressed) { gestureStart = position; dragging = true; gestureUsed = false; }
            if (dragging && !gestureUsed && State == RunState.Running)
            {
                Vector2 delta = position - gestureStart;
                if (Mathf.Abs(delta.x) >= Mathf.Max(35f, Screen.width * 0.045f) && Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                {
                    ChangeLane(delta.x > 0 ? 1 : -1);
                    gestureUsed = true;
                }
            }
            if (released)
            {
                if (dragging && !gestureUsed && Vector2.Distance(position, gestureStart) < 25f &&
                    (State == RunState.Ready || State == RunState.GameOver)) StartRun();
                dragging = false;
            }
        }

        public void ChangeLane(int direction)
        {
            if (State == RunState.Running) targetLane = Mathf.Clamp(targetLane + direction, 0, 2);
        }

        public void StartRun()
        {
            targetLane = 1;
            Distance = 0;
            Score = 0;
            Speed = startSpeed;
            shake = 0;
            dragging = false;
            player.position = playerOrigin;
            playerVisual.localRotation = playerRotation;
            playerVisual.localScale = playerScale;
            if (playerVisual != player) playerVisual.localPosition = Vector3.zero;
            absorptionPulse = 0;
            laneVelocity = 0;
            pickupAge = 1;
            animationTime = 0;
            gameCamera.transform.position = cameraOrigin;
            cameraFollow = cameraOrigin;
            gameCamera.fieldOfView = cameraFov;
            if (collectionAudio != null) collectionAudio.Stop();
            playerRenderer.sharedMaterial = track.ColorMaterial(playerCubeColor);
            playerColor = playerRenderer.sharedMaterial.color;
            SetPlayerColor(playerColor);
            if (wake != null) { wake.SetColor(playerColor); wake.ResetWake(); }
            track.Reset(fixedSeed == 0 ? System.Environment.TickCount : fixedSeed, playerCubeColor);
            State = RunState.Running;
            hud.Show(State);
        }

        public void TogglePause()
        {
            if (State == RunState.Running) State = RunState.Paused;
            else if (State == RunState.Paused) State = RunState.Running;
            else return;
            if (collectionAudio != null)
            {
                if (State == RunState.Paused) collectionAudio.Pause();
                else collectionAudio.UnPause();
            }
            hud.Show(State);
        }

        private void Crash()
        {
            State = RunState.GameOver;
            shake = 0.25f;
            SetPlayerColor(playerColor * 0.7f, 0.1f);
            int score = Score;
            if (score > Best)
            {
                Best = score;
                PlayerPrefs.SetInt(BestKey, Best);
                PlayerPrefs.Save();
            }
            hud.Show(State);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && State == RunState.Running) TogglePause();
        }

        private void OnValidate()
        {
            maximumSpeed = Mathf.Clamp(maximumSpeed, 12f, 30f);
            startSpeed = Mathf.Clamp(startSpeed, 1f, maximumSpeed);
            laneChangeSpeed = Mathf.Max(12f, laneChangeSpeed);
            acceleration = Mathf.Max(0f, acceleration);
        }

        private void SetPlayerColor(Color color, float emission = 0.55f)
        {
            playerAppearance.SetColor("_BaseColor", color);
            playerAppearance.SetColor("_Color", color);
            playerAppearance.SetColor("_EmissionColor", color * emission);
            playerRenderer.SetPropertyBlock(playerAppearance);
        }
    }
}
