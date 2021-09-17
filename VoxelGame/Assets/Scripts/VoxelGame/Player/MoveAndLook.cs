using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class MoveAndLook : MonoBehaviour
{
	public float moveSpeed;
	public float turnSpeed;

	public Transform lookRoot;

	public CharacterController controller;

	[SerializeField]
	private Transform crosshairUI = null;

	private void Start()
	{
		if (controller == null)
			controller = GetComponent<CharacterController>();

		Cursor.lockState = CursorLockMode.Locked;
		if (crosshairUI != null)
		{ 
			crosshairUI.gameObject.SetActive(true);
		}
	}

	// Update is called once per frame
	void Update()
	{
		var moveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
		var turnInput = new Vector2(Input.GetAxis("Mouse X")   , Input.GetAxis("Mouse Y"));

		
		var move_dir = lookRoot.forward * moveInput.y + lookRoot.right * moveInput.x;

		if (Input.GetKey(KeyCode.Space))
		{
			move_dir.y += 1;  // Make the Player move mostly upwards (the move amount will be normalized of course).
		}

		if (move_dir.sqrMagnitude > 1)
		{  // Make sure that we can't move faster than move_speed;
			move_dir.Normalize();
		}

		var trans_speed = Input.GetKey(KeyCode.LeftShift) ? moveSpeed * 2.5F : moveSpeed;

		controller.Move(move_dir * trans_speed * Time.deltaTime);

		var prev_rot = lookRoot.rotation.eulerAngles;

		lookRoot.localRotation = Quaternion.Euler(prev_rot.x - turnInput.y * turnSpeed * Time.deltaTime, 0, 0);
		transform.Rotate(0, turnInput.x * turnSpeed * Time.deltaTime, 0);
	}
}
