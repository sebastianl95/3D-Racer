using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarController : MonoBehaviour
{
    public float brakingDistance = 6f;
    public float forwardOffset;
    public float turningDistance = 6f;
    public float leftOffset;
    public float rightOffset;

    public Transform waypointContainer;

    private Transform[] waypoints;
    public int currentWaypoint = 0;
    private float inputSteer;
    private float inputTorque;

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

    public RaceManager manager;

    // Use this for initialization
    void Start()
    {
        //get the waypoints from the track
        GetWaypoints();

        //calculate the spread of top speed over the number of gears
        gearSpread = topSpeed / numberOfGears;

        //lower centre of mass for roll-over resistance
        body = GetComponent<Rigidbody>();
        body.centerOfMass += centerOfMassAdjustment;
    }

    // Update is called once per frame
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

        //flip them around if they start traveling in reverse
        if (currentSpeed < -40)
        {
            StartCoroutine(Flipper());
        }
    }

    // FixedUpdate is called once per physics frame
    void FixedUpdate()
    {
        if (manager.raceStart)
        {
            //calculate turn angle
            Vector3 RelativeWaypointPosition = transform.InverseTransformPoint(new Vector3(waypoints[currentWaypoint].position.x, transform.position.y, waypoints[currentWaypoint].position.z));
            inputSteer = RelativeWaypointPosition.x / RelativeWaypointPosition.magnitude;

            //Spoilers add down pressure based on the car's speed (upside down lift)
            Vector3 localVelocity = transform.InverseTransformDirection(body.velocity);
            body.AddForce(-transform.up * (localVelocity.z * spoilerRatio), ForceMode.Impulse);

            //calculate torque
            if (Mathf.Abs(inputSteer) < 0.5f)
            {
                //when making minor turning adjustments speed is based on how far to the next point
                inputTorque = (RelativeWaypointPosition.z / RelativeWaypointPosition.magnitude);
                applyHandbrake = false;
            }
            else
            {
                //we need to make a hard turn, if moving fast apply handbrake to slide
                if (body.velocity.magnitude > 10)
                {
                    applyHandbrake = true;
                }
                //if not moving forward backup and turn opposite
                else if (localVelocity.z < 0)
                {
                    applyHandbrake = false;
                    inputTorque = -1;
                    inputSteer *= -1;
                }
                //let off the gas while making a hard turn
                else
                {
                    applyHandbrake = false;
                    inputTorque = 0;
                }
            }

            //set slip values
            if (applyHandbrake)
            {
                SetSlipValues(handbrakeForwardSlip, handbrakeSidewaysSlip);
            }
            else
            {
                SetSlipValues(1f, 1f);
            }

            //if close enough, change waypoints
            if (RelativeWaypointPosition.magnitude < 25)
            {
                currentWaypoint++;
                if (currentWaypoint >= waypoints.Length)
                {
                    currentWaypoint = 0;
                    RaceManager.Instance.LapFinishedByAI(this);
                }
            }

            //check for cars beside
            float steerAdjustment = LeftRayCast() + RightRayCast();
            //front wheel steering
            wheelFL.steerAngle = steerAdjustment * inputSteer * maxTurnAngle;
            wheelFR.steerAngle = steerAdjustment * inputSteer * maxTurnAngle;

            //calculate max speed in KM/H (optimized calc)
            currentSpeed = wheelBL.radius * wheelBL.rpm * Mathf.PI * 0.12f;

            if (currentSpeed < topSpeed && currentSpeed > topSpeedReverse)
            {
                //check for cars in front
                float adjustment = ForwardRayCast();
                //real wheel drive
                wheelBL.motorTorque = adjustment * inputTorque * maxTorque;
                wheelBR.motorTorque = adjustment * inputTorque * maxTorque;
            }
            else
            {
                //can' go faster, already at top speed
                wheelBL.motorTorque = 0;
                wheelBR.motorTorque = 0;
            }
        }
    }

    private float ForwardRayCast()
    {
        RaycastHit hit;
        Vector3 carFront = transform.position + (transform.forward * forwardOffset);
        Debug.DrawRay(carFront, transform.forward * brakingDistance);

        //if we detect a car in front of us, slow down or even reverse based on distance
        if (Physics.Raycast(carFront, transform.forward, out hit, brakingDistance))
        {
            return (((carFront - hit.point).magnitude / brakingDistance) * 2) - 1;
        }

        //otherwise no change
        return 1f;
    }

    private float LeftRayCast()
    {
        RaycastHit hit;
        Vector3 carSide = transform.position + (transform.right * leftOffset);
        Debug.DrawRay(carSide, transform.right * turningDistance);

        //if we detect a car beside us, turn away
        if (Physics.Raycast(carSide, transform.right, out hit, turningDistance))
        {
            return (((carSide - hit.point).magnitude / turningDistance) * 2) - 1;
        }

        //otherwise no change
        return 1f;
    }

    private float RightRayCast()
    {
        RaycastHit hit;
        Vector3 carSide = transform.position + (transform.right * rightOffset);
        Debug.DrawRay(carSide, transform.right * turningDistance);

        //if we detect a car beside us, turn away
        if (Physics.Raycast(carSide, transform.right, out hit, turningDistance))
        {
            return (((carSide - hit.point).magnitude / turningDistance) * 2) - 1;
        }

        //otherwise no change
        return 1f;
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
        if ((currentSpeed > 0 && inputTorque < 0) || (currentSpeed < 0 && inputTorque > 0) || applyHandbrake)
        {
            leftBrakeLight.GetComponent<Renderer>().material.mainTexture = brakeLightTex;
            rightBrakeLight.GetComponent<Renderer>().material.mainTexture = brakeLightTex;
            brakeLightL.enabled = true;
            brakeLightR.enabled = true;
            reverseLightL.enabled = false;
            reverseLightR.enabled = false;
        }
        else if (currentSpeed < 0 && inputTorque < 0)
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
        if (currentSpeed > 0)
        {
            if (currentSpeed > topSpeed)
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

    public IEnumerator Flipper()
    {

        yield return new WaitForSeconds(5);
        if (currentSpeed < -50)
        {
            Vector3 targetDir = waypoints[currentWaypoint].position - transform.position;
            float step = Mathf.Abs(currentSpeed) * Time.deltaTime;
            Vector3 newDir = Vector3.RotateTowards(transform.forward, targetDir, step, 0.0F);
            Debug.DrawRay(transform.position, newDir, Color.red);
            transform.rotation = Quaternion.LookRotation(newDir);
        }

    }
}

