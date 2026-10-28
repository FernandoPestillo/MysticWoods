using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{

    private Rigidbody2D _playerRigidBody2D;
    private Animator    _playerAnimator;
    public float        _playerSpeed;
    private float       _playerInitialSpeed;
    public float       _playerRunSpeed;
    private Vector2     _playerDirection;
    public HeartSystem _playerHeartSystem;

    private bool _isAttack = false;


    // Start is called before the first frame update
    void Start()
    {
        _playerRigidBody2D = GetComponent<Rigidbody2D>();
        _playerAnimator = GetComponent<Animator>();

        _playerInitialSpeed = _playerSpeed;
    }

    // Update is called once per frame
    void Update()
    {
        

        // Flip();
        PlayerRun();
        OnAttack();

        
    }

     void FixedUpdate()
    {
        var keyboard = Keyboard.current;
        _playerDirection = Vector2.zero;
        if (keyboard != null)
        {
            int horizontal = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
            int vertical = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            _playerDirection = new Vector2(horizontal, vertical);
        }

        if (_playerDirection.sqrMagnitude > 0.1 && _playerAnimator.GetInteger("Movimento") != 2)
        {
            MovePlayer();

            _playerAnimator.SetFloat("AxisX", _playerDirection.x);
            _playerAnimator.SetFloat("AxisY", _playerDirection.y);


            _playerAnimator.SetInteger("Movimento", 1);
        }
        else
        {
            _playerAnimator.SetInteger("Movimento", 0);
        }

        if (_isAttack)
        {
            _playerAnimator.SetInteger("Movimento", 2);
        }
    }

    void MovePlayer()
    {
        if(_playerHeartSystem.gettingKnockBack) { return; }
        _playerRigidBody2D.MovePosition(_playerRigidBody2D.position + _playerDirection.normalized * _playerSpeed * Time.fixedDeltaTime);
    }

    void Flip()
    {
        if (_playerDirection.x > 0)
        {
            transform.eulerAngles = new Vector2(0f, 0f);
        }
        else if (_playerDirection.x < 0)
        {
            transform.eulerAngles = new Vector2(0f, 180f);
        }
    }

    void PlayerRun()
    {
        if (Keyboard.current?.leftShiftKey.wasPressedThisFrame == true)
        {
            _playerSpeed = _playerRunSpeed;
        }

        if (Keyboard.current?.leftShiftKey.wasReleasedThisFrame == true)
        {
            _playerSpeed = _playerInitialSpeed;
        }
    }

    void OnAttack()
    {
        if (Keyboard.current?.spaceKey.wasPressedThisFrame == true || Mouse.current?.leftButton.wasPressedThisFrame == true)
        {
            _isAttack = true;
            _playerSpeed = 0;
        }

        if (Keyboard.current?.spaceKey.wasReleasedThisFrame == true || Mouse.current?.leftButton.wasReleasedThisFrame == true)
        {
            _isAttack = false;
            _playerSpeed = _playerInitialSpeed;
        }
    }
}
