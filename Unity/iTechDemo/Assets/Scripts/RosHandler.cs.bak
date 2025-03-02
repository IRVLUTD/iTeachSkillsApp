using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Debug = UnityEngine.Debug;

using System.IO;

using Unity.Robotics.ROSTCPConnector;


namespace iTeachSkills.ROS
{

    [System.Serializable]
    public class RosConnectionConfig
    {
        public string RosIPAddress;
        public int RosPort;
        public float KeepaliveTime;
        public float NetworkTimeoutSeconds;
        public float SleepTimeSeconds;
        public bool ShowHud;
        public string VideoTopic;
        public string LabelFrameTopic;
        public string RecordCommandTopic;
        public string SendPromptsTopic;
        public int ImageHeight;
        public int ImageWidth;

        public static RosConnectionConfig CreateFromJSON(string jsonString)
        {
            return JsonUtility.FromJson<RosConnectionConfig>(jsonString);
        }
    }




    public class RosHandler : MonoBehaviour
    {
        public static RosHandler Instance;
        public static ROSConnection rosConnection;
        public static RosConnectionConfig config;
        

        private string rosConfigPath;

        void Awake()
        {
            // Ensure that there is only one instance of this class
            if (Instance != null)
            {
                Debug.LogError("There should only be one instance of " + this.GetType().Name);
                Destroy(this);
                return;
            }
            Instance = this;
        }
        

        // Load ROS connection configuration from a JSON file
        private RosConnectionConfig LoadRosConnectionConfig()
        {
            rosConfigPath = Path.Combine(Application.persistentDataPath, "ROSConnectionConfig.json");
            if (!File.Exists(rosConfigPath))
            {
                rosConfigPath = Path.Combine(Application.streamingAssetsPath, "ROSConnectionConfig.json");
            }

            byte[] bytes = UnityEngine.Windows.File.ReadAllBytes(rosConfigPath);
            string jsonString = System.Text.Encoding.ASCII.GetString(bytes);
            return RosConnectionConfig.CreateFromJSON(jsonString);
        }

        // Connect to ROS master
        public void RosConnect()
        {
            //var config = LoadRosConnectionConfig();
            config = LoadRosConnectionConfig();
            rosConnection = ROSConnection.GetOrCreateInstance();
            rosConnection.RosIPAddress = config.RosIPAddress;
            rosConnection.RosPort = config.RosPort;
            rosConnection.KeepaliveTime = config.KeepaliveTime;
            rosConnection.NetworkTimeoutSeconds = config.NetworkTimeoutSeconds;
            rosConnection.SleepTimeSeconds = config.SleepTimeSeconds;
            rosConnection.ShowHud = config.ShowHud;
            rosConnection.Connect();

            Debug.Log("Connected to ROS at " + config.RosIPAddress);
        }

        // Disconnect from ROS master
        public void RosDisconnect()
        {
            if (rosConnection != null)
            {
                rosConnection.Disconnect();
                Debug.Log("Disconnected from ROS.");
            }
        }
    }
}
