using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkidEnabler : MonoBehaviour
{
    public WheelCollider wheelCollider;
    public GameObject skidTrailRenderer;
    public float skidLife = 4f;
    private TrailRenderer skidMark;

	// Use this for initialization
	void Start ()
    {
        skidMark = skidTrailRenderer.GetComponent<TrailRenderer>();
        //this avoids a visual bug on first use, if the art team set the effects time to 0
        skidMark.time = skidLife;
	}
	
	// Update is called once per frame
	void Update ()
    {
		if(wheelCollider.forwardFriction.stiffness < 0.1f && wheelCollider.isGrounded)
        {
            //if skidMark's time variable is 0 then we have to reset it previously and can now use it
            if(skidMark.time == 0)
            {
                skidMark.time = skidLife;
                skidTrailRenderer.transform.parent = wheelCollider.transform;
                skidTrailRenderer.transform.localPosition = wheelCollider.center + ((wheelCollider.radius - 0.1f) * -wheelCollider.transform.up);
            }
            //if this skid mark's parent is null then we have previously used it and need to reset it first
            if(skidTrailRenderer.transform.parent == null)
            {
                skidMark.time = 0;
            }
        }
        //unhook the skid effect game object from the wheel collider so it gets left behind
        else
        {
            skidTrailRenderer.transform.parent = null;
        }
	}
}
