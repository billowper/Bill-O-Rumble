using System;
using System.Collections;
using System.Collections.Generic;
using GameManagement;
using UnityEngine;

namespace BillORumble
{
    public class RumbleManager : MonoBehaviour
    {
        private float maxGroundedSpeed = 60;
        private float maxGrindSpeed = 50;
        private float maxLandingVelocity = 10;
        private float maxJumpForce = 9f;

        public enum RumbleEvents
        {
            GrindEnter,
            Pop,
            Land,
            Push,
            Catch,
            Bail,
            Brake
        }

        private Dictionary<RumbleEvents, RumbleSettings> rumbleEvents = new Dictionary<RumbleEvents, RumbleSettings>()
        {
            {RumbleEvents.GrindEnter, new RumbleSettings(motor_level: 0.2f, time: .15f)},
            {RumbleEvents.Pop, new RumbleSettings(min_motor_level: 0.2f, max_motor_level: 0.5f, time: 0.2f)},
            {RumbleEvents.Land, new RumbleSettings(min_motor_level: 0.3f, max_motor_level: 1f, time: 0.4f)},
            {RumbleEvents.Push, new RumbleSettings(motor_level: 0.18f, time: 0.4f)},
            {RumbleEvents.Catch, new RumbleSettings(motor_level: 0.2f, time: 0.15f)},
            {RumbleEvents.Bail, new RumbleSettings(motor_level: 1, time: 0.8f)},
            {RumbleEvents.Brake, new RumbleSettings(motor_level: 0.2f, time: 0.3f)},
        };

        public class RumbleSettings
        {
            public bool Enabled = true;
            public float MinMotorLevel;
            public float MaxMotorLevel;
            public int MotorIndex = 0;
            public float Time;

            public float GetMotorLevel(float fraction)
            {
                return Mathf.Lerp(a: MinMotorLevel, b: MaxMotorLevel, t: fraction);
            }

            public RumbleSettings(float motor_level, float time)
            {
                MinMotorLevel = motor_level;
                MaxMotorLevel = motor_level;
                Time = time;
            }

            public RumbleSettings(float min_motor_level, float max_motor_level, float time)
            {
                MinMotorLevel = min_motor_level;
                MaxMotorLevel = max_motor_level;
                Time = time;
            }
        }

        private Dictionary<PlayerController.SurfaceTags, float> surfaceRumbleLookup = new Dictionary<PlayerController.SurfaceTags, float>()
        {
            {PlayerController.SurfaceTags.None, 0},
            {PlayerController.SurfaceTags.Brick, 0.3f},
            {PlayerController.SurfaceTags.Concrete, 0.12f},
            {PlayerController.SurfaceTags.Grass, 0.4f},
            {PlayerController.SurfaceTags.Tarmac, 0.13f},
            {PlayerController.SurfaceTags.Wood, 0.185f}
        };

        private bool enable_Rolling;
        private bool enable_Grinding;
        private bool enable_PowerSlide;

        private float powerSlideMultiplier = 10;
        private float slideMultiplier = 0.5f;
        private float grindMultiplier = 1f;

        private Dictionary<DeckSounds.GrindState, float> grindRumbleLookup = new Dictionary<DeckSounds.GrindState, float>()
        {
            {DeckSounds.GrindState.concrete, 0.3f},
            {DeckSounds.GrindState.metal, 0.1f},
            {DeckSounds.GrindState.none, 0},
            {DeckSounds.GrindState.wood, 0.2f},
        };

        private bool doRumble = true;
        private bool isGrinding;
        private bool isGrounded;
        private bool wasPowerSliding;
        private bool showUI;

        private void OnEnable()
        {
            GameStateMachine.Instance.OnGameStateChanged += GameStateMachine_OnGameStateChanged;
            EventManager.Instance.onGPEvent += EventManager_onRunEvent;

            if (PlayerPrefs.HasKey(key: "BillORumble_InitialValuesSaved") == false)
            {
                SaveValues();
            }
            else
            {
                maxJumpForce = PlayerPrefs.GetFloat(key: $"BillORumble_Settings_{nameof(maxJumpForce)}");
                maxLandingVelocity = PlayerPrefs.GetFloat(key: $"BillORumble_Settings_{nameof(maxLandingVelocity)}");
                maxGrindSpeed = PlayerPrefs.GetFloat(key: $"BillORumble_Settings_{nameof(maxGrindSpeed)}");
                maxGroundedSpeed = PlayerPrefs.GetFloat(key: $"BillORumble_Settings_{nameof(maxGroundedSpeed)}");

                foreach (var p in rumbleEvents)
                {
                    p.Value.Enabled = PlayerPrefs.GetInt(key: $"BillORumble_Event_{p.Key}_{nameof(p.Value.Enabled)}") == 1;
                    p.Value.MinMotorLevel = PlayerPrefs.GetFloat(key: $"BillORumble_Event_{p.Key}_{nameof(p.Value.MinMotorLevel)}");
                    p.Value.MaxMotorLevel = PlayerPrefs.GetFloat(key: $"BillORumble_Event_{p.Key}_{nameof(p.Value.MaxMotorLevel)}");
                    p.Value.Time = PlayerPrefs.GetFloat(key: $"BillORumble_Event_{p.Key}_{nameof(p.Value.Time)}");
                    p.Value.MotorIndex = PlayerPrefs.GetInt(key: $"BillORumble_Event_{p.Key}_{nameof(p.Value.MotorIndex)}");
                }

                enable_Rolling = PlayerPrefs.GetInt(key: $"BillORumble_Toggles_{nameof(enable_Rolling)}", defaultValue: 1) == 1;
                enable_Grinding = PlayerPrefs.GetInt(key: $"BillORumble_Toggles_{nameof(enable_Grinding)}", defaultValue: 1) == 1;
                enable_PowerSlide = PlayerPrefs.GetInt(key: $"BillORumble_Toggles_{nameof(enable_PowerSlide)}", defaultValue: 1) == 1;

                powerSlideMultiplier = PlayerPrefs.GetFloat(key: $"BillORumble_General_{nameof(powerSlideMultiplier)}", defaultValue: 10);

                foreach (var k in surfaceRumbleLookup.Keys)
                {
                    surfaceRumbleLookup[key: k] = PlayerPrefs.GetFloat(key: $"BillORumble_Surface_{k}");
                }

                slideMultiplier = PlayerPrefs.GetFloat(key: $"BillORumble_Grinds_{nameof(slideMultiplier)}", defaultValue: 0.5f);
                grindMultiplier = PlayerPrefs.GetFloat(key: $"BillORumble_Grinds_{nameof(grindMultiplier)}", defaultValue: 1f);

                foreach (var k in grindRumbleLookup.Keys)
                {
                    grindRumbleLookup[key: k] = PlayerPrefs.GetFloat(key: $"BillORumble_Grinds_{k}");
                }
            }
        }

        private void OnDisable()
        {
            InputController.Instance.player.StopVibration();

            GameStateMachine.Instance.OnGameStateChanged -= GameStateMachine_OnGameStateChanged;
            EventManager.Instance.onGPEvent -= EventManager_onRunEvent;
        }

        private void GameStateMachine_OnGameStateChanged(Type prevState, Type newState)
        {
            if (newState == typeof(PlayState))
            {
                doRumble = true;
            }
            else
            {
                InputController.Instance.player.StopVibration();

                doRumble = false;
            }
        }

        private void OnDestroy()
        {
            SaveValues();
        }

        private void SaveValues()
        {
            PlayerPrefs.SetFloat(key: $"BillORumble_Settings_{nameof(maxJumpForce)}", value: maxJumpForce);
            PlayerPrefs.SetFloat(key: $"BillORumble_Settings_{nameof(maxLandingVelocity)}", value: maxLandingVelocity);
            PlayerPrefs.SetFloat(key: $"BillORumble_Settings_{nameof(maxGrindSpeed)}", value: maxGrindSpeed);
            PlayerPrefs.SetFloat(key: $"BillORumble_Settings_{nameof(maxGroundedSpeed)}", value: maxGroundedSpeed);

            foreach (var p in rumbleEvents)
            {
                PlayerPrefs.SetInt(key: $"BillORumble_Event_{p.Key}_{nameof(p.Value.Enabled)}", value: p.Value.Enabled ? 1 : 0);
                PlayerPrefs.SetFloat(key: $"BillORumble_Event_{p.Key}_{nameof(p.Value.MinMotorLevel)}", value: p.Value.MinMotorLevel);
                PlayerPrefs.SetFloat(key: $"BillORumble_Event_{p.Key}_{nameof(p.Value.MaxMotorLevel)}", value: p.Value.MaxMotorLevel);
                PlayerPrefs.SetFloat(key: $"BillORumble_Event_{p.Key}_{nameof(p.Value.Time)}", value: p.Value.Time);
                PlayerPrefs.SetInt(key: $"BillORumble_Event_{p.Key}_{nameof(p.Value.MotorIndex)}", value: p.Value.MotorIndex);
            }

            PlayerPrefs.SetInt(key: $"BillORumble_Toggles_{nameof(enable_Rolling)}", value: enable_Rolling ? 1 : 0);
            PlayerPrefs.SetInt(key: $"BillORumble_Toggles_{nameof(enable_Grinding)}", value: enable_Grinding ? 1 : 0);
            PlayerPrefs.SetInt(key: $"BillORumble_Toggles_{nameof(enable_PowerSlide)}", value: enable_PowerSlide ? 1 : 0);

            PlayerPrefs.SetFloat(key: $"BillORumble_General_{nameof(powerSlideMultiplier)}", value: powerSlideMultiplier);

            foreach (var p in surfaceRumbleLookup)
            {
                PlayerPrefs.SetFloat(key: $"BillORumble_Surface_{p.Key}", value: p.Value);
            }

            PlayerPrefs.SetFloat(key: $"BillORumble_Grinds_{nameof(grindMultiplier)}", value: grindMultiplier);
            PlayerPrefs.SetFloat(key: $"BillORumble_Grinds_{nameof(slideMultiplier)}", value: slideMultiplier);

            foreach (var p in grindRumbleLookup)
            {
                PlayerPrefs.SetFloat(key: $"BillORumble_Grinds_{p.Key}", value: p.Value);
            }

            PlayerPrefs.SetInt(key: "BillORumble_InitialValuesSaved", value: 1);
            PlayerPrefs.Save();
        }

        private void Update()
        {
            if (Input.GetKeyDown(key: KeyCode.F9))
            {
                showUI = !showUI;

                if (showUI == false)
                    SaveValues();
            }
        }

        private void FixedUpdate()
        {
            if (doRumble == false)
            {
                return;
            }

            if (isGrounded == false && PlayerController.Instance.PredictLanding())
            {
                isGrounded = true;
                var vel = PlayerController.Instance.boardController.boardRigidbody.velocity;
                TriggerEvent(RumbleEvents.Land, Mathf.Clamp01(value: vel.y / maxLandingVelocity));
                return;
            }

            if (isGrounded)
            {
                var speed_factor = Mathf.Clamp01(value: Mathf.Abs(f: PlayerController.Instance.boardController.boardRigidbody.velocity.magnitude) / maxGroundedSpeed);
                var surface_type = PlayerController.Instance.GetSurfaceTag(_tag: PlayerController.Instance.boardController.GetSurfaceTagString());
                var rumble = surfaceRumbleLookup[key: surface_type];

                var do_rumble = enable_Rolling;

                if (EventManager.Instance.IsPowerSliding && enable_PowerSlide)
                {
                    rumble *= powerSlideMultiplier;
                    do_rumble = true;
                    wasPowerSliding = true;
                }

                if (EventManager.Instance.IsManualling && enable_Rolling)
                {
                    rumble *= 0.5f;
                    do_rumble = true;
                }

                if (do_rumble)
                    InputController.Instance.player.SetVibration(motorIndex: 1, motorLevel: rumble * speed_factor);

                else if (wasPowerSliding && EventManager.Instance.IsPowerSliding == false)
                {
                    InputController.Instance.player.StopVibration();
                    wasPowerSliding = false;
                }
            }

            if (enable_Grinding)
            {
                if (isGrinding)
                {
                    var speed_factor = 1f - Mathf.Clamp01(value: Mathf.Abs(f: PlayerController.Instance.boardController.boardRigidbody.velocity.magnitude) / maxGrindSpeed);
                    var rumble = grindRumbleLookup[key: DeckSounds.Instance.grindState];
                    var v = rumble * speed_factor * (PlayerController.Instance.boardController.isSliding ? slideMultiplier : grindMultiplier);

                    InputController.Instance.player.SetVibration(motorIndex: 1, motorLevel: v);
                }
            }
        }

        private void EventManager_onRunEvent(GPEvent runEvent)
        {
            // TODO velocity/vibration scale for events types

            switch (runEvent)
            {
                case BailEvent bail_event:
                    isGrounded = false;
                    isGrinding = false;
                    TriggerEvent(RumbleEvents.Bail);
                    break;
                case BrakeEvent brake_event:
                    TriggerEvent(RumbleEvents.Brake);
                    break;
                case CatchEvent catch_event:
                    TriggerEvent(RumbleEvents.Catch);
                    break;
                case GrindEnterEvent grind_enter_event:
                    TriggerEvent(RumbleEvents.GrindEnter);
                    isGrinding = true;
                    break;
                case GrindExitEvent grind_exit_event:
                    isGrinding = false;
                    InputController.Instance.player.StopVibration();
                    break;
                case JumpEvent jump_event:
                    isGrounded = false;
                    isGrinding = false;
                    TriggerEvent(RumbleEvents.Pop, Mathf.Clamp01(value: jump_event.popForce / maxJumpForce));
                    break;
                case PushEvent push_event:
                    TriggerEvent(RumbleEvents.Push);
                    break;
            }
        }

        private void TriggerEvent(RumbleEvents evt, float mulitplier = 1f)
        {
            if (rumbleEvents.TryGetValue(evt, out var e) && e.Enabled)
                InputController.Instance.player.SetVibration(motorIndex: 0, motorLevel: e.GetMotorLevel(fraction: mulitplier), duration: e.Time);
        }

        private Rect windowRect = new Rect(x: 50, y: 50, width: 500, height: Screen.height - 100);
        private Vector2 scroll;

        private void OnGUI()
        {
            if (showUI == false)
                return;

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            windowRect = GUI.Window(id: 0,
                clientRect: windowRect,
                func: id =>
                {
                    GUILayout.BeginVertical(style: new GUIStyle {padding = new RectOffset(left: 10, right: 10, top: 50, bottom: 10)});
                    {
                        scroll = GUILayout.BeginScrollView(scroll);

                        GUILayout.BeginVertical(style: new GUIStyle(other: "box"));
                        GUILayout.Label(text: "Settings");
                        GUILayout.Label(text: "These settings are used to interpolate between the min/max motor levels of the various events and vibration settings. E.g. We work out what % of Max Jump Force your last jump achieved, and use that to scale the vibration accordingly.");

                        DrawField(label: "Max Jump Force", field: ref maxJumpForce);
                        DrawField(label: "Max Landing Velocity", field: ref maxLandingVelocity);
                        DrawField(label: "Max Speed", field: ref maxGroundedSpeed);
                        DrawField(label: "Max Grind Speed", field: ref maxGrindSpeed);
                        GUILayout.EndVertical();

                        GUILayout.BeginVertical(style: new GUIStyle(other: "box"));
                        GUILayout.Label(text: "Events");
                        foreach (RumbleEvents evt in Enum.GetValues(enumType: typeof(RumbleEvents)))
                        {
                            var data = rumbleEvents[key: evt];

                            GUILayout.BeginVertical(style: new GUIStyle(other: "box"));
                            data.Enabled = GUILayout.Toggle(data.Enabled, $"{evt}");
                            if (data.Enabled)
                            {
                                GUILayout.BeginHorizontal();
                                DrawField(label: "Min Motor Level", field: ref data.MinMotorLevel);
                                DrawField(label: "Max Motor Level", field: ref data.MaxMotorLevel);
                                GUILayout.EndHorizontal();
                                DrawField(label: "Motor Index", field: ref data.MotorIndex);
                                DrawField(label: "Time", field: ref data.Time);
                            }
                            GUILayout.EndVertical();

                        }
                        GUILayout.EndVertical();

                        GUILayout.Label(text: "Grinds");
                        GUILayout.BeginVertical(style: new GUIStyle(other: "box"));
                        enable_Grinding = GUILayout.Toggle(enable_Grinding, $"Enable Grind Rumble");

                        if (enable_Grinding)
                        {
                            DrawField(label: "Grind Multiplier", field: ref grindMultiplier);
                            DrawField(label: "Slide Multiplier", field: ref slideMultiplier);
                            foreach (DeckSounds.GrindState grind_state in Enum.GetValues(enumType: typeof(DeckSounds.GrindState)))
                            {
                                if (grind_state == DeckSounds.GrindState.none)
                                    continue;

                                GUILayout.BeginHorizontal();
                                GUILayout.Label(text: $"{grind_state}");
                                var st = GUILayout.TextField(text: grindRumbleLookup[key: grind_state].ToString(format: "0.000"));
                                grindRumbleLookup[key: grind_state] = float.Parse(s: st);
                                GUILayout.EndHorizontal();
                            }
                        }
                        GUILayout.EndVertical();

                        GUILayout.Label(text: "Surfaces");
                        GUILayout.BeginVertical(style: new GUIStyle(other: "box"));
                        enable_Rolling = GUILayout.Toggle(enable_Rolling, $"Enable Rolling Surface Rumble");
                        enable_PowerSlide = GUILayout.Toggle(enable_PowerSlide, $"Enable PowerSlide Rumble");

                        if (enable_Rolling || enable_PowerSlide)
                        {

                            DrawField(label: "PowerSlide Multiplier", field: ref powerSlideMultiplier);

                            foreach (PlayerController.SurfaceTags surface_tag in Enum.GetValues(enumType: typeof(PlayerController.SurfaceTags)))
                            {
                                if (surface_tag == PlayerController.SurfaceTags.None)
                                    continue;

                                GUILayout.BeginHorizontal();
                                GUILayout.Label(text: $"{surface_tag}");
                                var st = GUILayout.TextField(text: surfaceRumbleLookup[key: surface_tag].ToString(format: "0.000"));
                                surfaceRumbleLookup[key: surface_tag] = float.Parse(s: st);
                                GUILayout.EndHorizontal();
                            }
                        }
                        GUILayout.EndVertical();
                    }
                    PlayerPrefs.Save();

                    if (GUILayout.Button(text: "Save & Close"))
                    {
                        SaveValues();
                        showUI = false;
                        Cursor.visible = false;
                    }

                    GUILayout.EndArea();
                    GUILayout.EndScrollView();

                    GUI.DragWindow(position: windowRect);
                },
                text: "Bill O'Rumble", style: new GUIStyle(other: "box"));
        }

        private void DrawField(string label, ref float field)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(text: label);
            var v = GUILayout.TextField(text: field.ToString(format: "0.000"));
            field = float.Parse(s: v);
            GUILayout.EndHorizontal();
        }

        private void DrawField(string label, ref int field)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(text: label);
            var v = GUILayout.TextField(text: field.ToString());
            field = int.Parse(s: v);
            GUILayout.EndHorizontal();
        }
    }
}