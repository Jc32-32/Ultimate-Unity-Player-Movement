using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

public class Player_Movement : MonoBehaviour
{
    [Header("Player State")]
    public PlayerState playerState;
    public enum PlayerState
    {
        idle,
        walking,
        sprinting,
        crouching,
        sliding,
        airborn   
    }

    [Header("Movement Settings")]
    public bool move_Enabled;
    public float walkSpeed;
    [Range(0.01f, 2)] public float airSpeedMultiplier;
    public float groundDrag;
    public float airDrag;
    private InputAction move_InputAction;
    private Vector3 move_Direction;
    private Vector2 move_Input;

    [Header("Sprinting")]
    public bool sprint_Enabled;
    public float sprint_Speed;
    private InputAction sprint_InputAction;

    [Header("Jump Settings")]
    public bool jump_Enabled;
    public float jump_Force;
    private InputAction jump_InputAction;
    private bool holdingJump, canJump;

    [Header("Crouch & Slide Settings")]
    public bool crouch_Enabled;
    public float crouch_Height;
    public float crouch_Speed;
    private InputAction crouch_InputAction;

    [Space(15)] // Slide
    public bool slide_Enabled;
    public bool slide_InAirEnabled; // Can we slide while were jumping?
    public float slide_LaunchForce;
    public float slide_StartForce;
    public float slide_ForceDecayMultiplier;
    public float slide_MaxTime; // Air and Ground

    private Vector3 slide_Direction;
    private float slide_Time = 0;
    private float slide_Force;

    [Header("Ground Check & Slope Settings")]
    public float player_Height;
    public LayerMask ground_Mask;
    private Vector3 ground_Normal;
    private float ground_Distance;

    [Space(5)] // Slope
    public float slope_MaxAngle;
    public float slope_StickForce;
    private bool on_Slope, on_UpHill;
    
    [Header("References")]
    public Transform orientation;

    private Rigidbody rb;

    private float speed;

    void Awake()
    {
        AssignInputActions();
    }

    // Just assigns input action variables
    private void AssignInputActions()
    {   
        // Move Input Check
        if(move_Enabled && InputSystem.actions.FindAction("Move") == null)
        {
            Debug.LogError("Missing Keybind: " + move_InputAction.ToString() + " | Please create a 'Move' Input Action or disable action");
            move_Enabled = false;
        }
        else if(move_Enabled)
        {
            move_InputAction = InputSystem.actions.FindAction("Move");
        }    

        // Sprint Input Check
        if(sprint_Enabled && InputSystem.actions.FindAction("Sprint") == null)
        {
            Debug.LogError("Missing Keybind: " + sprint_InputAction.ToString() + " | Please create a 'Sprint' Input Action or disable action");
            sprint_Enabled = false;
        }
        else if(sprint_Enabled)
        {
            sprint_InputAction = InputSystem.actions.FindAction("Sprint");

            sprint_InputAction.performed += Sprint_Input;
            sprint_InputAction.canceled += Sprint_Input;
        }

        // Jump Input Check
        if(jump_Enabled && InputSystem.actions.FindAction("Jump") == null)
        {
            Debug.LogError("Missing Keybind: " + jump_InputAction.ToString() + " | Please create a 'Jump' Input Action or disable action");
            jump_Enabled = false;
        }
        else if(jump_Enabled)
        {
            jump_InputAction = InputSystem.actions.FindAction("Jump");

            jump_InputAction.performed += Jump_Input;
            jump_InputAction.canceled += Jump_Input;

            canJump = true;
        }

        // Crouch/Slide Input Check
        if(crouch_Enabled && InputSystem.actions.FindAction("Crouch") == null)
        {
            Debug.LogError("Missing Keybind: " + crouch_InputAction.ToString() + " | Please create a 'Crouch' Input Action or disable action");
            crouch_Enabled = false;
        }
        else if(crouch_Enabled)
        {
            crouch_InputAction = InputSystem.actions.FindAction("Crouch");

            crouch_InputAction.performed += Crouch_Input;
            crouch_InputAction.canceled += Crouch_Input;
        }
        else if(!crouch_Enabled && slide_Enabled)
        {
            // Makes sure if slide is enabled to also enabled crouch
            crouch_Enabled = true;
            Debug.LogWarning("Conflicting Actions: " + crouch_Enabled.ToString() + " must be enabled to allow " + slide_Enabled.ToString() + " to function properly. " + crouch_Enabled.ToString() + " has been set to true for your convinence. Please adjust corresponding varriables as needed.");
        }
    } 

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }   

    void FixedUpdate()
    {
        // Finds move direction based on our orientation and WASD inputs
        move_Direction = orientation.forward * move_Input.y + orientation.right * move_Input.x;

        // Move Player
        if(Grounded())
        {
            // Adjusts Move Direction for slopes
            if(ground_Normal != Vector3.up)
            {
                // Checks if player can walk up slope
                if(Vector3.Angle(Vector3.up, ground_Normal) > slope_MaxAngle) return;

                // Sticks Player to Slope
                rb.AddForce(-ground_Normal * slope_StickForce, ForceMode.Force);

                // Adjusts move Direction to slope
                if(slide_Enabled && playerState == PlayerState.sliding)
                {
                    slide_Direction = Vector3.ProjectOnPlane(slide_Direction, ground_Normal);

                    // Checks if we are moving up hill
                    on_UpHill = Vector3.SignedAngle(transform.forward, slide_Direction, transform.right) < 0;
                }    
                else
                {
                    move_Direction = Vector3.ProjectOnPlane(move_Direction, ground_Normal);

                    // Checks if we are moving up hill
                    on_UpHill = Vector3.SignedAngle(transform.forward, move_Direction, transform.right) < 0;
                }    

                on_Slope = true;
            }
            else
            {
                on_UpHill = false;
                on_Slope = false;
            }

            // Grounded Movement
            if(slide_Enabled && playerState == PlayerState.sliding)
                rb.AddForce(slide_Direction * slide_Force * 5, ForceMode.Force);
            else
                rb.AddForce(move_Direction * speed * 5, ForceMode.Force);
        }  
        else
        {
            // Air Movement
            if(slide_Enabled && playerState == PlayerState.sliding)
                rb.AddForce(slide_Direction * slide_Force * airSpeedMultiplier, ForceMode.Force);
            else
                rb.AddForce(move_Direction * speed * airSpeedMultiplier, ForceMode.Force);
        }
    }

    void Update()
    {   
        // Update Inputs
        GetInput();

        // Updates PlayerState variable
        StateMachine();

        // Makes sure player doesn't move faster than the players max speed
        SpeedControl();
    }

    // All Inputs and Keybinds
    private void GetInput()
    {
        // Move Input
        if(move_Enabled) move_Input = move_InputAction.ReadValue<Vector2>();

        if(jump_Enabled && holdingJump) Jump();
    }

    void Sprint_Input(InputAction.CallbackContext ctx)
    {
        if(ctx.performed)
        {
            // Hold
            if(ctx.interaction is HoldInteraction)
            {
                speed = sprint_Speed;
            }

            // Toggle
            else
            {
                if(playerState == PlayerState.sprinting) speed = walkSpeed * 5;
                else speed = sprint_Speed;
            }
        }

        if(ctx.canceled)
        {
            if(ctx.interaction is HoldInteraction)
            {
                speed = walkSpeed;
            }
        }
    }

    void Jump_Input(InputAction.CallbackContext ctx)
    {
        if(ctx.performed)
        {
            if(ctx.interaction is HoldInteraction)
            {
                holdingJump = true;
                return;
            }

            Jump();
        }

        if(ctx.canceled)
        {
            if(ctx.interaction is HoldInteraction)
            {
                holdingJump = false;
            }
        }
    }

    private void Jump()
    {
        if(Grounded() && canJump)
        {
            canJump = false;

            // Jump
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jump_Force, ForceMode.Impulse);

            Invoke(nameof(ResetJump), .1f);
        }
    }

    private void ResetJump()
    {
        canJump = true;
    }

    private void Crouch_Input(InputAction.CallbackContext ctx)
    {
        if(ctx.performed)
        {
            // Checks if we can Crouch
            if(playerState == PlayerState.airborn && !slide_InAirEnabled) return;
            if(playerState == PlayerState.crouching || playerState == PlayerState.sliding) return;

            // Crouch
            if(speed <= walkSpeed)
            {
                Crouch();
            }
            
            // Slide
            else if(slide_Enabled && slide_Time == 0 && slide_Force == 0)
            {
                Slide();
            }
        }

        if(ctx.canceled)
        {
            if(playerState == PlayerState.crouching) Stand_Up();
        }
    }

    private void Crouch()
    {
        transform.localScale = new Vector3(transform.localScale.x, crouch_Height/player_Height, transform.localScale.z);
        rb.AddForce(Vector3.down * 5, ForceMode.Impulse);

        speed = crouch_Speed; 
    }

    private void Slide()
    {   
        transform.localScale = new Vector3(transform.localScale.x, crouch_Height/player_Height, transform.localScale.z);

        slide_Direction = move_Direction;
        rb.AddForce(slide_Direction * slide_LaunchForce, ForceMode.Impulse);

        slide_Force = slide_StartForce;
    }   

    private void Stand_Up()
    {
        if(Physics.Raycast(transform.position, Vector3.up, player_Height - crouch_Height))
        {
            InvokeRepeating(nameof(Check_Stand_Up), 0, Time.deltaTime);
            return;
        }

        transform.localScale = new Vector3(transform.localScale.x, player_Height/2, transform.localScale.z);

        if(slide_Enabled)
        {
            slide_Time = 0;
            slide_Force = 0;
        }
    }

    private void Check_Stand_Up()
    {
        if(Physics.Raycast(transform.position, Vector3.up, player_Height - (crouch_Height/2))) return;

        Stand_Up();
        CancelInvoke(nameof(Check_Stand_Up));
    }

    private void StateMachine()
    {
        // On Ground States
        if(Grounded())
        {
            // Adjusts Drag
            if(rb.linearDamping != groundDrag) rb.linearDamping = groundDrag;

            // Crouch & Sliding (If Enabled)
            if(ground_Distance < player_Height/2 && crouch_Enabled)
            {   
                // Slide (If Enabled)
                if(slide_Enabled && slide_Force > 0)
                {
                    if(slide_Time < slide_MaxTime)
                    {
                        // Allows player to slide down a slope indefinetly
                        if(!on_Slope || on_UpHill)
                        {
                            slide_Time += Time.deltaTime;

                            // Slows down slide over time
                            slide_Force -= Time.deltaTime * slide_ForceDecayMultiplier;
                        }

                        playerState = PlayerState.sliding;
                    }
                    else
                    {
                        Stand_Up();
                    }

                    return;
                }   
                else
                {
                    playerState = PlayerState.crouching;
                }

                return;
            }

            // Sprinting (If Enabled)
            if(sprint_Enabled && speed == sprint_Speed)
            {
                playerState = PlayerState.sprinting;

                return;
            }
            // Walking
            if(move_Direction.magnitude > 0)
            {
                playerState = PlayerState.walking;
                speed = walkSpeed;
            }
            // Idle
            else
            {
                playerState = PlayerState.idle;
                speed = walkSpeed;
            }
        }

        // Air States
        else
        {
            playerState = PlayerState.airborn;

            // Adjusts Slide Direction in Air (IF enabled)
            if(slide_InAirEnabled) slide_Direction = move_Direction;

            // Adjusts Drag
            if(rb.linearDamping != airDrag) rb.linearDamping = airDrag;
        }
    }

    private bool Grounded()
    {
        // Assigns player height
        float height = 0;
        if(crouch_Enabled && transform.localScale.y < 1)
        {
            height = crouch_Height/2;
        }
        else
        {
            height = player_Height/2;
        }

        // Grounded
        if(Physics.Raycast(transform.position, Vector3.down, out RaycastHit hitInfo, .2f + height, ground_Mask))
        {
            ground_Normal = hitInfo.normal;
            ground_Distance = (transform.position - hitInfo.point).magnitude;
            return true; 
        }
        else return false;
    }

    private void SpeedControl()
    {
        // Limits Player Speed
        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        if (flatVel.magnitude > speed)
        {
            Vector3 limitedVel = flatVel.normalized * speed;
            rb.linearVelocity = new Vector3(limitedVel.x, rb.linearVelocity.y, limitedVel.z);
        }
    }
}


// Styleized Editor Code
[CustomEditor(typeof(Player_Movement))]
public class Player_Movement_Editor : Editor
{
    // DEBUG
    static bool enabled_StyleMenu = true;

    public override void OnInspectorGUI()
    {
        Player_Movement pm = (Player_Movement)target;

        // Header Style
        GUIStyle style_Header = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            margin = new RectOffset(0, 0, 20, 4)
        };

        // DEBUG
        //enabled_StyleMenu = EditorGUILayout.Toggle("Enable Styled Menu (DEBUG)", enabled_StyleMenu);

        EditorGUILayout.Space(15);

        if(enabled_StyleMenu)
        {
            // Movement Settings
            EditorGUILayout.LabelField("Movement Settings", style_Header);
            pm.move_Enabled = EditorGUILayout.Toggle("Movement Enabled", pm.move_Enabled);
            if(pm.move_Enabled)
            {
                // Walk Speed  
                pm.walkSpeed = EditorGUILayout.FloatField("Walk Speed",pm.walkSpeed);

                // Drag
                EditorGUILayout.BeginHorizontal();

                // Ground Drag
                EditorGUILayout.LabelField("Ground Drag", GUILayout.Width(70));
                pm.groundDrag = EditorGUILayout.FloatField(pm.groundDrag, GUILayout.Width(60));

                // Air Drag
                EditorGUILayout.LabelField("Air Drag", GUILayout.Width(70));
                pm.airDrag = EditorGUILayout.FloatField(pm.airDrag, GUILayout.Width(60));

                EditorGUILayout.EndHorizontal();

                pm.airSpeedMultiplier = EditorGUILayout.Slider("Air Speed Multiplier" ,pm.airSpeedMultiplier, 0, 2);
            }
            EditorGUILayout.Space(30);

            // Sprint Settings
            EditorGUILayout.LabelField("Sprint Settings", style_Header);
            pm.sprint_Enabled = EditorGUILayout.Toggle("Sprinting Enabled", pm.sprint_Enabled);
            if(pm.sprint_Enabled)
            {
                pm.sprint_Speed = EditorGUILayout.FloatField("Sprint Speed", pm.sprint_Speed);
            }
            EditorGUILayout.Space(30);

            // Jump Settings
            EditorGUILayout.LabelField("Jump Settings", style_Header);
            pm.jump_Enabled = EditorGUILayout.Toggle("Jump Enabled", pm.jump_Enabled);
            if(pm.jump_Enabled)
            {
                pm.jump_Force = EditorGUILayout.FloatField("Jump Force", pm.jump_Force);
            }
            EditorGUILayout.Space(30);

            // Crouch Settings
            EditorGUILayout.LabelField("Crouch Settings", style_Header);
            pm.crouch_Enabled = EditorGUILayout.Toggle("Crouch Enabled", pm.crouch_Enabled);
            if(pm.crouch_Enabled)
            {
                pm.crouch_Height = EditorGUILayout.FloatField("Crouch Force", pm.crouch_Height);

                pm.crouch_Speed = EditorGUILayout.FloatField("Crouch Speed", pm.crouch_Speed);
                EditorGUILayout.Space(15);

                // Slide Settings
                EditorGUILayout.LabelField("Slide Settings", style_Header);
                pm.slide_Enabled = EditorGUILayout.Toggle("Slide Enabled", pm.slide_Enabled);
                if(pm.slide_Enabled)
                {
                    pm.slide_InAirEnabled = EditorGUILayout.Toggle("Slide In Air Enabled", pm.slide_InAirEnabled);

                    pm.slide_LaunchForce = EditorGUILayout.FloatField("Launch Force", pm.slide_LaunchForce);

                    pm.slide_StartForce = EditorGUILayout.FloatField("Slide Force", pm.slide_StartForce);

                    pm.slide_MaxTime = EditorGUILayout.FloatField("Slide Duration", pm.slide_MaxTime);

                    pm.slide_ForceDecayMultiplier = EditorGUILayout.Slider("Slide Force Decay Multiplier", pm.slide_ForceDecayMultiplier, 1, 10);
                }
            }
            EditorGUILayout.Space(30);

            // Slope & Ground Check Settings
            EditorGUILayout.LabelField("Ground Check & Slope Settings", style_Header);
            pm.player_Height = EditorGUILayout.FloatField("Player Height", pm.player_Height);
            // Ground Layer
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ground_Mask"));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(5);
            // Slope
            pm.slope_MaxAngle = EditorGUILayout.FloatField("Max Slope Angle", pm.slope_MaxAngle);
            pm.slope_StickForce = EditorGUILayout.FloatField("Slope Stick Force", pm.slope_StickForce);
        }
        else base.OnInspectorGUI();
    }
}