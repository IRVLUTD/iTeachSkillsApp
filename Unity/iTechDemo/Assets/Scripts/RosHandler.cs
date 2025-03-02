
using System.IO;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using ImageMsg = RosMessageTypes.Sensor.ImageMsg;
using CompressedImageMsg = RosMessageTypes.Sensor.CompressedImageMsg;

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



    // Wrapper class for ROSTCPConnector
    public class RosHandler
    {
        public ROSConnection ros;
        public RosConnectionConfig config;


        public RosHandler(ROSConnection rosConnection)
        {
            ros = rosConnection;

            // Load ROS connection configuration from a JSON file
            LoadRosConnectionConfig();
        }

        // Load ROS connection configuration from a JSON file
        private void LoadRosConnectionConfig()
        {
            var rosConfigPath = Path.Combine(Application.persistentDataPath, "ROSConnectionConfig.json");
            if (!File.Exists(rosConfigPath))
            {
                rosConfigPath = Path.Combine(Application.streamingAssetsPath, "ROSConnectionConfig.json");
            }

            byte[] bytes = UnityEngine.Windows.File.ReadAllBytes(rosConfigPath);
            string jsonString = System.Text.Encoding.ASCII.GetString(bytes);

            config = RosConnectionConfig.CreateFromJSON(jsonString);

            ros.RosIPAddress = config.RosIPAddress;
            ros.RosPort = config.RosPort;
            ros.KeepaliveTime = config.KeepaliveTime;
            ros.NetworkTimeoutSeconds = config.NetworkTimeoutSeconds;
            ros.SleepTimeSeconds = config.SleepTimeSeconds;
            ros.ShowHud = false;
        }

        // Connect to ROS master
        public void Connect()
        {
            LoadRosConnectionConfig();
            ros.Connect();
            Debug.Log("Connected to ROS at " + config.RosIPAddress);
        }

        // Disconnect from ROS master
        public void Disconnect()
        {
            if (ros != null)
            {
                ros.Disconnect();
                Debug.Log("Disconnected from ROS.");
            }
        }

        public void SubscribeImageTopic<T>(string topic, Texture2D tex) where T : Message
        {
            Utils.LogInfo($"Subscribing to topic: {topic}");
            ros.Subscribe<T>(topic, (msg) =>
            {
                if (msg is ImageMsg imgMsg)
                {
                    if (imgMsg.data.Length == 0)
                    {
                        return;
                    }
                    tex.LoadRawTextureData(imgMsg.data);
                    tex.Apply();
                }
                else if (msg is CompressedImageMsg compressedImgMsg)
                {
                    if (compressedImgMsg.data.Length == 0)
                    {
                        return;
                    }
                    ImageConversion.LoadImage(tex, compressedImgMsg.data);
                }
            });
        }
    }
}
