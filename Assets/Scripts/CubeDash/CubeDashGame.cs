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
        [Header("Run entry")]
        [SerializeField] private bool useStartCountdown = false;
        public bool UsesStartCountdown => useStartCountdown;
        public bool CountdownActive => countdownRemaining > 0;
        public float CountdownRemaining => countdownRemaining;
        private float countdownRemaining;
        private bool pausedCountdown;
        [Header("Arcade feedback")]
        [SerializeField] private AudioSource collectionAudio = null;
        [SerializeField] private AudioClip collectionSound = null;
        [SerializeField] private RunnerAudio audioPresentation = null;
        [SerializeField] private BiomeAmbience ambiencePresentation = null;
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
        [Tooltip("How quickly speed approaches the progression target, in metres per second squared.")]
        [SerializeField, Min(0f)] private float acceleration = 0.8f;
        [Tooltip("Points needed to reach top speed and the most demanding lane patterns.")]
        [SerializeField, Min(30)] private int scoreForMaximumDifficulty = 180;
        [Tooltip("Progression continues even when coins are skipped. Turn off for the original score-based ramp.")]
        [SerializeField] private bool distanceBasedProgression = true;
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
        [Header("Manual Supercar (cosmetic toggle)")]
        [SerializeField] private PlayerSupercar supercarPresentation = null;
        [SerializeField, Range(0.15f, 0.6f)] private float supercarDoublePressWindow = 0.35f;

        public RunState State { get; private set; }
        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public int Best { get; private set; }
        public int Score { get; private set; }
        public float Difficulty => distanceBasedProgression ? LevelProgression.Difficulty(Distance)
            : RunnerRules.DifficultyForScore(Score, scoreForMaximumDifficulty);
        public int Stage => LevelProgression.Stage(Distance);
        public CubeColor PlayerCubeColor => playerCubeColor;

        private const string BestKey = "CubeDash.BestColorScore";
        private Renderer playerRenderer;
        private PlayerCubeVisual cubeVisualPresentation;
        private float playerMeshBottom = -0.5f;
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
        private float timedShieldDuration;
        private float truckTimer;
        private float magnetTimer;
        private readonly DoublePressInput supercarEnter = new DoublePressInput();
        public bool SupercarEnabled { get; private set; }
        public bool InvincibilityEnabled { get; private set; }
        private bool cheatedRun;
        public bool SupercarDriving => SupercarEnabled && !Flying && !Trucking;
        public PlayerSupercar SupercarPresentation => supercarPresentation;
        public bool MagnetActive => magnetTimer > 0;
        public float MagnetRemaining => magnetTimer;
        public float MagnetDuration => Mathf.Max(0.1f, magnetDuration);
        public float CollectionPulse => absorptionPulse;
        public float CollectionAge => pickupAge;
        public int Shields => shields;
        public bool DoublePointsActive => doublePointsTimer > 0;
        public float DoublePointsRemaining => doublePointsTimer;
        public float DoublePointsDuration => Mathf.Max(0, doublePointsDuration);
        public bool Flying => flightTimer > 0;
        public float FlightRemaining => flightTimer;
        public float FlightDuration => Mathf.Max(0.1f, flightDuration);
        public float LandingShieldRemaining => landingShieldTimer;
        public float TimedShieldDuration => timedShieldDuration;
        public PlayerFlight FlightPresentation => flightPresentation;
        public bool Trucking => truckTimer > 0;
        public float TruckRemaining => truckTimer;
        public float TruckDuration => Mathf.Max(0.1f, truckDuration);
        public PlayerTruck TruckPresentation => truckPresentation;
        public Transform Player => player;
        public Camera GameCamera => gameCamera;
        public EndlessTrack Track => track;
        public RunnerAudio AudioPresentation => audioPresentation;
        public BiomeAmbience AmbiencePresentation => ambiencePresentation;

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
            cubeVisualPresentation = playerVisual.GetComponent<PlayerCubeVisual>();
            if (cubeVisualPresentation != null) cubeVisualPresentation.UseGameplayAnimation();
            if (playerVisual.TryGetComponent(out MeshFilter playerMesh) && playerMesh.sharedMesh != null)
                playerMeshBottom = playerMesh.sharedMesh.bounds.min.y;
            track.PreparePresentation(player);
            if (audioPresentation == null) audioPresentation = GetComponent<RunnerAudio>();
            if (ambiencePresentation == null) ambiencePresentation = GetComponent<BiomeAmbience>();
            if (flightPresentation == null) flightPresentation = player.GetComponent<PlayerFlight>();
            if (truckPresentation == null) truckPresentation = player.GetComponent<PlayerTruck>();
            if (supercarPresentation == null) supercarPresentation = player.GetComponent<PlayerSupercar>();
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
            track.ResetForward(fixedSeed == 0 ? System.Environment.TickCount : fixedSeed, playerCubeColor, playerOrigin.z);
            hud.Initialize(this);
            State = RunState.Ready;
            hud.Show(State);
        }

        private void Start()
        {
            if (useStartCountdown && State == RunState.Ready) RequestRun();
        }

        private void Update()
        {
            ReadInput();
            float dt = Time.deltaTime;
            TickStartCountdown(dt);
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
                player.position = new Vector3(x, altitude, player.position.z);
                // Road-space lean stays the same even when the authored cube face is turned around.
                playerVisual.localRotation = Quaternion.Lerp(playerVisual.localRotation,
                    Quaternion.Euler(0, laneVelocity * 0.16f, Mathf.Clamp(-laneVelocity * 0.65f, -12f, 12f)) * playerRotation,
                    1f - Mathf.Exp(-16f * dt));
                float targetSpeed = Mathf.Lerp(startSpeed, maximumSpeed, Difficulty);
                Speed = Mathf.MoveTowards(Speed, targetSpeed, acceleration * dt);
                float travel = Speed * dt;
                Distance += travel;
                player.position += Vector3.forward * travel;
                // Refresh the moving contact centre before sweeping through stationary world hazards.
                Physics.SyncTransforms();
                Bounds bounds = playerCollider.bounds;
                Vector2 halfSize = Trucking && truckPresentation != null ? truckPresentation.ContactHalfSize
                    : new Vector2(bounds.extents.x, bounds.extents.z);
                bool wrongColor = track.AdvanceForward(travel, oldX, x, Difficulty,
                    halfSize, playerCollider.enabled ? bounds.center.z : player.position.z,
                    shields > 0, Flying || altitude > playerOrigin.y + 0.2f, landingProtection || InvincibilityEnabled, Trucking,
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
                if (audioPresentation != null) audioPresentation.PlayTruckImpacts(track.LastTruckSmashes);
                if (wrongColor) Crash();
                RebaseWorldIfNeeded();
                doublePointsTimer = Mathf.Max(0, doublePointsTimer - dt);
            }
            if (State != RunState.Paused) UpdatePlayerAnimation(dt);
            if (State != RunState.Paused) UpdateCamera(dt);
            if (State == RunState.Running && flightPresentation != null)
                flightPresentation.Tick(dt, laneVelocity, !Trucking && (landingShieldTimer > 0 || shields > 0));
            if (State == RunState.Running && truckPresentation != null)
                truckPresentation.Tick(dt, Speed, laneVelocity);
            if (State == RunState.Running && supercarPresentation != null)
                supercarPresentation.Tick(dt, Speed, laneVelocity);
            hud.UpdateStats();
            if (audioPresentation != null) audioPresentation.Tick(dt, State, Flying, Trucking, Speed, laneVelocity, SupercarDriving);
            if (ambiencePresentation != null) ambiencePresentation.Tick(dt, State, Distance);
        }

        private void UpdatePlayerAnimation(float dt)
        {
            if (cubeVisualPresentation != null) cubeVisualPresentation.TickBlink(dt);
            animationTime += dt;
            pickupAge += dt;
            absorptionPulse = Mathf.MoveTowards(absorptionPulse, 0, dt / pickupDuration);
            // Compress first, then rebound; settle smoothly at the authored scale.
            float phase = Mathf.Clamp01(pickupAge / pickupDuration);
            float pop = -Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Pow(1f - phase, 2);
            Vector3 idle = State == RunState.Ready ? PlayerCubeVisual.JellyScale(animationTime) : Vector3.one;
            Vector3 pickup = new Vector3(1f - pop * 0.13f, 1f + pop * 0.19f, 1f - pop * 0.13f);
            playerVisual.localScale = Vector3.Scale(playerScale, Vector3.Scale(pickup, idle));
            // Keep the visual's feet on the road without changing the gameplay collider.
            if (playerVisual != player)
                playerVisual.localPosition = PlayerCubeVisual.GroundedPosition(playerVisualOrigin, playerScale, playerVisual.localScale, playerMeshBottom);
            if (State != RunState.GameOver)
            {
                float breathing = State == RunState.Ready ? 1f + Mathf.Sin(animationTime * PlayerCubeVisual.IdleAngularSpeed) * 0.12f : 1f;
                SetPlayerColor(playerColor, cubeEmission * breathing + absorptionPulse * pickupEmission);
            }
        }

        private void UpdateCamera(float dt)
        {
            shake = Mathf.MoveTowards(shake, 0, dt * 0.8f);
            Vector3 offset = shake > 0 ? new Vector3(Mathf.Sin(Time.unscaledTime * 61),
                Mathf.Cos(Time.unscaledTime * 47), 0) * shake : Vector3.zero;
            Vector3 target = cameraOrigin + Vector3.right * ((player.position.x - playerOrigin.x) * 0.16f)
                + Vector3.up * ((player.position.y - playerOrigin.y) * 0.7f)
                + Vector3.forward * (player.position.z - playerOrigin.z);
            cameraFollow = Vector3.Lerp(cameraFollow, target, 1f - Mathf.Exp(-8f * dt));
            // Match forward travel exactly; smoothing Z would add a speed-dependent camera lag.
            cameraFollow.z = target.z;
            gameCamera.transform.position = cameraFollow + offset;
            gameCamera.transform.rotation = cameraRotation;
            gameCamera.fieldOfView = Mathf.Lerp(gameCamera.fieldOfView,
                cameraFov + Mathf.InverseLerp(startSpeed, maximumSpeed, Speed) * 7f, 1f - Mathf.Exp(-3f * dt));
        }

        private void RebaseWorldIfNeeded()
        {
            // Travel normally for kilometres, then shift all run-owned coordinates together in one frame.
            // This is not per-frame scenery scrolling; it prevents float jitter on endless runs.
            const float span = 4096;
            float offset = Mathf.Floor((player.position.z - playerOrigin.z) / span) * span;
            if (offset <= 0) return;
            player.position += Vector3.back * offset;
            cameraFollow += Vector3.back * offset;
            gameCamera.transform.position += Vector3.back * offset;
            track.ShiftWorldOrigin(offset);
            if (wake != null) wake.ShiftWorldOrigin(offset);
        }

        private void ReadInput()
        {
            if (activeGamepad != null && (!activeGamepad.added || !activeGamepad.enabled))
            {
                activeGamepad = null;
                if (hud.UsingGamepad && (State == RunState.Running || (State == RunState.Ready && CountdownActive))) TogglePause();
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
            // There is no start button during countdown. Confirm/retry/steering cannot skip it.
            if (CountdownActive && State == RunState.Ready)
            {
                if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame)) hud.SetInputMode(true);
                if ((keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame)) ||
                    (pad != null && pad.startButton.wasPressedThisFrame)) TogglePause();
                return;
            }
            if (keyboard != null)
            {
                if (keyboard.iKey.wasPressedThisFrame) ToggleInvincibility();
                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame) { TogglePause(); return; }
                if (keyboard.rKey.wasPressedThisFrame && State != RunState.Ready) { RequestRun(); return; }
                bool enter = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
                if (enter && State == RunState.Running) PressSupercarEnter(Time.unscaledTimeAsDouble);
                if (keyboard.spaceKey.wasPressedThisFrame || (enter && State != RunState.Running))
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
                if (State == RunState.Ready || State == RunState.GameOver) RequestRun();
                else TogglePause();
                return;
            }
            if (back && State == RunState.Paused) { TogglePause(); return; }
            if (retry && (State == RunState.GameOver || State == RunState.Paused)) { RequestRun(); return; }
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
            countdownRemaining = 0;
            pausedCountdown = false;
            InvincibilityEnabled = false;
            if (hud != null) hud.RefreshCheatStatus();
            if (gameCamera != null && gameCamera.TryGetComponent(out RunnerGraphics graphics))
                graphics.Tick(0, RunState.Ready, 0);
            supercarEnter.Reset(); SupercarEnabled = false;
            if (supercarPresentation != null) supercarPresentation.ResetPresentation();
            if (playerRenderer != null) RefreshGroundPresentation();
            if (audioPresentation != null) audioPresentation.ResetSounds();
            if (ambiencePresentation != null) ambiencePresentation.ResetSounds();
            activeGamepad = null;
            stickInput.Reset();
            menuStickInput.Reset();
        }

        public void ChangeLane(int direction)
        {
            if (State == RunState.Running) targetLane = Mathf.Clamp(targetLane + direction, 0, 2);
        }

        /// <summary>Session-only testing toggle. Survives retries; never grants points or spends shields.</summary>
        public void ToggleInvincibility()
        {
            InvincibilityEnabled = !InvincibilityEnabled;
            if (State == RunState.Running || State == RunState.Paused) cheatedRun |= InvincibilityEnabled;
            if (hud != null) hud.RefreshCheatStatus();
        }

        /// <summary>Called for Enter press edges only; menu confirmations never arm the in-run toggle.</summary>
        public bool PressSupercarEnter(double now)
        {
            if (State != RunState.Running || supercarPresentation == null) { supercarEnter.Reset(); return false; }
            if (!supercarEnter.Press(now, supercarDoublePressWindow)) return false;
            SupercarEnabled = !SupercarEnabled;
            RefreshGroundPresentation();
            hud.NotifySupercar(SupercarEnabled);
            return true;
        }

        /// <summary>Player-facing start/retry; opted-in scenes prepare a run behind a frozen 3, 2, 1 countdown.</summary>
        public void RequestRun()
        {
            if (CountdownActive && State == RunState.Ready) return;
            StartRun();
            if (!useStartCountdown) return;
            countdownRemaining = 3;
            State = RunState.Ready;
            hud.Show(State);
            hud.UpdateStats();
        }

        private void TickStartCountdown(float dt)
        {
            if (!CountdownActive || State != RunState.Ready) return;
            countdownRemaining = Mathf.Max(0, countdownRemaining - Mathf.Max(0, dt));
            if (CountdownActive) return;
            ResetGamepadStick();
            State = RunState.Running;
            hud.Show(State);
        }

        /// <summary>Immediate reset/start for programmatic simulations. Player UI uses RequestRun.</summary>
        public void StartRun()
        {
            countdownRemaining = 0;
            pausedCountdown = false;
            if (gameCamera != null && gameCamera.TryGetComponent(out RunnerGraphics graphics))
                graphics.Tick(0, RunState.Ready, 0);
            targetLane = 1;
            Distance = 0;
            Score = 0;
            cheatedRun = InvincibilityEnabled;
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
            timedShieldDuration = 0;
            truckTimer = 0;
            magnetTimer = 0;
            SupercarEnabled = false; supercarEnter.Reset();
            playerRenderer.enabled = true;
            if (cubeVisualPresentation != null) cubeVisualPresentation.SyncEyes();
            playerCollider.enabled = true;
            if (flightPresentation != null) flightPresentation.ResetPresentation();
            if (truckPresentation != null) truckPresentation.ResetPresentation();
            if (supercarPresentation != null) supercarPresentation.ResetPresentation();
            if (wake != null) wake.SetVisible(true);
            gameCamera.transform.position = cameraOrigin;
            cameraFollow = cameraOrigin;
            gameCamera.fieldOfView = cameraFov;
            if (collectionAudio != null) { collectionAudio.Stop(); collectionAudio.pitch = 1f; }
            if (audioPresentation != null) audioPresentation.ResetSounds();
            playerRenderer.sharedMaterial = track.ColorMaterial(playerCubeColor);
            if (ambiencePresentation != null) ambiencePresentation.ResetSounds();
            playerColor = playerRenderer.sharedMaterial.color;
            SetPlayerColor(playerColor, cubeEmission);
            if (wake != null) { wake.SetColor(playerColor); wake.ResetWake(); }
            track.ResetForward(fixedSeed == 0 ? System.Environment.TickCount : fixedSeed, playerCubeColor, playerOrigin.z);
            State = RunState.Running;
            hud.Show(State);
        }

        public void TogglePause()
        {
            if (State == RunState.Running || (State == RunState.Ready && CountdownActive))
            {
                pausedCountdown = State == RunState.Ready;
                State = RunState.Paused;
            }
            else if (State == RunState.Paused)
            {
                State = pausedCountdown ? RunState.Ready : RunState.Running;
                pausedCountdown = false;
            }
            else return;
            ResetGamepadStick();
            supercarEnter.Reset();
            if (collectionAudio != null)
            {
                if (State == RunState.Paused) collectionAudio.Pause();
                else collectionAudio.UnPause();
            }
            if (audioPresentation != null) audioPresentation.SetPaused(State == RunState.Paused);
            if (ambiencePresentation != null) ambiencePresentation.SetPaused(State == RunState.Paused);
            hud.Show(State);
        }

        private void Crash()
        {
            if (InvincibilityEnabled) return;
            bool wasFlying = Flying;
            flightTimer = landingShieldTimer = 0;
            timedShieldDuration = 0;
            truckTimer = 0;
            magnetTimer = 0;
            SupercarEnabled = false; supercarEnter.Reset();
            if (supercarPresentation != null) supercarPresentation.ResetPresentation();
            if (wasFlying) SetFlightPresentation(false);
            SetTruckPresentation(false);
            if (flightPresentation != null) flightPresentation.ResetPresentation();
            State = RunState.GameOver;
            if (audioPresentation != null) audioPresentation.ResetSounds();
            shake = 0.25f;
            SetPlayerColor(playerColor * 0.7f, 0.1f);
            int score = Score;
            if (!cheatedRun && score > Best)
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
                    timedShieldDuration = 0;
                    SetFlightPresentation(true);
                    break;
                case PowerUpType.Truck:
                    flightTimer = 0;
                    SetFlightPresentation(false);
                    player.position = new Vector3(player.position.x, playerOrigin.y, player.position.z);
                    truckTimer = Mathf.Max(0.1f, truckDuration);
                    landingShieldTimer = 0;
                    timedShieldDuration = 0;
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
            timedShieldDuration = landingShieldDuration;
            SetFlightPresentation(false);
            hud.NotifyLandingShield();
        }

        private void SetFlightPresentation(bool flying)
        {
            if (flightPresentation != null) flightPresentation.SetFlying(flying);
            RefreshGroundPresentation();
        }

        private void UpdateTruckTimers(float dt)
        {
            if (State != RunState.Running || !Trucking) return;
            truckTimer = Mathf.Max(0, truckTimer - dt);
            if (Trucking) return;
            landingShieldTimer = truckShieldDuration;
            timedShieldDuration = truckShieldDuration;
            SetTruckPresentation(false);
            hud.NotifyTruckShield();
        }

        private void SetTruckPresentation(bool driving)
        {
            if (truckPresentation != null) truckPresentation.SetDriving(driving);
            RefreshGroundPresentation();
        }

        private void RefreshGroundPresentation()
        {
            bool carVisible = SupercarDriving && supercarPresentation != null;
            if (supercarPresentation != null) supercarPresentation.SetDriving(carVisible);
            playerRenderer.enabled = !Flying && !Trucking && !carVisible;
            if (cubeVisualPresentation != null) cubeVisualPresentation.SyncEyes();
            playerCollider.enabled = !Flying;
            // Preserve the player's matching-color wake even though the reference car stays cyan.
            if (wake != null) wake.SetVisible(!Flying && !Trucking);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && (State == RunState.Running || (State == RunState.Ready && CountdownActive))) TogglePause();
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
