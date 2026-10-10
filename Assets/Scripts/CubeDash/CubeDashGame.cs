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
        [SerializeField] private RunnerAudio audioPresentation = null;
        [SerializeField, Range(0f, 1f)] private float collectionVolume = 0.65f;
        [SerializeField, Range(0f, 0.1f)] private float collectionPitchStep = 0.045f;
        [SerializeField, Range(0.04f, 0.2f)] private float laneSmoothTime = 0.085f;
        [SerializeField, Min(0f)] private float cubeEmission = 0.75f;
        [SerializeField, Min(0f)] private float pickupEmission = 2.8f;
        [SerializeField, Range(0.2f, 0.6f)] private float pickupDuration = 0.35f;

        [Header("Runner tuning")]
        [SerializeField] private CubeColor playerCubeColor = CubeColor.Red;
        [SerializeField, Min(1f)] private float startSpeed = 12f;
        [SerializeField, Range(12f, 48f)] private float maximumSpeed = 30f;
        [Tooltip("How quickly speed approaches the score-based target, in metres per second squared.")]
        [SerializeField, Min(0f)] private float acceleration = 0.8f;
        [Tooltip("Points needed to reach top speed and the most demanding lane patterns.")]
        [SerializeField, Min(30)] private int scoreForMaximumDifficulty = 180;
        [SerializeField, Min(12f)] private float laneChangeSpeed = 18f;
        [Tooltip("0 gives each run a fresh seed. Use a nonzero value to reproduce a track.")]
        [SerializeField] private int fixedSeed = 0;

        [Header("Power-ups")]
        [SerializeField, Min(0f)] private float doublePointsDuration = 7f;
        [SerializeField] private PlayerFlight flightPresentation = null;
        [SerializeField, Min(0.1f)] private float flightDuration = 10f;
        [SerializeField, Min(0f)] private float landingShieldDuration = 3f;
        [SerializeField, Min(2.5f)] private float flightAltitude = 3.2f;
        [SerializeField] private PlayerTruck truckPresentation = null;
        [SerializeField, Min(0.1f)] private float truckDuration = 10f;
        [SerializeField, Min(0f)] private float truckShieldDuration = 3f;
        [SerializeField, Min(0.1f)] private float magnetDuration = 10f;
        [SerializeField, Min(1f)] private float magnetRange = 8f;

        public RunState State { get; private set; }
        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public int Best { get; private set; }
        public int Score { get; private set; }
        public float Difficulty => RunnerRules.DifficultyForScore(Score, scoreForMaximumDifficulty);
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
        private Gamepad activeGamepad;
        private readonly GamepadLaneInput stickInput = new GamepadLaneInput();
        private readonly GamepadLaneInput menuStickInput = new GamepadLaneInput();
        private float shake;
        private Vector3 playerScale;
        private Vector3 playerVisualOrigin;
        private float absorptionPulse;
        private float laneVelocity;
        private float animationTime;
        private float pickupAge = 1f;
        private int shields;
        private float doublePointsTimer;
        private float flightTimer;
        private float landingShieldTimer;
        private float truckTimer;
        private float magnetTimer;
        public bool MagnetActive => magnetTimer > 0;
        public float MagnetRemaining => magnetTimer;
        public float CollectionPulse => absorptionPulse;
        public float CollectionAge => pickupAge;
        public int Shields => shields;
        public bool DoublePointsActive => doublePointsTimer > 0;
        public float DoublePointsRemaining => doublePointsTimer;
        public bool Flying => flightTimer > 0;
        public float FlightRemaining => flightTimer;
        public float LandingShieldRemaining => landingShieldTimer;
        public PlayerFlight FlightPresentation => flightPresentation;
        public bool Trucking => truckTimer > 0;
        public float TruckRemaining => truckTimer;
        public PlayerTruck TruckPresentation => truckPresentation;
        public Transform Player => player;
        public Camera GameCamera => gameCamera;
        public EndlessTrack Track => track;
        public RunnerAudio AudioPresentation => audioPresentation;

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
            if (audioPresentation == null) audioPresentation = GetComponent<RunnerAudio>();
            if (flightPresentation == null) flightPresentation = player.GetComponent<PlayerFlight>();
            if (truckPresentation == null) truckPresentation = player.GetComponent<PlayerTruck>();
            if (playerVisual == null) playerVisual = player;
            playerRotation = playerVisual.localRotation;
            playerScale = playerVisual.localScale;
            playerVisualOrigin = playerVisual.localPosition;
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
                bool landingProtection = landingShieldTimer > 0;
                UpdateFlightTimers(dt);
                UpdateTruckTimers(dt);
                UpdateMagnetTimer(dt);
                landingProtection |= landingShieldTimer > 0;
                float oldX = player.position.x;
                float x = Mathf.SmoothDamp(oldX, playerOrigin.x + (targetLane - 1) * track.LaneWidth,
                    ref laneVelocity, laneSmoothTime, laneChangeSpeed, dt);
                float altitude = Mathf.MoveTowards(player.position.y, playerOrigin.y + (Flying ? flightAltitude : 0), dt * 9f);
                player.position = new Vector3(x, altitude, playerOrigin.z);
                playerVisual.localRotation = Quaternion.Lerp(playerVisual.localRotation,
                    playerRotation * Quaternion.Euler(0, laneVelocity * 0.16f, Mathf.Clamp(-laneVelocity * 0.65f, -12f, 12f)),
                    1f - Mathf.Exp(-16f * dt));
                float targetSpeed = Mathf.Lerp(startSpeed, maximumSpeed, Difficulty);
                Speed = Mathf.MoveTowards(Speed, targetSpeed, acceleration * dt);
                float travel = Speed * dt;
                Distance += travel;
                Bounds bounds = playerCollider.bounds;
                Vector2 halfSize = Trucking && truckPresentation != null ? truckPresentation.ContactHalfSize
                    : new Vector2(bounds.extents.x, bounds.extents.z);
                bool wrongColor = track.Advance(travel, oldX, x, Difficulty,
                    halfSize, playerCollider.enabled ? bounds.center.z : player.position.z,
                    shields > 0, Flying || altitude > playerOrigin.y + 0.2f, landingProtection, Trucking,
                    MagnetActive, magnetRange, player.position, dt,
                    out int collected, out bool shieldUsed);
                if (shieldUsed) shields--;
                Score += collected * (doublePointsTimer > 0 ? 2 : 1);
                if (collected > 0)
                {
                    absorptionPulse = 1f;
                    pickupAge = 0;
                    hud.NotifyCollection(collected);
                    if (collectionAudio != null && collectionSound != null)
                    {
                        collectionAudio.pitch = 1f + ((Score - 1) % 5) * collectionPitchStep;
                        collectionAudio.PlayOneShot(collectionSound, collectionVolume);
                    }
                }
                for (int i = 0; i < track.LastPowerUps.Count; i++) ApplyPowerUp(track.LastPowerUps[i]);
                if (wrongColor) Crash();
                doublePointsTimer = Mathf.Max(0, doublePointsTimer - dt);
            }
            if (State != RunState.Paused) UpdatePlayerAnimation(dt);
            if (State != RunState.Paused) UpdateCamera(dt);
            if (State == RunState.Running && flightPresentation != null)
                flightPresentation.Tick(dt, laneVelocity, !Trucking && (landingShieldTimer > 0 || shields > 0));
            if (State == RunState.Running && truckPresentation != null)
                truckPresentation.Tick(dt, Speed, laneVelocity);
            hud.UpdateStats();
            if (audioPresentation != null) audioPresentation.Tick(dt, State, Flying, Trucking, Speed, laneVelocity);
        }

        private void UpdatePlayerAnimation(float dt)
        {
            animationTime += dt;
            pickupAge += dt;
            absorptionPulse = Mathf.MoveTowards(absorptionPulse, 0, dt / pickupDuration);
            // Compress first, then rebound; settle smoothly at the authored scale.
            float phase = Mathf.Clamp01(pickupAge / pickupDuration);
            float pop = -Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Pow(1f - phase, 2);
            float idle = State == RunState.Ready ? Mathf.Sin(animationTime * 2.6f) * 0.025f : 0;
            playerVisual.localScale = Vector3.Scale(playerScale, new Vector3(1f - pop * 0.13f + idle,
                1f + pop * 0.19f - idle, 1f - pop * 0.13f + idle));
            // Keep the visual's feet on the road without changing the gameplay collider.
            if (playerVisual != player)
                playerVisual.localPosition = playerVisualOrigin + Vector3.up * ((playerVisual.localScale.y - playerScale.y) * 0.5f);
            if (State != RunState.GameOver)
            {
                float breathing = State == RunState.Ready ? 1f + Mathf.Sin(animationTime * 2.6f) * 0.12f : 1f;
                SetPlayerColor(playerColor, cubeEmission * breathing + absorptionPulse * pickupEmission);
            }
        }

        private void UpdateCamera(float dt)
        {
            shake = Mathf.MoveTowards(shake, 0, dt * 0.8f);
            Vector3 offset = shake > 0 ? new Vector3(Mathf.Sin(Time.unscaledTime * 61),
                Mathf.Cos(Time.unscaledTime * 47), 0) * shake : Vector3.zero;
            Vector3 target = cameraOrigin + Vector3.right * ((player.position.x - playerOrigin.x) * 0.16f)
                + Vector3.up * ((player.position.y - playerOrigin.y) * 0.7f);
            cameraFollow = Vector3.Lerp(cameraFollow, target, 1f - Mathf.Exp(-8f * dt));
            gameCamera.transform.position = cameraFollow + offset;
            gameCamera.transform.rotation = cameraRotation;
            gameCamera.fieldOfView = Mathf.Lerp(gameCamera.fieldOfView,
                cameraFov + Mathf.InverseLerp(startSpeed, maximumSpeed, Speed) * 7f, 1f - Mathf.Exp(-3f * dt));
        }

        private void ReadInput()
        {
            if (activeGamepad != null && (!activeGamepad.added || !activeGamepad.enabled))
            {
                activeGamepad = null;
                if (hud.UsingGamepad && State == RunState.Running) TogglePause();
                hud.SetInputMode(false);
            }
            Gamepad pad = Gamepad.current;
            if (pad != null && (!pad.added || !pad.enabled)) pad = null;
            if (pad != activeGamepad)
            {
                activeGamepad = pad;
                ResetGamepadStick();
            }
            Keyboard keyboard = Keyboard.current;
            if ((keyboard != null && keyboard.anyKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)) hud.SetInputMode(false);
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame) { TogglePause(); return; }
                if (keyboard.rKey.wasPressedThisFrame && State != RunState.Ready) { StartRun(); return; }
                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
                {
                    if (State != RunState.Running) hud.ConfirmSelection();
                    return;
                }
                if (keyboard.tabKey.wasPressedThisFrame && State != RunState.Running) hud.CycleMenuSelection();
                int direction = (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame ? 1 : 0)
                    - (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame ? 1 : 0);
                if (direction != 0 && State == RunState.Running) { ChangeLane(direction); return; }
            }
            if (pad == null) return;
            int stickDirection = stickInput.Read(pad.leftStick.x.ReadValue());
            int menuDirection = menuStickInput.Read(pad.leftStick.y.ReadValue());
            bool confirm = pad.buttonSouth.wasPressedThisFrame;
            bool pause = pad.startButton.wasPressedThisFrame;
            bool back = pad.buttonEast.wasPressedThisFrame;
            bool retry = pad.buttonWest.wasPressedThisFrame;
            int dpadDirection = (pad.dpad.right.wasPressedThisFrame ? 1 : 0) - (pad.dpad.left.wasPressedThisFrame ? 1 : 0);
            if (confirm || pause || back || retry || stickDirection != 0 || menuDirection != 0 ||
                dpadDirection != 0 || pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame) hud.SetInputMode(true);
            // Confirm is owned here, not also by the UI module: one press causes one action.
            if (pause)
            {
                if (State == RunState.Ready || State == RunState.GameOver) StartRun();
                else TogglePause();
                return;
            }
            if (back && State == RunState.Paused) { TogglePause(); return; }
            if (retry && (State == RunState.GameOver || State == RunState.Paused)) { StartRun(); return; }
            if (confirm && State != RunState.Running) { hud.ConfirmSelection(); return; }
            if (State == RunState.Running) ChangeLane(dpadDirection != 0 ? dpadDirection : stickDirection);
        }

        private void ResetGamepadStick()
        {
            stickInput.Reset(activeGamepad != null ? activeGamepad.leftStick.x.ReadValue() : 0);
            menuStickInput.Reset(activeGamepad != null ? activeGamepad.leftStick.y.ReadValue() : 0);
        }

        private void OnDisable()
        {
            if (audioPresentation != null) audioPresentation.ResetSounds();
            activeGamepad = null;
            stickInput.Reset();
            menuStickInput.Reset();
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
            ResetGamepadStick();
            player.position = playerOrigin;
            playerVisual.localRotation = playerRotation;
            playerVisual.localScale = playerScale;
            if (playerVisual != player) playerVisual.localPosition = playerVisualOrigin;
            absorptionPulse = 0;
            laneVelocity = 0;
            pickupAge = 1;
            animationTime = 0;
            shields = 0;
            doublePointsTimer = 0;
            flightTimer = landingShieldTimer = 0;
            truckTimer = 0;
            magnetTimer = 0;
            playerRenderer.enabled = true;
            playerCollider.enabled = true;
            if (flightPresentation != null) flightPresentation.ResetPresentation();
            if (truckPresentation != null) truckPresentation.ResetPresentation();
            if (wake != null) wake.SetVisible(true);
            gameCamera.transform.position = cameraOrigin;
            cameraFollow = cameraOrigin;
            gameCamera.fieldOfView = cameraFov;
            if (collectionAudio != null) { collectionAudio.Stop(); collectionAudio.pitch = 1f; }
            if (audioPresentation != null) audioPresentation.ResetSounds();
            playerRenderer.sharedMaterial = track.ColorMaterial(playerCubeColor);
            playerColor = playerRenderer.sharedMaterial.color;
            SetPlayerColor(playerColor, cubeEmission);
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
            ResetGamepadStick();
            if (collectionAudio != null)
            {
                if (State == RunState.Paused) collectionAudio.Pause();
                else collectionAudio.UnPause();
            }
            if (audioPresentation != null) audioPresentation.SetPaused(State == RunState.Paused);
            hud.Show(State);
        }

        private void Crash()
        {
            bool wasFlying = Flying;
            flightTimer = landingShieldTimer = 0;
            truckTimer = 0;
            magnetTimer = 0;
            if (wasFlying) SetFlightPresentation(false);
            SetTruckPresentation(false);
            if (flightPresentation != null) flightPresentation.ResetPresentation();
            State = RunState.GameOver;
            if (audioPresentation != null) audioPresentation.ResetSounds();
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

        private void ApplyPowerUp(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Shield: shields = 1; break;
                case PowerUpType.DoublePoints: doublePointsTimer = doublePointsDuration; break;
                case PowerUpType.Magnet: magnetTimer = Mathf.Max(0.1f, magnetDuration); break;
                case PowerUpType.FighterPlane:
                    if (Trucking) return;
                    flightTimer = Mathf.Max(0.1f, flightDuration);
                    landingShieldTimer = 0;
                    SetFlightPresentation(true);
                    break;
                case PowerUpType.Truck:
                    flightTimer = 0;
                    SetFlightPresentation(false);
                    player.position = new Vector3(player.position.x, playerOrigin.y, playerOrigin.z);
                    truckTimer = Mathf.Max(0.1f, truckDuration);
                    landingShieldTimer = 0;
                    SetTruckPresentation(true);
                    break;
            }
            hud.NotifyPowerUp(type);
            if (audioPresentation != null) audioPresentation.PlayPickup(type);
        }

        private void UpdateMagnetTimer(float dt)
        {
            if (State == RunState.Running) magnetTimer = Mathf.Max(0, magnetTimer - dt);
        }

        private void UpdateFlightTimers(float dt)
        {
            if (State != RunState.Running) return;
            landingShieldTimer = Mathf.Max(0, landingShieldTimer - dt);
            if (!Flying) return;
            flightTimer = Mathf.Max(0, flightTimer - dt);
            if (Flying) return;
            landingShieldTimer = landingShieldDuration;
            SetFlightPresentation(false);
            hud.NotifyLandingShield();
        }

        private void SetFlightPresentation(bool flying)
        {
            playerRenderer.enabled = !flying && !Trucking;
            playerCollider.enabled = !flying;
            if (flightPresentation != null) flightPresentation.SetFlying(flying);
            if (wake != null) wake.SetVisible(!flying && !Trucking);
        }

        private void UpdateTruckTimers(float dt)
        {
            if (State != RunState.Running || !Trucking) return;
            truckTimer = Mathf.Max(0, truckTimer - dt);
            if (Trucking) return;
            landingShieldTimer = truckShieldDuration;
            SetTruckPresentation(false);
            hud.NotifyTruckShield();
        }

        private void SetTruckPresentation(bool driving)
        {
            playerRenderer.enabled = !driving && !Flying;
            playerCollider.enabled = !Flying;
            if (truckPresentation != null) truckPresentation.SetDriving(driving);
            if (wake != null) wake.SetVisible(!driving && !Flying);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && State == RunState.Running) TogglePause();
        }

        private void OnValidate()
        {
            maximumSpeed = Mathf.Clamp(maximumSpeed, 12f, 48f);
            startSpeed = Mathf.Clamp(startSpeed, 1f, maximumSpeed);
            laneChangeSpeed = Mathf.Max(12f, laneChangeSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            scoreForMaximumDifficulty = Mathf.Max(30, scoreForMaximumDifficulty);
            flightDuration = Mathf.Max(0.1f, flightDuration);
            landingShieldDuration = Mathf.Max(0, landingShieldDuration);
            flightAltitude = Mathf.Max(2.5f, flightAltitude);
            truckDuration = Mathf.Max(0.1f, truckDuration);
            truckShieldDuration = Mathf.Max(0, truckShieldDuration);
            magnetDuration = Mathf.Max(0.1f, magnetDuration);
            magnetRange = Mathf.Max(1, magnetRange);
        }

        private void SetPlayerColor(Color color, float emission)
        {
            playerAppearance.SetColor("_BaseColor", color);
            playerAppearance.SetColor("_Color", color);
            playerAppearance.SetColor("_EmissionColor", color * emission);
            playerRenderer.SetPropertyBlock(playerAppearance);
        }
    }
}
