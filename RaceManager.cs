using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

public class RaceManager : MonoBehaviour
{
    public Car player;
    public Rigidbody[] cars;
    public float respawnDelay = 5f;
    public float distanceToCover = 1f;

    public bool raceStart = false;

    private int[] laps;
    private CarController[] scripts;
    private float[] respawnTimes;
    private float[] distanceLeftToTravel;
    private Transform[] waypoint;

    public Texture2D startRaceImage;
    public Texture2D digit1Image;
    public Texture2D digit2Image;
    public Texture2D digit3Image;
    private int countdownTimerDelay;
    private float countdownTimerStartTime;

    public static RaceManager Instance { get { return instance; } }
    private static RaceManager instance = null;

    void Awake()
    {
        if(instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        else
        {
            instance = this;
        }

        CountdownTimerReset(3); 
    }

    // Use this for initialization
    void Start ()
    {
        respawnTimes = new float[cars.Length];
        distanceLeftToTravel = new float[cars.Length];
        scripts = new CarController[cars.Length];
        waypoint = new Transform[cars.Length];
        laps = new int[cars.Length];

        //initialize the arrays with starting values
        for(int i = 0; i < respawnTimes.Length; i++)
        {
            scripts[i] = cars[i].gameObject.GetComponent<CarController>();
            respawnTimes[i] = respawnDelay;
            distanceLeftToTravel[i] = float.MaxValue;
            laps[i] = 0;
        }
	}
	
	// Update is called once per frame
	void Update ()
    {
        //if the countdown is finished, start the race
        StartRace();
        //check if any of the cars need a respawn
        for (int i = 0; i < cars.Length; ++i)
        {
            Transform nextWaypoint = scripts[i].GetCurrentWaypoint();
            float distanceCovered = (nextWaypoint.position - cars[i].position).magnitude;
            //if the car has moved far enough or is moving to a new waypoint reset it's values
            if (distanceLeftToTravel[i] - distanceToCover > distanceCovered || waypoint[i] != nextWaypoint)
            {
                waypoint[i] = nextWaypoint;
                respawnTimes[i] = respawnDelay;
                distanceLeftToTravel[i] = distanceCovered;
            }
            //otherwise tick down time before we respawn the car
            else
            {
                respawnTimes[i] -= Time.deltaTime;
            }

            //if it's respawn timer has elapsed
            if(respawnTimes[i] <= 0)
            {
                //reset its respawn tracking variables
                respawnTimes[i] = respawnDelay;
                distanceLeftToTravel[i] = float.MaxValue;
                cars[i].velocity = Vector3.zero;

                //andspawn it at its last waypoint facing the next waypoint
                Transform lastWaypoint = scripts[i].GetLastWaypoint();
                cars[i].position = lastWaypoint.position;
                cars[i].rotation = Quaternion.LookRotation(nextWaypoint.position - lastWaypoint.position);
            }
            //testing if the lap counter works. First car to complete 3 laps triggers a level restart
            if(laps[i] >= 3 || player.lapCounter >= 3)
            {
                SceneManager.LoadScene("MainMenu");
            }
        }
	}

    public void LapFinishedByAI(CarController script)
    {
        //search through and find the car that communicated with us
        for (int i = 0; i < respawnTimes.Length; ++i)
        {
            if(scripts[i] == script)
            {
                //increment its lap counter
                laps[i]++;
                break;
            }
        }
    }

    public void LapFinishedByPlayer(Car script)
    {
        script.lapCounter++;
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(0, 0, Screen.width, Screen.height));
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label(CountdownTimerImage());
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.EndArea(); 
    }

    Texture2D CountdownTimerImage()
    {
        switch(CountdownTimerSecondsRemaining())
        {
            case 3:
                return digit3Image;
            case 2:
                return digit2Image;
            case 1:
                return digit1Image;
            case 0:
                return startRaceImage;
            default:
                return null;
        }
    }

    int CountdownTimerSecondsRemaining()
    {
        int elapsedSeconds = (int)(Time.time - countdownTimerStartTime);
        int secondsLeft = (countdownTimerDelay - elapsedSeconds);
        return secondsLeft;
    }

    void CountdownTimerReset(int delayInSeconds)
    {
        countdownTimerDelay = delayInSeconds;
        countdownTimerStartTime = Time.time;
    }

    void StartRace()
    {
        if (CountdownTimerSecondsRemaining() <= 0)
        {
            raceStart = true;
        }
        
    }
}
