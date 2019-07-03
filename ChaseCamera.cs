using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChaseCamera : MonoBehaviour
{
    public Transform car;
    public float distance;
    public float height;
    public float rotationDamping = 3f;
    public float heightDamping = 2f;
    public float fieldTrans;
    private float desiredAngle = 0;


    void FixedUpdate ()
    {
        desiredAngle = car.eulerAngles.y;
        //if the car is going backwards add 180 to the wanted rotation
        Vector3 localVelocity = car.InverseTransformDirection(car.GetComponent<Rigidbody>().velocity);

        if (localVelocity.z < -0.5f || Input.GetButton("LookBack"))
        {
            desiredAngle += 180;
        }
        if (localVelocity.z > 5)
        {
            if (GetComponent<Camera>().fieldOfView < 85)
            {
                GetComponent<Camera>().fieldOfView += Time.deltaTime * fieldTrans;
            }
        }
        else if (localVelocity.z < 5)
        {
            if (GetComponent<Camera>().fieldOfView > 60)
            {
                GetComponent<Camera>().fieldOfView -= Time.deltaTime * fieldTrans;
            }
        }
    }
	
	// LateUpdate is called once per frame after Update() has been called
	void LateUpdate ()
    {
        float currentAngle = transform.eulerAngles.y;
        float currentHeight = transform.position.y;
        //Determine where we want to be
        float desiredHeight = car.position.y + height;
        //now move towards our goal
        currentAngle = Mathf.LerpAngle(currentAngle, desiredAngle, rotationDamping * Time.deltaTime);
        currentHeight = Mathf.Lerp(currentHeight, desiredHeight, heightDamping * Time.deltaTime);
        Quaternion currentRotation = Quaternion.Euler(0, currentAngle, 0);
        //set our new position
        Vector3 finalPosition = car.position - (currentRotation * Vector3.forward * distance);
        finalPosition.y = currentHeight;
        transform.position = finalPosition;
        transform.LookAt(car);
	}
}
