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
        [SerializeField] private string summaryInfoTopic;

        // Local variables
        private ROS.RosHandler rosHandler;
        private GuiHandler guiHandler;
        private GameObject videoFrame;
        private RawImage rawImage;
        private Texture2D imgTexture;
        private Texture2D labelTexture;
        private bool isStreaming = false;
        private bool isRecording = false;
        private bool isLabeling = false;
        private int labelIndex = 0;
        private Texture2D texture;
        private LabelsWrapper labelPrompts = new LabelsWrapper();
        private GameObject summaryInfoPanel;
        private string summaryInfo = "";
        private bool isSummaryInfo = false;

        void Awake()
        {
            // Find the video display object
            videoDisplay = GameObject.Find("MixedRealitySceneContent/VideoDisplay");

            // Find the video frame object
            videoFrame = videoDisplay.transform.Find("Canvas/VideoFrame").gameObject;

            // Find the summary info panel
            summaryInfoPanel = videoDisplay.transform.Find("Canvas/Summary_Info").gameObject;

            // Get the GUI handler
            guiHandler = gameObject.GetComponent<GuiHandler>();
        }

        void Start()
        {
            // Initialize ROS
            InitializeROS();

            // Initialize GUI
            InitializeGUI();
        }

        // Disconnect when the application quits
        void OnApplicationQuit()
        {
            UnsubscribeFromStream();
            rosHandler.ros.Unsubscribe(labelFrameTopic);
            rosHandler.ros.Unsubscribe(videoTopic);
            SendRecordCommand(false);
            rosHandler.Disconnect();
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

            rawImage.texture = imgTexture;
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
                isLabeling = true;
                rawImage.texture = labelTexture;
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

        public void EraseLabel()
        {
            Utils.LogInfo("Command Received: Erase Label");
            guiHandler?.UpdateCommand("Erase Current Labels");
            if (!isLabeling)
            {
                Utils.LogWarning("No label in progress");
                guiHandler?.UpdateWarning("No label in progress");
                return;
            }
            else if (labelIndex == 0)
            {
                Utils.LogWarning("No labels to erase");
                guiHandler?.UpdateWarning("No labels to erase");
                return;
            }
            else
            {
                Utils.LogInfo($"Erasing current labels for index {labelIndex}");
                labelPrompts.Remove(labelIndex - 1);
                labelPrompts.NewList();
                guiHandler?.UpdateWarning("Label erased");
                SendPrompts();
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

        public void ShowSummaryInfo()
        {
            Utils.LogInfo("Command Received: Show Summary Info");
            guiHandler?.UpdateCommand("Show Summary Info");
            if (isSummaryInfo)
            {
                summaryInfoPanel.SetActive(false);
                isSummaryInfo = false;
            }
            else
            {
                summaryInfoPanel.SetActive(true);
                guiHandler?.UpdateSummary(summaryInfo);
                isSummaryInfo = true;
            }
        }

        private void InitializeROS()
        {
            // Get the ROS handler
            rosHandler = new ROS.RosHandler(gameObject.GetComponent<ROSConnection>());

            // Connect to ROS master
            try
            {
                rosHandler.Connect();

                // Get ROS connection configuration
                videoTopic = rosHandler.config.VideoTopic;
                labelFrameTopic = rosHandler.config.LabelFrameTopic;
                recordCommandTopic = rosHandler.config.RecordCommandTopic;
                sendPromptsTopic = rosHandler.config.SendPromptsTopic;
                summaryInfoTopic = rosHandler.config.SummaryInfoTopic;

                // Initialize image texture
                var width = rosHandler.config.ImageWidth;
                var height = rosHandler.config.ImageHeight;
                imgTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
                labelTexture = new Texture2D(width, height, TextureFormat.RGB24, false);

                // Register publishers
                if (!string.IsNullOrEmpty(recordCommandTopic))
                {
                    Utils.LogInfo($"Registering publisher topic: {recordCommandTopic}");
                    rosHandler.ros.RegisterPublisher<RosBoolMsg>(recordCommandTopic);
                }

                if (!string.IsNullOrEmpty(sendPromptsTopic))
                {
                    Utils.LogInfo($"Registering publisher topic: {sendPromptsTopic}");
                    rosHandler.ros.RegisterPublisher<StringMsg>(sendPromptsTopic);
                }

                // Subscribe to the label frame topic
                if (!string.IsNullOrEmpty(labelFrameTopic))
                {
                    Utils.LogInfo($"Subscribing to topic: {labelFrameTopic}");
                    rosHandler.ros.Subscribe<CompressedRosImgMsg>(labelFrameTopic, (msg) =>
                    {
                        if (!isLabeling || msg.data.Length == 0)
                        {
                            return;
                        }

                        ImageConversion.LoadImage(labelTexture, msg.data);
                        rawImage.texture = labelTexture;
                    });
                }

                // Subscribe to the summary info topic
                if (!string.IsNullOrEmpty(summaryInfoTopic))
                {
                    Utils.LogInfo($"Subscribing to topic: {summaryInfoTopic}");
                    rosHandler.ros.Subscribe<StringMsg>(summaryInfoTopic, (msg) =>
                    {
                        summaryInfo = msg.data;
                    });
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Error in InitializeROS: " + ex.Message);
            }
        }

        private void InitializeGUI()
        {
            rawImage = videoFrame.GetComponent<RawImage>();
            //rawImage.texture = imgTexture;
            videoFrame.SetActive(false);
            summaryInfoPanel.SetActive(false);
            guiHandler.Initialize();
        }

        private void SendRecordCommand(bool isRecording)
        {
            if (!string.IsNullOrEmpty(recordCommandTopic))
            {
                RosBoolMsg msg = new RosBoolMsg { data = isRecording };
                rosHandler.ros.Publish(recordCommandTopic, msg);

                Utils.LogInfo($"Sending record command: {isRecording}");
            }
        }

        private void SendPrompts()
        {
            string prompts = labelPrompts.SaveToString();

            if (!string.IsNullOrEmpty(sendPromptsTopic))
            {
                StringMsg msg = new StringMsg { data = prompts };
                rosHandler.ros.Publish(sendPromptsTopic, msg);
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
            DrawCircleOnTexture(ref tex, x, y, label == 1 ? Color.green : Color.red, size);
            tex.Apply();
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

            if (IsValidUV(x, y))
            {
                labelPrompts.Add(x, y, label);
                Utils.LogInfo($"Label added: {x}, {y}, {label}");
                guiHandler?.UpdateWarning($"Label added: {x}, {y}, {label}");

                // Draw the label on the video frame
                DrawLabelOnTexture(ref labelTexture, x, y, label);                
                Utils.LogInfo("Label drawn on the video frame");
            }
            else
            {
                Utils.LogWarning($"Invalid UV coordinates [{x}, {y}]");
                guiHandler?.UpdateWarning("Invalid UV coordinates!!!");    
            }
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

        private void SubscribeToStream()
        {
            isStreaming = true;

            //SubscribeImageTopic<CompressedRosImgMsg>(videoTopic);
            Utils.LogInfo($"Subscribing to topic: {videoTopic}");
            rosHandler.ros.Subscribe<CompressedRosImgMsg>(videoTopic, (msg) =>
            {
                if (isStreaming && msg.data.Length > 0)
                {
                    ImageConversion.LoadImage(imgTexture, msg.data);
                }
            });
        }

        private void UnsubscribeFromStream()
        {
            isStreaming = false;
            rosHandler.ros.Unsubscribe(videoTopic);
        }
    }
}
