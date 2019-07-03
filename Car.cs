using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Car : MonoBehaviour
{
    public Texture2D speedometer;
    public Texture2D needle;

    public int numberOfGears;
    public float handbrakeForwardSlip = 0.04f;
    public float handbrakeSidewaysSlip = 0.08f;
    public float maxBrakeTorque = 100;
    private bool applyHandbrake = false;

    private float gearSpread;
    public float maxTurnAngle = 10;
    public float maxTorque = 10;
    public float decelerationTorque = 30;
    public float topSpeed = 150;
    public float topSpeedReverse = -50;

    private float currentSpeed;

    public WheelCollider wheelFL;
    public WheelCollider wheelFR;
    public WheelCollider wheelBL;
    public WheelCollider wheelBR;

    public Transform wheelTransformFL;
    public Transform wheelTransformFR;
    public Transform wheelTransformBL;
    public Transform wheelTransformBR;

    public ParticleSystem dustL;
    public ParticleSystem dustR;

    public GameObject leftBrakeLight;
    public GameObject rightBrakeLight;
    public Texture2D idleLightTex;
    public Texture2D brakeLightTex;
    public Texture2D reverseLightTex;
    public Light brakeLightL;
    public Light brakeLightR;
    public Light reverseLightL;
    public Light reverseLightR;

    public Vector3 centerOfMassAdjustment = new Vector3(0f, -0.9f, 0f);
    private Rigidbody body;

    public float spoilerRatio = 0.1f;

    public Transform waypointContainer;

    private Transform[] waypoints;
    public int currentWaypoint = 0;

    public int lapCounter;

    public RaceManager manager;

    // Use this for initialization
    void Start()
    {
        //calculate the spread of top speed over the number of gears
        gearSpread = topSpeed / numberOfGears;
        
        //lower centre of mass for roll-over resistance
        body = GetComponent<Rigidbody>();
        body.centerOfMass += centerOfMassAdjustment;

        //get waypoints from the track
        GetWaypoints();
    }

    void Update()
    {
        //Adjust the wheels heights based on the suspension
        UpdateWheelPositions();

        //rotate the wheel meshes
        float rotationThisFrame = 360 * Time.deltaTime;
        wheelTransformFL.Rotate(0, -wheelFL.rpm / rotationThisFrame, 0);
        wheelTransformFR.Rotate(0, -wheelFR.rpm / rotationThisFrame, 0);
        wheelTransformBL.Rotate(0, -wheelBL.rpm / rotationThisFrame, 0);
        wheelTransformBR.Rotate(0, -wheelBR.rpm / rotationThisFrame, 0);

        //Determine what texture to use on our brake lights right now
        DetermineBrakeLightState();

        //adjust engine sound
        EngineSound();

        //spray dust behind the cars
        ToggleDust();
    }

    // FixedUpdate is called once per physics frame
    void FixedUpdate()
    {
        if (manager.raceStart)
        {
            //calculate max speed in KM/H (condensed calculation)
            currentSpeed = wheelBL.radius * wheelBL.rpm * Mathf.PI * 0.12f;
            if (currentSpeed < topSpeed && currentSpeed > topSpeedReverse)
            {
                //rear wheel drive
                wheelBL.motorTorque = Input.GetAxis("Vertical") * maxTorque;
                wheelBR.motorTorque = Input.GetAxis("Vertical") * maxTorque;
            }
            else
            {
                //can't go faster, already at top speed
                wheelBL.motorTorque = 0;
                wheelBR.motorTorque = 0;
            }

            //front wheel steering
            wheelFL.steerAngle = Input.GetAxis("Horizontal") * maxTurnAngle;
            wheelFR.steerAngle = Input.GetAxis("Horizontal") * maxTurnAngle;

            //turn the wheels to face the steering direction
            wheelTransformFL.localEulerAngles = new Vector3(wheelTransformFL.localEulerAngles.x, wheelFL.steerAngle, 90);
            wheelTransformFR.localEulerAngles = new Vector3(wheelTransformFR.localEulerAngles.x, wheelFR.steerAngle, 90);

            //Spoilers add downforce based on the car's speed
            Vector3 localVelocity = transform.InverseTransformDirection(body.velocity);
            body.AddForce(-transform.up * (localVelocity.z * spoilerRatio), ForceMode.Impulse);

            //Handbrake controls
            if (Input.GetButton("Jump"))
            {
                applyHandbrake = true;
                wheelFL.brakeTorque = maxBrakeTorque;
                wheelFR.brakeTorque -= maxBrakeTorque;
                //wheels are locked, power slide!!
                if (GetComponent<Rigidbody>().velocity.magnitude > 1)
                {
                    SetSlipValues(handbrakeForwardSlip, handbrakeSidewaysSlip);
                }
                else //skid to a stop, regular friction enabled
                {
                    SetSlipValues(1f, 1f);
                }
            }
            else
            {
                applyHandbrake = false;
                wheelFL.brakeTorque = 0;
                wheelFR.brakeTorque = 0;
                SetSlipValues(1f, 1f);
            }

            //apply deceleration when pressing the brakes or lightly when not pressing the gas
            if (!applyHandbrake && ((Input.GetAxis("Vertical") <= -0.5f && localVelocity.z > 0) || (Input.GetAxis("Vertical") >= 0.5f && localVelocity.z < 0)))
            {
                wheelBL.brakeTorque = decelerationTorque + maxTorque;
                wheelBR.brakeTorque = decelerationTorque + maxTorque;
            }
            else if (!applyHandbrake && Input.GetAxis("Vertical") == 0)
            {
                wheelBL.brakeTorque = decelerationTorque;
                wheelBR.brakeTorque = decelerationTorque;
            }
            else
            {
                wheelBL.brakeTorque = 0;
                wheelBR.brakeTorque = 0;
            }

            //if close enough, change waypoints
            Vector3 RelativeWaypointPosition = transform.InverseTransformPoint(new Vector3(waypoints[currentWaypoint].position.x, transform.position.y, waypoints[currentWaypoint].position.z));
            if (RelativeWaypointPosition.magnitude < 25)
            {
                currentWaypoint++;
                if (currentWaypoint >= waypoints.Length)
                {
                    currentWaypoint = 0;
                    RaceManager.Instance.LapFinishedByPlayer(this);
                }
            }

            if (Input.GetButton("Reset"))
            {
                Transform nextWaypoint = GetCurrentWaypoint();
                Transform lastWaypoint = GetLastWaypoint();
                this.transform.position = lastWaypoint.position;
                this.transform.rotation = Quaternion.LookRotation(nextWaypoint.position - lastWaypoint.position);
            }
        }
    }

    //move the wheels based on their suspension
    void UpdateWheelPositions()
    {
        WheelHit contact = new WheelHit();

        if (wheelFL.GetGroundHit(out contact))
        {
            Vector3 temp = wheelFL.transform.position;
            temp.y = (contact.point + (wheelFL.transform.up * wheelFL.radius)).y;
            wheelTransformFL.position = temp;
        }
        if (wheelFR.GetGroundHit(out contact))
        {
            Vector3 temp = wheelFR.transform.position;
            temp.y = (contact.point + (wheelFR.transform.up * wheelFR.radius)).y;
            wheelTransformFR.position = temp;
        }
        if (wheelBL.GetGroundHit(out contact))
        {
            Vector3 temp = wheelBL.transform.position;
            temp.y = (contact.point + (wheelBL.transform.up * wheelBL.radius)).y;
            wheelTransformBL.position = temp;
        }
        if (wheelBR.GetGroundHit(out contact))
        {
            Vector3 temp = wheelBR.transform.position;
            temp.y = (contact.point + (wheelBR.transform.up * wheelBR.radius)).y;
            wheelTransformBR.position = temp;

        }
    }

    //control friction for the wheels if using the handbrake
    void SetSlipValues(float forward, float sideways)
    {
        //change the stiffness values of wheel friction curve and then reapply it
        WheelFrictionCurve tempStruct = wheelBR.forwardFriction;
        tempStruct.stiffness = forward;
        wheelBR.forwardFriction = tempStruct;

        tempStruct = wheelBR.sidewaysFriction;
        tempStruct.stiffness = sideways;
        wheelBR.sidewaysFriction = tempStruct;

        tempStruct = wheelBL.forwardFriction;
        tempStruct.stiffness = forward;
        wheelBL.forwardFriction = tempStruct;

        tempStruct = wheelBL.sidewaysFriction;
        tempStruct.stiffness = sideways;
        wheelBL.sidewaysFriction = tempStruct;
    }

    void DetermineBrakeLightState()
    {
        if ((currentSpeed > 0 && Input.GetAxis("Vertical") < 0) || (currentSpeed < 0 && Input.GetAxis("Vertical") > 0) || applyHandbrake)
        {
            leftBrakeLight.GetComponent<Renderer>().material.mainTexture = brakeLightTex;
            rightBrakeLight.GetComponent<Renderer>().material.mainTexture = brakeLightTex;
            brakeLightL.enabled = true;
            brakeLightR.enabled = true;
            reverseLightL.enabled = false;
            reverseLightR.enabled = false;
        }
        else if(currentSpeed < 0 && Input.GetAxis("Vertical") < 0)
        {
            leftBrakeLight.GetComponent<Renderer>().material.mainTexture = reverseLightTex;
            rightBrakeLight.GetComponent<Renderer>().material.mainTexture = reverseLightTex;
            brakeLightL.enabled = false;
            brakeLightR.enabled = false;
            reverseLightL.enabled = true;
            reverseLightR.enabled = true;
        }
        else
        {
            leftBrakeLight.GetComponent<Renderer>().material.mainTexture = idleLightTex;
            rightBrakeLight.GetComponent<Renderer>().material.mainTexture = idleLightTex;
            brakeLightL.enabled = false;
            brakeLightR.enabled = false;
            reverseLightL.enabled = false;
            reverseLightR.enabled = false;
        }
    }

    void ToggleDust()
    {
        if (currentSpeed > 0 || currentSpeed < 0)
        {
            dustL.enableEmission = true;
            dustR.enableEmission = true;
        }
        else
        {
            dustL.enableEmission = false;
            dustR.enableEmission = false;
        }
    }

    void EngineSound()
    {
        //going forward calculate how far along that gear we are and the pitch sound
        if(currentSpeed > 0)
        {
            if(currentSpeed > topSpeed)
            {
                GetComponent<AudioSource>().pitch = 1.75f;
            }
            else
            {
                GetComponent<AudioSource>().pitch = ((currentSpeed % gearSpread) / gearSpread) + 0.75f;
            }
        }
        //when reversing we only have one gear
        else
        {
            GetComponent<AudioSource>().pitch = (currentSpeed / topSpeedReverse) + 0.75f; 
        }
    }

    void OnGUI()
    {
        GUI.DrawTexture(new Rect(Screen.width - 300, Screen.height - 150, 300, 150), speedometer);
        float speedFactor = currentSpeed / topSpeed;
        float rotationAngle = Mathf.Lerp(0, 180, Mathf.Abs(speedFactor));
        GUIUtility.RotateAroundPivot(rotationAngle, new Vector2(Screen.width - 150, Screen.height));
        GUI.DrawTexture(new Rect(Screen.width - 300, Screen.height - 150, 300, 300), needle);
    }

    void GetWaypoints()
    {
        //NOTE: Unity named this function poorly it also return the parent's component
        Transform[] potentialWaypoints = waypointContainer.GetComponentsInChildren<Transform>();

        //initialize the waypoints array so that it has enough space to store the nodes
        waypoints = new Transform[(potentialWaypoints.Length - 1)];

        //loop through the list and copy the nodes into the array
        //start at 1 instead of 0 to skip the WaypointContainer's transform
        for (int i = 1; i < potentialWaypoints.Length; ++i)
        {
            waypoints[i - 1] = potentialWaypoints[i];
        }
    }

    public Transform GetCurrentWaypoint()
    {
        return waypoints[currentWaypoint];
    }

    public Transform GetLastWaypoint()
    {
        if (currentWaypoint - 1 < 0)
        {
            return waypoints[waypoints.Length - 1];
        }

        return waypoints[currentWaypoint - 1];
    }
}
