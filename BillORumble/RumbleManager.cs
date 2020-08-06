using System;
using System.Collections;
using System.Collections.Generic;
using GameManagement;
using UnityEngine;

namespace BillORumble
{
    public class RumbleManager : MonoBehaviour
    {
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

        public class RumbleSettings
        {
            public bool Enabled = true;
            public float MinMotorLevel;
            public float MaxMotorLevel;
            public int MotorIndex = 0;
            public float MinTime;
            public float MaxTime;

            public float GetMotorLevel(float fraction)
            {
                return Mathf.Lerp(a: MinMotorLevel, b: MaxMotorLevel, t: fraction);
            }

            public float GetTime(float fraction)
            {
                return Mathf.Lerp(a: MinTime, b: MaxTime, t: fraction);
            }

            public RumbleSettings(float motor_level, float min_time) 
            {
                MinMotorLevel = motor_level;
                MaxMotorLevel = motor_level;
                MinTime = min_time;
                MaxTime = min_time;
            }

            public RumbleSettings(float min_motor_level, float max_motor_level, float min_time)
            {
                MinMotorLevel = min_motor_level;
                MaxMotorLevel = max_motor_level;
                MinTime = min_time;
	            MaxTime = min_time;
            }

            public RumbleSettings(float min_motor_level, float max_motor_level, float min_time, float max_time)
            {
	            MinMotorLevel = min_motor_level;
	            MaxMotorLevel = max_motor_level;
	            MinTime = min_time;
	            MaxTime = max_time;
            }
        }

		// config

	    private float maxGroundedSpeed = 60;
	    private float maxGrindSpeed = 50;
	    private float maxDropHeight = 10;
	    private float maxJumpForce = 9f;

	    private Dictionary<RumbleEvents, RumbleSettings> rumbleEvents;
	    private Dictionary<PlayerController.SurfaceTags, float> surfaceRumbleLookup;
	    private Dictionary<DeckSounds.GrindState, float> grindRumbleLookup;

        private bool enable_Rolling;
        private bool enable_Grinding;
        private bool enable_PowerSlide;

        private float powerSlideMultiplier = 10;
        private float slideMultiplier = 0.5f;
        private float grindMultiplier = 1f;

		// state

	    private bool showUI;

        private bool doRumble = true;
        private bool isSkating;
        private bool isGrinding;
        private bool isGrounded;
        private bool wasPowerSliding;
	    private float maxHeight = Mathf.NegativeInfinity;
	    private float dropHeight;

		// helpers

	    private Vector3 boardVelocity => PlayerController.Instance.boardController.boardRigidbody.velocity;
	    private float boardHeight => PlayerController.Instance.boardController.boardTransform.position.y;

        private void OnEnable()
        {
	        InitValues();

            GameStateMachine.Instance.OnGameStateChanged += GameStateMachine_OnGameStateChanged;
            EventManager.Instance.onGPEvent += EventManager_onRunEvent;

            if (PlayerPrefs.HasKey(key: "BillORumble_InitialValuesSaved") == false)
            {
                SaveValues();
            }
            else
            {
	            LoadValues();
            }
        }

	    private void LoadValues()
	    {
		    maxJumpForce = PlayerPrefs.GetFloat(key: $"BillORumble_Settings_{nameof(maxJumpForce)}");
	        maxDropHeight = PlayerPrefs.GetFloat(key: $"BillORumble_Settings_{nameof(maxDropHeight)}");
            maxGrindSpeed = PlayerPrefs.GetFloat(key: $"BillORumble_Settings_{nameof(maxGrindSpeed)}");
            maxGroundedSpeed = PlayerPrefs.GetFloat(key: $"BillORumble_Settings_{nameof(maxGroundedSpeed)}");

            foreach (var p in rumbleEvents)
            {
                p.Value.Enabled = PlayerPrefs.GetInt(key: $"BillORumble_Event_{p.Key}_{nameof(p.Value.Enabled)}") == 1;
                p.Value.MinMotorLevel = PlayerPrefs.GetFloat(key: $"BillORumble_Event_{p.Key}_{nameof(p.Value.MinMotorLevel)}");
                p.Value.MaxMotorLevel = PlayerPrefs.GetFloat(key: $"BillORumble_Event_{p.Key}_{nameof(p.Value.MaxMotorLevel)}");
                p.Value.MinTime = PlayerPrefs.GetFloat(key: $"BillORumble_Event_{p.Key}_{nameof(p.Value.MinTime)}");
                p.Value.MaxTime = PlayerPrefs.GetFloat(key: $"BillORumble_Event_{p.Key}_{nameof(p.Value.MaxTime)}");
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

		private void SaveValues()
        {
            PlayerPrefs.SetFloat(key: $"BillORumble_Settings_{nameof(maxJumpForce)}", value: maxJumpForce);
            PlayerPrefs.SetFloat(key: $"BillORumble_Settings_{nameof(maxDropHeight)}", value: maxDropHeight);
            PlayerPrefs.SetFloat(key: $"BillORumble_Settings_{nameof(maxGrindSpeed)}", value: maxGrindSpeed);
            PlayerPrefs.SetFloat(key: $"BillORumble_Settings_{nameof(maxGroundedSpeed)}", value: maxGroundedSpeed);

            foreach (var p in rumbleEvents)
            {
                PlayerPrefs.SetInt(key: $"BillORumble_Event_{p.Key}_{nameof(p.Value.Enabled)}", value: p.Value.Enabled ? 1 : 0);
                PlayerPrefs.SetFloat(key: $"BillORumble_Event_{p.Key}_{nameof(p.Value.MinMotorLevel)}", value: p.Value.MinMotorLevel);
                PlayerPrefs.SetFloat(key: $"BillORumble_Event_{p.Key}_{nameof(p.Value.MaxMotorLevel)}", value: p.Value.MaxMotorLevel);
                PlayerPrefs.SetFloat(key: $"BillORumble_Event_{p.Key}_{nameof(p.Value.MinTime)}", value: p.Value.MinTime);
                PlayerPrefs.SetFloat(key: $"BillORumble_Event_{p.Key}_{nameof(p.Value.MaxTime)}", value: p.Value.MaxTime);
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

        private void InitValues()
        {
	        maxGroundedSpeed = 60;
	        maxGrindSpeed = 50;
	        maxDropHeight = 8f;
	        maxJumpForce = 9f;

	        enable_Rolling = false;
	        enable_Grinding = true;
	        enable_PowerSlide = true;

	        powerSlideMultiplier = 10;
	        slideMultiplier = 0.5f;
	        grindMultiplier = 1f;

	        rumbleEvents = new Dictionary<RumbleEvents, RumbleSettings>()
	        {
		        {RumbleEvents.GrindEnter, new RumbleSettings(motor_level: 0.2f, min_time: .15f)},
		        {RumbleEvents.Pop, new RumbleSettings(min_motor_level: 0.2f, max_motor_level: 0.5f, min_time: 0.2f)},
		        {RumbleEvents.Land, new RumbleSettings(min_motor_level: 0.3f, max_motor_level: 10f, min_time: 0.2f, max_time: 1f)},
		        {RumbleEvents.Push, new RumbleSettings(motor_level: 0.18f, min_time: 0.4f)},
		        {RumbleEvents.Catch, new RumbleSettings(motor_level: 0.2f, min_time: 0.15f)},
		        {RumbleEvents.Bail, new RumbleSettings(motor_level: 1, min_time: 0.8f)},
		        {RumbleEvents.Brake, new RumbleSettings(motor_level: 0.2f, min_time: 0.3f)},
	        };

	        surfaceRumbleLookup = new Dictionary<PlayerController.SurfaceTags, float>()
	        {
		        {PlayerController.SurfaceTags.None, 0},
		        {PlayerController.SurfaceTags.Brick, 0.3f},
		        {PlayerController.SurfaceTags.Concrete, 0.12f},
		        {PlayerController.SurfaceTags.Grass, 0.4f},
		        {PlayerController.SurfaceTags.Tarmac, 0.13f},
		        {PlayerController.SurfaceTags.Wood, 0.185f}
	        };

	        grindRumbleLookup = new Dictionary<DeckSounds.GrindState, float>()
	        {
		        {DeckSounds.GrindState.concrete, 0.3f},
		        {DeckSounds.GrindState.metal, 0.1f},
		        {DeckSounds.GrindState.none, 0},
		        {DeckSounds.GrindState.wood, 0.2f},
	        };

			SaveValues();
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
            if (doRumble == false || isSkating == false)
            {
                return;
            }

	        if (isGrounded == false)
	        {
		        if (boardHeight > maxHeight)
		        {
			        maxHeight = boardHeight;
		        }
	        }
			
            if (isGrounded == false && PlayerController.Instance.PredictLanding())
            {
				isGrounded = true;

	            dropHeight = Mathf.Abs(maxHeight - boardHeight);
	            maxHeight = Mathf.NegativeInfinity;

	            var factor = Mathf.Clamp01(dropHeight / maxDropHeight);

                TriggerEvent(RumbleEvents.Land, factor);

                return;
            }

            if (isGrounded)
            {
                var speed_factor = Mathf.Clamp01(value: Mathf.Abs(f: boardVelocity.magnitude) / maxGroundedSpeed);
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
                    var speed_factor = 1f - Mathf.Clamp01(value: Mathf.Abs(f: boardVelocity.magnitude) / maxGrindSpeed);
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
                    isSkating = false;
                    isGrounded = false;
                    isGrinding = false;
	                InputController.Instance.player.StopVibration();
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
	                InputController.Instance.player.StopVibration();
					TriggerEvent(RumbleEvents.Pop, Mathf.Clamp01(value: jump_event.popForce / maxJumpForce));
                    break;
                case PushEvent push_event:
                    TriggerEvent(RumbleEvents.Push);
                    break;
	            case RespawnEvent respawn_event:
		            InputController.Instance.player.StopVibration();
		            isSkating = true;
		            break;
            }
        }

        private void TriggerEvent(RumbleEvents evt, float fraction = 1f)
        {
            if (rumbleEvents.TryGetValue(evt, out var e) && e.Enabled)
                InputController.Instance.player.SetVibration(motorIndex: 0, motorLevel: e.GetMotorLevel(fraction: fraction), duration: e.GetTime(fraction: fraction));
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
                        DrawField(label: "Max Drop Height", field: ref maxDropHeight);
                        DrawField(label: "Max Speed", field: ref maxGroundedSpeed);
                        DrawField(label: "Max Grind Speed", field: ref maxGrindSpeed);

						GUILayout.Label($"Max Height = {maxHeight}");
						GUILayout.Label($"Last Drop Height = {dropHeight}");

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
                                DrawField(label: "Min Time", field: ref data.MinTime);
                                DrawField(label: "Max Time", field: ref data.MaxTime);
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
					
                    if (GUILayout.Button(text: "Reset to Defaults"))
                    {
						InitValues();
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
};