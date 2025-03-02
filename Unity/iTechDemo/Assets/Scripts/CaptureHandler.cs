using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ROS related libraries
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using RosBoolMsg = RosMessageTypes.Std.BoolMsg;
using RosImgMsg = RosMessageTypes.Sensor.ImageMsg;
using CompressedRosImgMsg = RosMessageTypes.Sensor.CompressedImageMsg;
using StringMsg = RosMessageTypes.Std.StringMsg;


namespace iTeachSkills
{
    public class CaptureHandler : MonoBehaviour
    {
        [System.Serializable]
        public class LabelsWrapper
        {
            public List<PromptData> prompts;

            public LabelsWrapper()
            {
                prompts = new List<PromptData>();
            }


            [System.Serializable]
            public class PromptData
            {
                public List<Vector2> points = new();
                public List<int> labels = new();

                public int Count()
                {
                    return points.Count;
                }

                public void Add(float x, float y, int label)
                {
                    points.Add(new Vector2(x, y));
                    labels.Add(label);
                }

                public void Remove(int index)
                {
                    if (points.Count >= index)
                    {
                        points.RemoveAt(index);
                        labels.RemoveAt(index);
                    }
                }

                public void Clear()
                {
                    points.Clear();
                    labels.Clear();
                }
            }

            public void Add(float x, float y, int label)
            {
                if (prompts.Count == 0)
                {
                    prompts.Add(new PromptData());
                }
                prompts[prompts.Count - 1].Add(x, y, label);
            }

            public void NewList()
            {
                prompts.Add(new PromptData());
            }

            public void Remove(int index)
            {
                if (prompts.Count >= index)
                {
                    prompts.RemoveAt(index);
                }
            }

            public void Remove(int index, int subIndex)
            {
                if (prompts.Count >= index && prompts[index].Count() >= subIndex)
                {
                    prompts[index].Remove(subIndex);
                }
            }

            public int Count()
            {
                return prompts.Count;
            }

            public void Clear()
            {
                prompts.Clear();
            }

            public string SaveToString()
            {
                return JsonUtility.ToJson(this, false);
            }
        }


        // VideoDisplay
        [SerializeField] private GameObject videoDisplay;

        // ROS related variables
        [SerializeField] private string videoTopic;
        [SerializeField] private string labelFrameTopic;
        [SerializeField] private string recordCommandTopic;
        [SerializeField] private string sendPromptsTopic;

        // Local variables
        private GuiHandler guiHandler;
        private GameObject videoFrame;
        private RawImage rawImage;
        private Texture2D imgTexture;
        private bool isStreaming = false;
        private bool isRecording = false;
        private bool isLabeling = false;
        private int labelIndex = 0;
        private Texture2D texture;
        private LabelsWrapper labelPrompts = new LabelsWrapper();

        void Awake()
        {
            // Find the video frame object
            videoFrame = videoDisplay.transform.Find("Canvas/VideoFrame").gameObject;

            // Get the GUI handler
            guiHandler = gameObject.GetComponent<GuiHandler>();
        }

        void OnEnable()
        {
            // Initialize ROS
            InitializeROS();

            // Initialize GUI
            InitializeGUI();
        }

        void Start()
        {

        }

        // Disconnect when the application quits
        void OnApplicationQuit()
        {
            UnsubscribeFromStream();
            ROS.RosHandler.rosConnection.Unsubscribe(labelFrameTopic);
            SendRecordCommand(false);
            ROS.RosHandler.Instance.RosDisconnect();
        }


        public void Stream()
        {
            Utils.LogInfo("Command Received: Stream");

            guiHandler?.UpdateCommand("Stream");

            if (isStreaming)
            {
                Utils.LogWarning("Already streaming");
                guiHandler?.UpdateWarning("Already streaming");
                return;
            }

            videoFrame.SetActive(true);
            SubscribeToStream();
        }

        public void BeginCapture()
        {
            Utils.LogInfo("Command Received: Begin Capture");

            guiHandler?.UpdateCommand("Begin Capture");

            if (!isStreaming)
            {
                Utils.LogWarning("Cannot begin capture without streaming");
                guiHandler?.UpdateWarning("Cannot begin capture without streaming");
                return;
            }
            else if (isRecording)
            {
                Utils.LogWarning("Capture already in progress");
                guiHandler?.UpdateWarning("Capture already in progress");
                return;
            }
            else if (isLabeling)
            {
                //Utils.LogWarning("Cannot begin capture while labeling");
                //guiHandler?.UpdateWarning("Cannot begin capture while labeling");
                ResetLabels();
            }

            isRecording = true;
            videoFrame.SetActive(false);
            SendRecordCommand(isRecording);
            UnsubscribeFromStream();
            guiHandler?.UpdateStatus(isRecording, labelIndex);

        }

        public void StopCapture()
        {
            Utils.LogInfo("Command Received: Stop Capture");

            guiHandler?.UpdateCommand("Stop Capture");

            if (!isRecording)
            {
                Utils.LogWarning("No capture in progress");
                guiHandler?.UpdateWarning("No capture in progress");
                return;
            }
            else
            {
                isRecording = false;
                videoFrame.SetActive(true);
                SendRecordCommand(isRecording);
                InitializeLabels();
                guiHandler?.UpdateStatus(isRecording, labelIndex);
            }
        }

        public void TrueLabel()
        {
            Utils.LogInfo("Command Received: True Label");

            guiHandler?.UpdateCommand("True Label");

            if (!isLabeling)
            {
                Utils.LogWarning("No label in progress");
                guiHandler?.UpdateWarning("No label in progress");
                return;
            }
            else
            {
                AddOneLabel(1);
            }
        }

        public void FalseLabel()
        {
            Utils.LogInfo("Command Received: False Label");

            guiHandler?.UpdateCommand("False Label");

            if (!isLabeling)
            {
                Utils.LogWarning("No label in progress");
                guiHandler?.UpdateWarning("No label in progress");
                return;
            }
            else
            {
                AddOneLabel(0);
            }
        }

        public void SendLabel()
        {
            Utils.LogInfo("Command Received: Send Label");

            guiHandler?.UpdateCommand("Send Label");

            if (!isLabeling)
            {
                Utils.LogWarning("No label in progress");
                guiHandler?.UpdateWarning("No label in progress");
                return;
            }
            else
            {
                SendPrompts();
            }
        }

        public void NextObject()
        {
            Utils.LogInfo("Command Received: Next Object");
            guiHandler?.UpdateCommand("Next Object");

            if (!isLabeling)
            {
                Utils.LogWarning("No label in progress");
                guiHandler?.UpdateWarning("No label in progress");
                return;
            }
            else
            {
                labelIndex++;
                labelPrompts.NewList();
                guiHandler?.UpdateStatus(isRecording, labelIndex);
            }
        }

        public void StopLabel()
        {
            Utils.LogInfo("Command Received: Stop Label");

            guiHandler?.UpdateCommand("Stop Label");

            if (!isLabeling)
            {
                Utils.LogWarning("No label in progress");
                guiHandler?.UpdateWarning("No label in progress");
                return;
            }
            else
            {
                videoFrame.SetActive(false);
                // Send the label prompts
                SendPrompts();

                // Reset the labels
                ResetLabels();

                // Update the GUI
                guiHandler?.UpdateStatus(isRecording, labelIndex);
            }
        }

        private void InitializeROS()
        {
            // Connect to ROS master
            try
            {
                ROS.RosHandler.Instance.RosConnect();

                // Get ROS connection and configuration
                //rosConnection = ROS.RosHandler.rosConnection;

                // Get ROS topics
                var rosConfig = ROS.RosHandler.config;
                videoTopic = rosConfig.VideoTopic;
                labelFrameTopic = rosConfig.LabelFrameTopic;
                recordCommandTopic = rosConfig.RecordCommandTopic;
                sendPromptsTopic = rosConfig.SendPromptsTopic;

                // Initialize image texture
                imgTexture = new Texture2D(rosConfig.ImageWidth, rosConfig.ImageHeight, TextureFormat.RGB24, false);

                // Register publishers
                if (!string.IsNullOrEmpty(recordCommandTopic))
                {
                    Utils.LogInfo($"Registering publisher topic: {recordCommandTopic}");
                    ROS.RosHandler.rosConnection.RegisterPublisher<RosBoolMsg>(recordCommandTopic);
                }

                if (!string.IsNullOrEmpty(sendPromptsTopic))
                {
                    Utils.LogInfo($"Registering publisher topic: {sendPromptsTopic}");
                    ROS.RosHandler.rosConnection.RegisterPublisher<StringMsg>(sendPromptsTopic);
                }

                // Subscribe to label frame topic
                if (!string.IsNullOrEmpty(labelFrameTopic)){
                    SubscribeImageTopic<CompressedRosImgMsg>(labelFrameTopic);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Error in InitializeROS: " + ex.Message);
            }
        }

        private void InitializeGUI()
        {
            guiHandler.Initialize();
            videoFrame.SetActive(false);


            rawImage = videoFrame.GetComponent<RawImage>();
            rawImage.texture = imgTexture;
        }

        private void SendRecordCommand(bool isRecording)
        {
            if (!string.IsNullOrEmpty(recordCommandTopic))
            {
                RosBoolMsg msg = new RosBoolMsg { data = isRecording };
                ROS.RosHandler.rosConnection.Publish(recordCommandTopic, msg);

                Utils.LogInfo($"Sending record command: {isRecording}");
            }
        }

        private void SendPrompts()
        {
            string prompts = labelPrompts.SaveToString();

            if (!string.IsNullOrEmpty(sendPromptsTopic))
            {
                StringMsg msg = new StringMsg { data = prompts };
                ROS.RosHandler.rosConnection.Publish(sendPromptsTopic, msg);
                Utils.LogInfo($"Label prompts sent: {prompts}");
            }
        }

        private Vector3[] GetVideoFrameWorldCorners()
        {
            // First, get the four corners of the video frame
            Vector3[] corners = new Vector3[4];
            videoFrame.GetComponent<RectTransform>().GetWorldCorners(corners);

            // Calculate the width and height of the video frame
            // Corners order: bottom-left, top-left, top-right, bottom-right
            return corners;

        }

        private bool IsValidUV(float x, float y)
        {
            return x >= 0 && x <= 1 && y >= 0 && y <= 1;
        }

        private void DrawLabelOnTexture(ref Texture2D tex, float x, float y, int label, int size = 5)
        {
            //DrawTriangleOnTexture(ref tex, x, y, label == 1 ? Color.green : Color.red, size);
            DrawCircleOnTexture(ref tex, x, y, label == 1 ? Color.green : Color.red, size);
            //DrawStarOnTexture(ref tex, x, y, label == 1 ? Color.green : Color.red, size);
            imgTexture.Apply();
        }

        private void DrawCircleOnTexture(ref Texture2D tex, float uvX, float uvY, Color color, int radius)
        {
            if (tex == null)
            {
                Debug.LogError("DrawCircleOnTexture: Texture is null!");
                return;
            }

            int centerX = Mathf.FloorToInt(uvX * tex.width);
            int centerY = Mathf.FloorToInt(uvY * tex.height);

            for (int i = -radius; i <= radius; i++)
            {
                for (int j = -radius; j <= radius; j++)
                {
                    int pixelX = centerX + i;
                    int pixelY = centerY + j;

                    // Ensure we don't go outside the texture bounds
                    if (pixelX >= 0 && pixelX < tex.width && pixelY >= 0 && pixelY < tex.height)
                    {
                        if (i * i + j * j <= radius * radius) // Circle formula
                        {
                            tex.SetPixel(pixelX, pixelY, color);
                        }
                    }
                }
            }
        }

        //private void DrawStarOnTexture(ref Texture2D tex, float uvX, float uvY, Color color, int size)
        //{
        //    if (tex == null)
        //    {
        //        Debug.LogError("DrawStarOnTexture: Texture is null!");
        //        return;
        //    }

        //    int x = Mathf.FloorToInt(uvX * tex.width);
        //    int y = Mathf.FloorToInt(uvY * tex.height);

        //    // Draw cross shape (+)
        //    for (int i = -size; i <= size; i++)
        //    {
        //        if (x + i >= 0 && x + i < tex.width) // Horizontal check
        //            tex.SetPixel(x + i, y, color); // Horizontal line

        //        if (y + i >= 0 && y + i < tex.height) // Vertical check
        //            tex.SetPixel(x, y + i, color); // Vertical line
        //    }

        //    // Draw diagonal shape (X)
        //    for (int i = -size; i <= size; i++)
        //    {
        //        if (x + i >= 0 && x + i < tex.width && y + i >= 0 && y + i < tex.height) // Main diagonal
        //            tex.SetPixel(x + i, y + i, color);

        //        if (x + i >= 0 && x + i < tex.width && y - i >= 0 && y - i < tex.height) // Anti diagonal
        //            tex.SetPixel(x + i, y - i, color);
        //    }
        //}

        private void DrawSquareOnTexture(ref Texture2D tex, float x, float y, Color color, int size)
        {
            int uv_x = Mathf.FloorToInt(x * tex.width);
            int uv_y = Mathf.FloorToInt(y * tex.height);
            for (int i = uv_x - size; i < uv_x + size; i++)
            {
                for (int j = uv_y - size; j < uv_y + size; j++)
                {
                    tex.SetPixel(i, j, color);
                }
            }
        }

        private void DrawTriangleOnTexture(ref Texture2D tex, float uvx, float uvy, Color color, int size = 5)
        {
            int x = Mathf.FloorToInt(uvx * tex.width);
            int y = Mathf.FloorToInt(uvy * tex.height);

            // Loop to draw the filled triangle
            for (int i = 0; i < size; i++)
            {
                for (int j = -i; j <= i; j++)
                {
                    int pixelX = x + j;
                    int pixelY = y - i;

                    // Ensure we don't go outside the texture bounds
                    if (pixelX >= 0 && pixelX < tex.width && pixelY >= 0 && pixelY < tex.height)
                    {
                        tex.SetPixel(pixelX, pixelY, color);
                    }
                }
            }
        }


        private void AddOneLabel(int label)
        {
            var frameCorners = GetVideoFrameWorldCorners();
            //Utils.LogInfo($"Frame Corners: {frameCorners[0]}, {frameCorners[1]}, {frameCorners[2]}, {frameCorners[3]}");
            Utils.LogInfo($"Image size: {frameCorners[2] - frameCorners[0]}");

            //var hitPosWorld = EyeTracking.FollowEyeGaze.GetHitPosition();
            var hitPosWorld = EyeTracking.FollowEyeGaze.hitPoint;

            float x = (hitPosWorld.x - frameCorners[0].x) / (frameCorners[2].x - frameCorners[0].x);
            float y = (hitPosWorld.y - frameCorners[0].y) / (frameCorners[2].y - frameCorners[0].y);


            //var hitPosLocal = videoFrame.transform.worldToLocalMatrix.MultiplyPoint3x4(hitPosWorld);
            //Utils.LogInfo($"Hit position local: {hitPosLocal}");

            //float x = (hitPosLocal.x - frameCorners[0].x) / (frameCorners[2].x - frameCorners[0].x);
            //float y = (hitPosLocal.y - frameCorners[0].y) / (frameCorners[1].y - frameCorners[0].y);

            if (IsValidUV(x, y))
            {
                labelPrompts.Add(x, y, label);
                Utils.LogInfo($"Label added: {x}, {y}, {label}");
                guiHandler?.UpdateWarning($"Label added: {x}, {y}, {label}");

                // Draw the label on the video frame

                DrawLabelOnTexture(ref imgTexture, x, y, label);                
                Utils.LogInfo("Label drawn on the video frame");
            }
            else
            {
                Utils.LogWarning($"Invalid UV coordinates [{x}, {y}]");
                guiHandler?.UpdateWarning("Invalid UV coordinates!!!");    
            }
        }

        public void DebugLog()
        {
            videoFrame.SetActive(true);
            AddOneLabel(1);
        }

        private void InitializeLabels()
        {

            isLabeling = true;
            labelIndex = 1;

            // Clear the label prompts data
            labelPrompts.Clear();
            labelPrompts.NewList();
        }

        private void ResetLabels()
        {
            isLabeling = false;
            labelIndex = 0;

            // Clear the label prompts data
            labelPrompts.Clear();
        }

        private void SubscribeImageTopic<T>(string topic) where T : Message
        {
            Utils.LogInfo($"Subscribing to topic: {topic}");
            ROS.RosHandler.rosConnection.Subscribe<T>(topic, (msg) =>
            {
                if (msg is RosImgMsg imgMsg)
                {
                    if (imgMsg.data.Length == 0)
                    {
                        return;
                    }
                    imgTexture.LoadRawTextureData(imgMsg.data);
                    imgTexture.Apply();
                }
                else if (msg is CompressedRosImgMsg compressedImgMsg)
                {
                    if (compressedImgMsg.data.Length == 0)
                    {
                        return;
                    }
                    ImageConversion.LoadImage(imgTexture, compressedImgMsg.data);
                }
            });
        }

        private void SubscribeToStream()
        {
            SubscribeImageTopic<CompressedRosImgMsg>(videoTopic);
            isStreaming = true;
        }

        private void UnsubscribeFromStream()
        {
            ROS.RosHandler.rosConnection.Unsubscribe(videoTopic);
            isStreaming = false;
        }

    }
}
