using System;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerController : MonoBehaviour, IPlayerController
{
    [SerializeField] private ScriptableStats _stats;
    [SerializeField] private LayerMask Interactuable;
    [SerializeField] private LayerMask EnemyLayer; 
    [SerializeField] TMP_Text pickablesText;
    [SerializeField] float interactuableRadius = 1.8f;

    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackCooldown = 0.4f;
    [SerializeField] private int attackDamage = 1;

    private int pickableAmount = 0;
    private Rigidbody2D _rb;
    private CapsuleCollider2D _col;
    private FrameInput _frameInput;
    private Vector2 _frameVelocity;
    private bool _cachedQueryStartInColliders;

    #region Interface
    public Vector2 FrameInput => _frameInput.Move;
    public event Action<bool, float> GroundedChanged;
    public event Action Jumped;
    public event Action Dashed;
    public event Action Attacked; 
    #endregion

    private float _time;
    private float _lastAttackTime;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<CapsuleCollider2D>();
        _cachedQueryStartInColliders = Physics2D.queriesStartInColliders;
    }

    private void Start() => updatePickableText();

    private void updatePickableText() => pickablesText.text = pickableAmount.ToString();

    public void addPickable(int add)
    {
        pickableAmount += add;
        updatePickableText();
    }

    private void checkInteractuables()
    {
        Collider2D col = Physics2D.OverlapCircle(transform.position, interactuableRadius, Interactuable);
        if (col != null)
        {
            col.GetComponent<Animator>().SetBool("IsOpened", true);
        }
    }

    private void Update()
    {
        _time += Time.deltaTime;
        GatherInput();
    }

    private void GatherInput()
    {
        _frameInput = new FrameInput
        {
            JumpDown = Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.C),
            JumpHeld = Input.GetButton("Jump") || Input.GetKey(KeyCode.C),
            DashDown = Input.GetButtonDown("Fire3") || Input.GetKeyDown(KeyCode.LeftShift),
            AttackDown = Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.X), 
            Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"))
        };

        if (_stats.SnapInput)
        {
            _frameInput.Move.x = Mathf.Abs(_frameInput.Move.x) < _stats.HorizontalDeadZoneThreshold ? 0 : Mathf.Sign(_frameInput.Move.x);
            _frameInput.Move.y = Mathf.Abs(_frameInput.Move.y) < _stats.VerticalDeadZoneThreshold ? 0 : Mathf.Sign(_frameInput.Move.y);
        }
        if (_frameInput.Move.x != 0f)
        {
            _lastMoveDirection = Mathf.Sign(_frameInput.Move.x);
        }
        if (_frameInput.JumpDown)
        {
            _jumpToConsume = true;
            _timeJumpWasPressed = _time;
        }

        if (_frameInput.DashDown)
            TryStartDash();

        if (_frameInput.AttackDown)
            TryAttack();

        if (Input.GetButtonDown("Submit"))
            checkInteractuables();
    }

    private void FixedUpdate()
    {
        CheckCollisions();

        HandleDash();
        if (!_isDashing)
        {
            HandleJump();
            HandleDirection();
            HandleGravity();
        }

        ApplyMovement();
    }

    #region Collisions
    private float _frameLeftGrounded = float.MinValue;
    private bool _grounded;

    private void CheckCollisions()
    {
        Physics2D.queriesStartInColliders = false;

        bool groundHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.down, _stats.GrounderDistance, ~_stats.PlayerLayer);
        bool ceilingHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.up, _stats.GrounderDistance, ~_stats.PlayerLayer);

        if (ceilingHit) _frameVelocity.y = Mathf.Min(0, _frameVelocity.y);

        if (!_grounded && groundHit)
        {
            _grounded = true;
            _coyoteUsable = true;
            _bufferedJumpUsable = true;
            _endedJumpEarly = false;
            _remainingJumps = _stats.ExtraJumps;
            GroundedChanged?.Invoke(true, Mathf.Abs(_frameVelocity.y));
        }
        else if (_grounded && !groundHit)
        {
            _grounded = false;
            _frameLeftGrounded = _time;
            GroundedChanged?.Invoke(false, 0);
        }

        Physics2D.queriesStartInColliders = _cachedQueryStartInColliders;
    }
    #endregion

    #region Jumping
    private bool _jumpToConsume;
    private bool _bufferedJumpUsable;
    private bool _endedJumpEarly;
    private bool _coyoteUsable;
    private float _timeJumpWasPressed;
    private int _remainingJumps;

    private bool HasBufferedJump => _bufferedJumpUsable && _time < _timeJumpWasPressed + _stats.JumpBuffer;
    private bool CanUseCoyote => _coyoteUsable && !_grounded && _time < _frameLeftGrounded + _stats.CoyoteTime;

    private void HandleJump()
    {
        if (!_endedJumpEarly && !_grounded && !_frameInput.JumpHeld && _rb.linearVelocity.y > 0)
            _endedJumpEarly = true;

        if (!_jumpToConsume && !HasBufferedJump) return;

        if (_grounded || CanUseCoyote)
        {
            ExecuteJump(_stats.JumpPower);
        }
        else if (_remainingJumps > 0)
        {
            ExecuteJump(_stats.DoubleJumpPower);
            _remainingJumps--;
        }

        _jumpToConsume = false;
    }

    private void ExecuteJump(float power)
    {
        _endedJumpEarly = false;
        _timeJumpWasPressed = 0;
        _bufferedJumpUsable = false;
        _coyoteUsable = false;
        _frameVelocity.y = power;
        Jumped?.Invoke();
    }
    #endregion

    #region Horizontal
    private void HandleDirection()
    {
        if (_frameInput.Move.x == 0)
        {
            var deceleration = _grounded ? _stats.GroundDeceleration : _stats.AirDeceleration;
            _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, 0, deceleration * Time.fixedDeltaTime);
        }
        else
        {
            _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, _frameInput.Move.x * _stats.MaxSpeed, _stats.Acceleration * Time.fixedDeltaTime);
        }
    }
    #endregion

    #region Gravity
    private void HandleGravity()
    {
        if (_grounded && _frameVelocity.y <= 0f)
        {
            _frameVelocity.y = _stats.GroundingForce;
        }
        else
        {
            var inAirGravity = _stats.FallAcceleration;
            if (_endedJumpEarly && _frameVelocity.y > 0)
                inAirGravity *= _stats.JumpEndEarlyGravityModifier;

            _frameVelocity.y = Mathf.MoveTowards(_frameVelocity.y, -_stats.MaxFallSpeed, inAirGravity * Time.fixedDeltaTime);
        }
    }
    #endregion

    #region Dash
    private bool _isDashing;
    private float _dashEndTime;
    private float _lastDashTime;

    private void TryStartDash()
    {
        if (_isDashing || _time < _lastDashTime + _stats.DashCooldown) return;

        _isDashing = true;
        _dashEndTime = _time + _stats.DashDuration;
        _lastDashTime = _time;

        float dir = _frameInput.Move.x != 0 ? Mathf.Sign(_frameInput.Move.x) : Mathf.Sign(transform.localScale.x);
        _frameVelocity = new Vector2(dir * _stats.DashSpeed, 0f);
        Dashed?.Invoke();
    }

    private void HandleDash()
    {
        if (!_isDashing) return;

        _rb.linearVelocity = _frameVelocity;

        if (_time >= _dashEndTime)
        {
            _isDashing = false;
        }
    }
    #endregion

    #region Attack

    private float _lastMoveDirection = 1f;
    private void TryAttack()
    {
        if (_time < _lastAttackTime + attackCooldown) return;
        _lastAttackTime = _time;

        // Si hay input horizontal reciente, usa esa dirección; si no, usa la última dirección guardada
        float dir = _frameInput.Move.x != 0 ? Mathf.Sign(_frameInput.Move.x) : _lastMoveDirection;

        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.5f;
        Vector2 direction = Vector2.right * dir;

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, attackRange, EnemyLayer);
        if (hit.collider != null)
        {
            hit.collider.SendMessage("getDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
        }

        Attacked?.Invoke();
    }
    #endregion

    private void ApplyMovement() => _rb.linearVelocity = _frameVelocity;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_stats == null)
            Debug.LogWarning("Please assign a ScriptableStats asset to the Player Controller's Stats slot", this);
    }
#endif

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactuableRadius);

        Gizmos.color = Color.red;
        Vector2 dir = Vector2.right * 1.5f;
        Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, transform.position + Vector3.up * 0.5f + (Vector3)dir);



        // Origen y dirección del raycast
        float dir2 = Mathf.Sign(transform.localScale.x);
        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.5f;
        Vector2 direction = Vector2.right * dir2;

        // Dibuja la línea que representa el rango del ataque
        Gizmos.DrawLine(origin, origin + direction * attackRange);


    }
}

public struct FrameInput
{
    public bool JumpDown;
    public bool JumpHeld;
    public bool DashDown;
    public bool AttackDown; 
    public Vector2 Move;
}

public interface IPlayerController
{
    event Action<bool, float> GroundedChanged;
    event Action Jumped;
    event Action Dashed;
    event Action Attacked; 
    Vector2 FrameInput { get; }
}
