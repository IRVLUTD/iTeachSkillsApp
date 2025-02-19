// ----------------------------------------------------------------------------------------------------
// Work done while being at the Intelligent Robotics and Vision Lab at the University of Texas, Dallas
// Please check the licenses of the respective works utilized here before using this script.
// 🖋️ Jishnu Jaykumar Padalunkal (2024).
// ----------------------------------------------------------------------------------------------------


// System related
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

// Unity related
using UnityEngine;
using Debug = UnityEngine.Debug; // Alias to resolve ambiguity

// Ros related
using Unity.Robotics.ROSTCPConnector;
using RosImgMsg = RosMessageTypes.Sensor.ImageMsg;
using compressedRosImgMsg = RosMessageTypes.Sensor.CompressedImageMsg;


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

        public static RosConnectionConfig CreateFromJSON(string jsonString)
        {
            return JsonUtility.FromJson<RosConnectionConfig>(jsonString);
        }
    }

    public class Utils {
        
        public static byte[] ConvertBGRToRGBAndFlip(byte[] imageData, int width, int height, bool doBGR2RGB, bool doHFlip, bool doVFlip)
        {
            byte[] flippedImageData = new byte[imageData.Length];

            // If no flipping is required, simplify the code
            if (!doHFlip && !doVFlip && !doBGR2RGB)
            {
                // If no flipping or color conversion is needed, simply copy the data
                System.Array.Copy(imageData, flippedImageData, imageData.Length);
                return flippedImageData;
            }

            int rowStride = width * 3;

            // Parallelize the row processing
            Parallel.For(0, height, y =>
            {
                int srcRowStart = y * rowStride;
                int dstY = doVFlip ? (height - y - 1) : y;  // Vertical flipping logic
                int dstRowStart = dstY * rowStride;

                for (int x = 0; x < width; x++)
                {
                    int srcIndex = srcRowStart + x * 3;
                    int dstX = doHFlip ? (width - x - 1) : x;  // Horizontal flipping logic
                    int dstIndex = dstRowStart + dstX * 3;

                    // If BGR to RGB conversion is needed
                    if (doBGR2RGB)
                    {
                        flippedImageData[dstIndex] = imageData[srcIndex + 2];     // Red
                        flippedImageData[dstIndex + 1] = imageData[srcIndex + 1]; // Green
                        flippedImageData[dstIndex + 2] = imageData[srcIndex];     // Blue
                    }
                    else
                    {
                        flippedImageData[dstIndex] = imageData[srcIndex];         // Blue
                        flippedImageData[dstIndex + 1] = imageData[srcIndex + 1]; // Green
                        flippedImageData[dstIndex + 2] = imageData[srcIndex + 2]; // Red
                    }
                }
            });

            return flippedImageData;
        }
    }

    public class ROSActions : MonoBehaviour
    {
        // Singleton pattern to ensure only one ROS connection instance
        public static ROSActions Instance;
        public static ROSConnection ros;
        private string rosConfigPath;

        private void Awake()
        {
            // Ensure only one instance of ROSActions
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);  // Keep this object alive across scenes
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // Load ROS connection configuration
        private RosConnectionConfig LoadRosConnectionConfig()
        {
            rosConfigPath = Path.Combine(Application.persistentDataPath, "ROSConnectionConfig.json");
            if (!File.Exists(rosConfigPath))
            {
                rosConfigPath = Path.Combine(Application.streamingAssetsPath, "ROSConnectionConfigTest.json");
            }

            byte[] bytes = UnityEngine.Windows.File.ReadAllBytes(rosConfigPath);
            string jsonString = System.Text.Encoding.ASCII.GetString(bytes);
            return RosConnectionConfig.CreateFromJSON(jsonString);
        }

        // Connect to ROS server
        public void RosConnect()
        {
            var config = LoadRosConnectionConfig();
            ros = ROSConnection.GetOrCreateInstance();
            ros.RosIPAddress = config.RosIPAddress;
            ros.RosPort = config.RosPort;
            ros.KeepaliveTime = config.KeepaliveTime;
            ros.NetworkTimeoutSeconds = config.NetworkTimeoutSeconds;
            ros.SleepTimeSeconds = config.SleepTimeSeconds;
            ros.ShowHud = config.ShowHud;
            ros.Connect();

            Debug.Log("Connected to ROS at " + config.RosIPAddress);
        }

        // Start connection when script starts
        public void Start()
        {
            try
            {
                RosConnect();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Failed to connect to ROS: " + ex.Message);
            }
        }

        // Disconnect when the application quits
        public void OnApplicationQuit()
        {
            if (ros != null)
            {
                ros.Disconnect();
                Debug.Log("Disconnected from ROS.");
            }
        }
    }
}
