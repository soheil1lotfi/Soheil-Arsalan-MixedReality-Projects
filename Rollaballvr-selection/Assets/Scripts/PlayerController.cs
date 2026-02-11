using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using TMPro;


public class PlayerController : MonoBehaviour
{

    private Rigidbody rb;
    private float movementX;
    private float movementY;
    public int playerHealth;
    private int ECTS;
    private int daysLeft = 365;
    public TextMeshProUGUI ectsTxt;
    public TextMeshProUGUI DaysLeftTxt;
    public GameObject graduationTxt;

    public float acceleration = 30f;
    public float maxSpeed = 6f;
    public float jumpForce = 5.0f;
    public Transform cameraTransform;
    public HealthUI healthUI;


    void Start()
    {
        ECTS = 0;
        playerHealth = 1;
        rb = GetComponent<Rigidbody>();
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
        healthUI.SetHealth(playerHealth);
        SetCountText();
        SetDaysLeftText();
        graduationTxt.SetActive(false);
        StartCoroutine(DaysCountdown());


    }

    void OnMove(InputValue movementValue)
    {
        Vector2 movementVector = movementValue.Get<Vector2>();
        movementX = movementVector.x;
        movementY = movementVector.y;

    }

    void OnJump(InputValue movementValue)
    {
        if (Physics.Raycast(transform.position, Vector3.down, GetComponent<Collider>().bounds.extents.y + 0.1f))
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    void SetCountText()
    {
        ectsTxt.text = "ECTS: " + ECTS.ToString();
    }

    void SetDaysLeftText()
    {
        DaysLeftTxt.text = "Days Left: " + daysLeft.ToString();
    }

    IEnumerator DaysCountdown()
    {
        while (daysLeft > 0)
        {
            yield return new WaitForSeconds(1f);
            daysLeft -= 1;
            SetDaysLeftText();
        }

        LoseGame();
    }

    public void AddEcts(int amount)
    {
        ECTS += amount;
        SetCountText();
        if (ECTS >= 60)
        {
            graduationTxt.SetActive(true);
            Time.timeScale = 0f;
            Destroy(GameObject.FindGameObjectWithTag("Deadline"));
        }
    }

    public int GetEcts()
    {
        return ECTS;
    }

    private void FixedUpdate()
    {
        Vector3 movementDirection = Vector3.zero;

        if (cameraTransform != null)
        {
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;

            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            movementDirection = (camForward * movementY + camRight * movementX).normalized;
        }
        else
        {
            movementDirection = new Vector3(movementX, 0.0f, movementY);
        }

        rb.AddForce(movementDirection * acceleration, ForceMode.Acceleration);

        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (horizontalVelocity.magnitude > maxSpeed)
        {
            Vector3 limited = horizontalVelocity.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(limited.x, rb.linearVelocity.y, limited.z);
        }

    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Deadline"))
        {

            if (playerHealth <= 0)
            {
                LoseGame();
            }
            else
            {
                playerHealth -= 1;
                healthUI.SetHealth(playerHealth);

                Destroy(collision.gameObject);
            }

        }
    }

    void LoseGame()
    {
        Destroy(gameObject);
        graduationTxt.gameObject.SetActive(true);
        graduationTxt.GetComponent<TextMeshProUGUI>().text = "You failed! Try next year.";
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Cantine"))
        {
            playerHealth += 1;
            healthUI.SetHealth(playerHealth);
            Destroy(other.gameObject);

        }
    }
}
