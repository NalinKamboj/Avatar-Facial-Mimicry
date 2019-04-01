using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

[Serializable]
public class faceData
{
    public string emotion;
    public double confidence;
    public Vector3 eulerAngles;

    public void printData()
    {
        Debug.Log("[INFO] Emotion: " + emotion + ", Confidence - " + confidence + "%");
        Debug.Log("[INFO] Angles - " + eulerAngles);
    }
}

public class emotionMapper : MonoBehaviour {
    public Button startButton;
    public Button headButton;

    Thread receiveThread;
    UdpClient client;
    static int port = 5065;
    private static int currentEmotion;    // 0 - happy, 1 - sad, angry, surprise, neutral
    private static int currentState = 0;    //0 - Inactive, 2 - Running
    private static bool batchReady = false;
    private static bool enableHT = false;
    const int batchSize = 5;    //Make sure it's an odd number to avoid collissions
    //const float blendSpeed = 1.0f;

    //Hash IDs for animations cuz its faster than string comparisons during run-time. StringToHash is a static method!
    int isHappy = Animator.StringToHash("IsHappy");
    int isSad = Animator.StringToHash("IsSad");
    int isAngry = Animator.StringToHash("IsAngry");
    int isSurprise = Animator.StringToHash("IsSurprise");
    int happyNeutral = Animator.StringToHash("HappyNeutral");
    int sadNeutral = Animator.StringToHash("SadNeutral");
    int angryNeutral = Animator.StringToHash("AngryNeutral");
    int surpriseNeutral = Animator.StringToHash("SurpriseNeutral");

    //float happyWeight = 0.0f;
    //float sadWeight = 0.0f;

    private Animator animator;
    faceData face = new faceData();    //Used for actual update methods.
    //faceData temp = new faceData();
    private int[] emotionBatch = new int[5];
    //private SkinnedMeshRenderer skinMeshRenderer;
    //private Process emotionServer;
    Transform head;    //For head rotation

    private void initUDP()
    {
        Debug.Log("[INFO] UDP Initialization Started");
        receiveThread = new Thread(new ThreadStart(ReceiveData))
        {
            IsBackground = true
        };
        receiveThread.Start();
    }

    private void ReceiveData()
    {
        client = new UdpClient(port);
        int batchCounter = 0;
        while (currentState==1)
        {
            try
            {
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);
                byte[] data = client.Receive(ref anyIP);
                string dataJSON = Encoding.UTF8.GetString(data);    //Python server sends data encoded as a JSON
                //print("[INFO] Data Received: " + dataJSON);
                //if(dataJSON.Length == 1)
                //{
                //    currentState = 1;
                //    Debug.Log("[INFO] STATE - RUNNING");
                //} else
                face = JsonUtility.FromJson<faceData>(dataJSON);
                //face.printData();

                //Batch processing 
                if (face.emotion == "happy")
                    emotionBatch[batchCounter] = 0;
                else if (face.emotion == "sad")
                    emotionBatch[batchCounter] = 1;
                else if (face.emotion == "angry")
                    emotionBatch[batchCounter] = 2;
                else if (face.emotion == "surprise")
                    emotionBatch[batchCounter] = 3;
                else if (face.emotion == "neutral")
                    emotionBatch[batchCounter] = 4;
                if (batchCounter == 4)
                {
                    batchReady = true;
                    //Debug.Log("BATCH READY!");
                }
                batchCounter = (batchCounter+1) % batchSize;
            }
            catch (Exception e)
            {
                Debug.Log("[EXCEPTION] ReceiveData: " + e.ToString());
            }
        }
        Debug.Log("[INFO] RECEIVE COMPLETE.");
        return;
    }

    // Use this for initialization
    void Start () {
        //UnityEngine.Debug.Log("Emotion Script Working!");
        //initUDP();

        //Get SkinnedMeshRenderer for manipulating blendshapes
        //skinMeshRenderer = GetComponent<SkinnedMeshRenderer>();
        animator = GetComponentInParent<Animator>();
        head = animator.GetBoneTransform(HumanBodyBones.Head);
    }

    void OnAnimatorIK(int layerIndex) {
        if(enableHT)
            animator.SetBoneLocalRotation(HumanBodyBones.Head, Quaternion.Euler(face.eulerAngles));
    }

    //TODO Refine GUI elements
    void OnGUI()
    {
        //Adding UI controls...
    }
	
	// Update is called once per frame
	void Update () {
        int emotion = 4;    //Default to neutral
        if(currentState == 1 && batchReady)    //Check and start emotion mapping.
        {
            batchReady = false;
            var counts = new Dictionary<int, int>();
            foreach (int number in emotionBatch)
            {
                int count;
                counts.TryGetValue(number, out count);
                count++;
                //Automatically replaces the entry if it exists;
                //no need to use 'Contains'
                counts[number] = count;
            }
            int mostCommonNumber = 0, occurrences = 0;
            foreach (var pair in counts)
            {
                if (pair.Value > occurrences)
                {
                    occurrences = pair.Value;
                    mostCommonNumber = pair.Key;
                }
            }
            emotion = mostCommonNumber;

            //old code// 0 - happy, 1 - sad, 2 - angry, 3 - surprise, 4 - neutral
            if (emotion == 0 && currentEmotion != 0)
            {
                Debug.Log(" HAPPY ANIM RUNNING! CURR EMO: " + currentEmotion);
                setNeutral();
                animator.SetTrigger(isHappy);
                currentEmotion = 0;
            }
            else if (emotion == 2 && currentEmotion != 2)
            {
                Debug.Log(" ANG ANIM RUNNING! CURR EMO: " + currentEmotion);
                setNeutral();
                animator.SetTrigger(isAngry);
                currentEmotion = 2;
            }
            else if (emotion == 3 && currentEmotion != 3)
            {
                Debug.Log(" SURP ANIM RUNNING! CURR EMO: " + currentEmotion);
                setNeutral();
                animator.SetTrigger(isSurprise);
                currentEmotion = 3;
            }
            else if (emotion == 1 && currentEmotion != 1)
            {
                Debug.Log(" SAD ANIM RUNNING! CURR EMO: " + currentEmotion);
                setNeutral();
                animator.SetTrigger(isSad);
                currentEmotion = 1;
            }
            else if (emotion == 4 && currentEmotion != 4)
            {
                Debug.Log("SET NEUTRAL CURR EMO: " + currentEmotion);
                setNeutral();
                currentEmotion = 4;
            }
        }
	}

    //utility function to set animate character to neutral state
    void setNeutral()
    {
        // 0 - happy, 1 - sad, 2 - angry, 3 - surprise, 4 - neutral
        if (currentEmotion == 4)
            return;
        if (currentEmotion == 0)
        {
            //animator.ResetTrigger(isHappy);
            animator.SetTrigger(happyNeutral);
        }
        else if (currentEmotion == 1)
        {
            //animator.ResetTrigger(isSad);
            animator.SetTrigger(sadNeutral);
        }
        else if (currentEmotion == 2)
        {
            //animator.ResetTrigger(isAngry);
            animator.SetTrigger(angryNeutral);
        }
        else if(currentEmotion == 3){
            //animator.ResetTrigger(isSurprise);
            animator.SetTrigger(surpriseNeutral);
        }
        currentEmotion = 4;
    }

    //Function to start python emotion recognition server
    public void StartServer()
    {
        if(currentState == 0)
        {
            //Start server
            currentState = 1;    //Set state to running
            startButton.GetComponentInChildren<Text>().text = "Map Running...";
            startButton.enabled = false;
            initUDP();
        } else if(currentState == 1)
        {
            currentState = 0;
            receiveThread.Join();
            Debug.Log("[INFO] Thread joined!");
        }
    }

    public void EnableHeadTransform()
    {
        enableHT = !enableHT;
        if(enableHT)
            headButton.GetComponentInChildren<Text>().text = "Disable Head Transform";
        else
            headButton.GetComponentInChildren<Text>().text = "Enable Head Transform";
    }
}
