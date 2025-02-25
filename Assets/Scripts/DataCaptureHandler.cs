// ----------------------------------------------------------------------------------------------------
// Work done while being at the Intelligent Robotics and Vision Lab at the University of Texas, Dallas
// Please check the licenses of the respective works utilized here before using this script.
// 🖋️ Jishnu Jaykumar Padalunkal (2024).
// 🖋️ Jikai Wang (2025).
// ----------------------------------------------------------------------------------------------------

// System related
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

// Unity related
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Windows.Speech;
using Debug = UnityEngine.Debug; // Alias to resolve ambiguity

// Ros related
using Unity.Robotics.ROSTCPConnector; // Import ROS TCP Connector for publishing messages
using iTeachSkills.ROS;
using RosBoolMsg = RosMessageTypes.Std.BoolMsg; // Import ROS Bool message type
using RosImgMsg = RosMessageTypes.Sensor.ImageMsg;
using compressedRosImgMsg = RosMessageTypes.Sensor.CompressedImageMsg;
using StringMsg = RosMessageTypes.Std.StringMsg;

using TMPro; // Import TextMesh Pro namespace
using Microsoft.MixedReality.Toolkit;





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
        if (prompts.Count ==0)
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

namespace iTeachSkills.DataCapture
{
    
    public class DataCaptureHandler : MonoBehaviour
    {
        // Store a reference to the ROS connection
        private ROSConnection rosConnection;
        private RosConnectionConfig rosConfig;
        private string videoTopic;
        private string labelFrameTopic; // Single ROS topic for getting the label frame
        private string recordCommandTopic; // Single ROS topic for recording commands
        private string sendPromptsTopic; // Single ROS topic for sending prompts to SAM2

        // Video related
        [SerializeField]
        private GameObject videoDisplayGo;
        [SerializeField]
        private RawImage videoDisplay;
        public bool videoDisplayTransparent = false; // Tracks if the video display is transparent or not
        private Texture2D texture;

        public TextMeshProUGUI WarningText; // Reference to WarningText TextMesh Pro object
        public TextMeshProUGUI CommandText; // Reference to CommandText TextMesh Pro object
        public TextMeshProUGUI RecordingStatus; // Reference to RecordingStatus TextMesh Pro object
        public GameObject Canvas; // Reference to the Canvas object
        public Camera MainCamera;
        //private const string videoTopic = "/head_camera/rgb/image_raw/compressed";
        private bool isStreaming = false; // Tracks if the robot stream is active

        // Voice related
        private KeywordRecognizer keywordRecognizer;
        private Dictionary<string, Action> actions = new Dictionary<string, Action>();
        //private const string recordCommandTopic = "/hololens/out/record_command"; // Single ROS topic for recording commands
        private bool isRecording = false; // Tracks recording state
        private float textDisplayTime = 1f; // 1 sec show time for cmd and warning texts

        // ROS IP Port display
        public TextMeshProUGUI ROS_IP_Port;

        // Point Prompt Labels by Eye Gaze
        //private const string sendPromptsTopic = "/hololens/out/prompts"; // Single ROS topic for sending prompts to SAM2
        [SerializeField]
        private GameObject visualMarkerPrefab;
        private GameObject hitPointMarker;
        private LabelsWrapper labelUVs = new();
        private Vector3[] displayCorners;
        private bool isLabeling = false;

        private void Awake()
        {
            videoDisplay = videoDisplayGo.GetComponent<RawImage>();
        }

        // Start is called before the first frame update
        void Start()
        {

            // Attach the canvas to the user's head position
            //AttachCanvasToUser();

            //WarningText.alignment = TextAlignmentOptions.Center; // Horizontally and Vertically centered
            //CommandText.alignment = TextAlignmentOptions.Center; // Horizontally and Vertically centered
            //RecordingStatus.alignment = TextAlignmentOptions.Center; // Horizontally and Vertically centered

            // Start the connection process
            ROS_IP_Port.text = "";
            WarningText.text = "";
            CommandText.text = "";
            RecordingStatus.text = "";
            ROS_IP_Port = GameObject.FindWithTag("ROSIPPortText").GetComponent<TextMeshProUGUI>();
            StartCoroutine(ConnectAndAssign());

            // Set the video display to a transparent state
            MakeVideoDisplayTransparent();

            // Video related
            //int initialWidth = 640;  // Replace with your camera’s resolution
            //int initialHeight = 480;
            //texture = new Texture2D(initialWidth, initialHeight, TextureFormat.RGB24, false);
            //videoDisplay.texture = texture;

            // Eye Gaze related
            displayCorners = GetVideoDisplayCorners();

            // Define commands and associated actions
            actions.Add("stream", RenderRobotStreamOnCanvas);
            actions.Add("begin capture", StartRecord);
            actions.Add("stop capture", StopRecord);
            actions.Add("true label", TrueLabel);
            actions.Add("false label", FalseLabel);
            actions.Add("next object", NextObject);
            actions.Add("send label", SendLabel);

            // Initialize KeywordRecognizer with exact keywords
            keywordRecognizer = new KeywordRecognizer(actions.Keys.ToArray(), ConfidenceLevel.Medium);

            // Attach event handler
            keywordRecognizer.OnPhraseRecognized += RecognizedSpeech;

            // Start the recognizer
            keywordRecognizer.Start();
            Debug.Log("KeywordRecognizer started with commands: " + string.Join(", ", actions.Keys));

            CommandText.text = ""; // Set default command text to empty
            UpdateRecordingStatus(); // Initialize recording status text

        }

        // Update is called once per frame
        void Update()
        {
            if (isLabeling  && !isRecording)
            {
                var eyeGazeProvider = CoreServices.InputSystem?.EyeGazeProvider;
                if (eyeGazeProvider == null || videoDisplayTransparent)
                {
                    return;
                }
                else
                {
                    UpdateVisMarker(new Ray(eyeGazeProvider.GazeOrigin, eyeGazeProvider.GazeDirection));
                }
            }
        }

        void LateUpdate()
        {
            PositionAndFaceCamera();
        }


        // Position the Canvas in front of the Camera and make it face the Camera
        public void PositionAndFaceCamera()
        {
            // Calculate the target position in front of the camera (e.g., 0.2 units away)
            Vector3 targetPosition = MainCamera.transform.position + (MainCamera.transform.forward * 1.7f);

            // Move the Canvas to the target position
            Canvas.transform.position = targetPosition;

            // Get the direction from the Canvas to the Camera
            Vector3 direction = MainCamera.transform.position - Canvas.transform.position;

            // Create the rotation to face the Camera, horizontally or fully (based on your preference)
            //Quaternion rotation = Quaternion.LookRotation(new Vector3(direction.x, direction.y, direction.z)); // Horizontal only
            //Quaternion rotation = Quaternion.LookRotation(direction); // Full 3D rotation

            // Apply the rotation to the Canvas
            //Canvas.transform.rotation = rotation;
        }



        private IEnumerator ConnectAndAssign()
        {

            Color attemptingColorYellow = new Color(255f / 255f, 183f / 255f, 3f / 255f); // Normalized RGB #FFB703 
            Color connectedColorGreen = new Color(167f / 255f, 201f / 255f, 87f / 255f); // Normalized RGB #A7C957
            Color connectionFailureColorRed = new Color(193f / 255f, 18f / 255f, 31f / 255f); // Normalized RGB  #c1121f

            // Display the attempting connection message with IP and Port
            if (ROS_IP_Port != null)
            {
                //ROS_IP_Port.alignment = TextAlignmentOptions.Center; // Horizontally and Vertically centered, not needed as tuned in unity ui
                ROS_IP_Port.color = attemptingColorYellow; // Yellow color for attempting to connect
                ROS_IP_Port.text = "Attempting to connect to ROS Server";
            }

            // Call RosConnect to initiate the connection
            ROSActions.Instance.RosConnect();

            // Optionally, wait for a short period (3 seconds, for example) to allow ROS connection to complete
            yield return new WaitForSeconds(3f);

            // After waiting, store the ROS connection instance
            rosConnection = ROSActions.ros;
            rosConfig = ROSActions.config;

            if (rosConfig != null)
            {
                videoTopic = rosConfig.VideoTopic;
                recordCommandTopic = rosConfig.RecordCommandTopic;
                sendPromptsTopic = rosConfig.SendPromptsTopic;
                labelFrameTopic = rosConfig.LabelFrameTopic;

                texture = new Texture2D(rosConfig.ImageWidth, rosConfig.ImageHeight, TextureFormat.RGB24, false);
                videoDisplay.texture = texture;
            }

            // Check the connection status if needed
            if (rosConnection != null && !rosConnection.HasConnectionError)
            {
                // Display connected message with IP and Port
                if (ROS_IP_Port != null)
                {
                    ROS_IP_Port.color = connectedColorGreen;
                    ROS_IP_Port.text = $"Connected to ROS Server at {rosConnection.RosIPAddress}:{rosConnection.RosPort}";
                }
                Debug.Log("Successfully connected to ROS at " + rosConnection.RosIPAddress);

                // register the publisher topic
                rosConnection.RegisterPublisher<RosBoolMsg>(recordCommandTopic);
                rosConnection.RegisterPublisher<StringMsg>(sendPromptsTopic);

                // Subscribe to the label frame topic
                try
                {
                    Debug.Log($"Subscribing to topic: {labelFrameTopic}");

                    // Subscribe to the video topic and update the texture when a message is received
                    rosConnection.Subscribe<compressedRosImgMsg>(
                        labelFrameTopic,
                        msg =>
                        {
                            ImageConversion.LoadImage(texture, msg.data);
                        }
                    );
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error during SubscribeToLabelFrame: {ex.Message}");
                }
            }
            else
            {
                // Display failure message
                if (ROS_IP_Port != null)
                {
                    ROS_IP_Port.color = connectionFailureColorRed;
                    ROS_IP_Port.text = "Failed to connect to ROS.";
                }
                Debug.LogError("Failed to connect to ROS.");
            }
        }


        private void AttachCanvasToUser()
        {
            //Camera mainCamera = Camera.main;

            //if (mainCamera != null && Canvas != null)
            //{
            //    Canvas.transform.SetParent(null); // Detach from camera to allow independent movement
            //    Canvas.transform.position = mainCamera.transform.position + mainCamera.transform.forward * 2f; // Position canvas 2 meters in front of the user
            //    Canvas.transform.LookAt(mainCamera.transform); // Make it initially face the user
            //    Canvas.transform.Rotate(0, 180, 0); // Correct orientation
            //}
            //else
            //{
            //    Debug.LogError("Main Camera or Canvas not found!");
            //}
        }


        // Cleanup on application quit
        void OnApplicationQuit()
        {
            if (rosConnection != null)
            {
                ROSActions.Instance.OnApplicationQuit();
            }

            // Unsubscribe topics
            rosConnection.Unsubscribe(videoTopic);
            rosConnection.Disconnect();
            Debug.Log("Disconnected from ROS.");

        }

        private void RecognizedSpeech(PhraseRecognizedEventArgs speech)
        {
            try
            {
                Debug.Log($"Recognized command: {speech.text}");

                // Display recognized command text with fade-in and fade-out effects
                StopAllCoroutines(); // Ensure only one fade coroutine runs at a time
                StartCoroutine(ShowCommandWithFade(speech.text, 5f));

                if (actions.ContainsKey(speech.text))
                {
                    actions[speech.text].Invoke();
                }
                else
                {
                    Debug.LogWarning($"Unrecognized command: {speech.text}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while handling recognized speech: {ex.Message}");
            }
        }

        private System.Collections.IEnumerator ShowCommandWithFade(string command, float displayTime)
        {
            CommandText.text = $"Command Received: {command}";
            yield return StartCoroutine(FadeText(CommandText, fadeIn: true)); // Fade-in effect
            yield return new WaitForSeconds(displayTime); // Wait before fade-out
            yield return StartCoroutine(FadeText(CommandText, fadeIn: false)); // Fade-out effect

            Color commandReceivedColorOceanMist = new Color(56f / 255f, 163f / 255f, 165f / 255f);  // Normalized RGB values # 38a3a5

            CommandText.color = commandReceivedColorOceanMist;
            CommandText.text = ""; // Reset to empty after fade-out
        }

        private System.Collections.IEnumerator ShowWarningWithFade(string message, float displayTime)
        {
            WarningText.text = message; // Set the warning message text
            yield return StartCoroutine(FadeText(WarningText, fadeIn: true)); // Fade-in effect
            yield return new WaitForSeconds(displayTime); // Display message for some time
            yield return StartCoroutine(FadeText(WarningText, fadeIn: false)); // Fade-out effect

            Color warningColorOrange = new Color(251f / 255f, 133f / 255f, 0f / 255f); // Normalized RGB #fb8500
            WarningText.color = warningColorOrange;
            WarningText.text = ""; // Clear the text after fade-out
        }


        private System.Collections.IEnumerator FadeText(TextMeshProUGUI textElement, bool fadeIn, float duration = 1f)
        {
            float elapsedTime = 0f;
            Color originalColor = textElement.color;
            float startAlpha = fadeIn ? 0f : 1f;
            float endAlpha = fadeIn ? 1f : 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float alpha = Mathf.Lerp(startAlpha, endAlpha, elapsedTime / duration);
                textElement.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                yield return null;
            }

            // Ensure final alpha is exactly set
            textElement.color = new Color(originalColor.r, originalColor.g, originalColor.b, endAlpha);
        }

        // Function to set the transparency of the video display
        public void SetVideoDisplayTransparency(float alpha)
        {
            if (videoDisplay != null)
            {
                Color currentColor = videoDisplay.color;
                currentColor.a = alpha; // Modify the alpha value (transparency)
                videoDisplay.color = currentColor; // Apply the new transparency

                // Update the transparency state
                videoDisplayTransparent = (alpha < 1f);
            }
            else
            {
                Debug.LogWarning("Video display not assigned.");
            }
        }

        // Example usage: set to transparent or opaque
        public void MakeVideoDisplayTransparent()
        {
            //SetVideoDisplayTransparency(0f); // Fully transparent
            videoDisplayGo.SetActive(false);
        }

        public void MakeVideoDisplayOpaque()
        {
            //SetVideoDisplayTransparency(1f); // Fully opaque
            videoDisplayGo.SetActive(true);
        }

        // Function to display a warning message and fade it out
        private void ShowWarningAndFade(string message)
        {
            try
            {
                Debug.LogWarning(message);

                // Ensure no other warning fade is running
                StopAllCoroutines();

                // Start the coroutine to show the warning message with fade
                StartCoroutine(ShowWarningWithFade(message, textDisplayTime));
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in ShowWarningAndFade: {ex.Message}");
            }
        }

        private void RenderRobotStreamOnCanvas()
        {
            try
            {
                Debug.Log("Inside RenderRobotStreamOnCanvas");

                if (!isRecording)
                {
                    MakeVideoDisplayOpaque();

                    if (isStreaming)
                    {
                        var warnMsg = "Stream command ignored: already streaming.";
                        ShowWarningAndFade(warnMsg);
                        return;
                    }

                    // Start subscribing to the robot's stream
                    SubscribeToStream();
                    isStreaming = true;
                }
                else
                {
                    ShowWarningAndFade("Recording in progress. Try 'stop capture' and then 'stream'.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during RenderRobotStreamOnCanvas: {ex.Message}");
            }
        }

        private void StartRecord()
        {
            try
            {
                if (isStreaming)
                {
                    if (isRecording)
                    {
                        ShowWarningAndFade("Start recording ignored: already recording.");
                        return;
                    }

                    Debug.Log("Inside StartRecord");

                    isRecording = true; // Set recording state to true
                    UpdateRecordingStatus(); // Update UI
                    MakeVideoDisplayTransparent(); // VideoDisplay Pane -> Transparent
                    SendRecordCommand(isRecording); // Send `true` to ROS topic
                    UnsubscribeFromStream(videoTopic); // unsubscribe from stream for optimal usage
                }
                else
                {
                    if (isRecording)
                    {
                        ShowWarningAndFade("'start capture' command ignored: already recording.");
                        return;
                    }
                    var warnMsg = "Inspect the scene with 'stream' command and then try 'start capture'.";
                    ShowWarningAndFade(warnMsg);
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during StartRecord: {ex.Message}");
            }
        }

        private void StopRecord()
        {
            try
            {
                if (!isRecording)
                {
                    var warnMsg = "'stop capture' command ignored: recording not started.";
                    ShowWarningAndFade(warnMsg);
                    return;
                }

                Debug.Log("Inside StopRecord");
                isRecording = false; // Set recording state to false
                isLabeling = true;
                ResetLabelData(); // Reset the label data
                MakeVideoDisplayOpaque();
                SendRecordCommand(isRecording); // Send `false` to ROS topic
                UpdateRecordingStatus(); // Update UI
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during StopRecord: {ex.Message}");
            }
        }

        private void NextObject()
        {
            try
            {
                if (!isLabeling || isRecording)
                {
                    Debug.LogWarning("Next Object command ignored: not in labeling mode.");
                    return;
                }

                Debug.Log("Next Object command received.");

                labelUVs.NewList();
                UpdateRecordingStatus();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during NextObject: {ex.Message}");
            }
        }

        private void SaveLabel(int label)
        {
            try
            {
                // Save the current label position
                if (hitPointMarker != null)
                {
                    Vector3 labelPos = videoDisplayGo.transform.worldToLocalMatrix.MultiplyPoint3x4(hitPointMarker.transform.position);
                    Vector2? uv = GetCursorPosInTexture(labelPos);

                    if (uv != null)
                    {
                        labelUVs.Add(uv.Value.x, uv.Value.y, label);
                        if (label == 1)
                        {
                            DrawUVOnTexture(uv, Color.green);
                        }
                        else
                        {
                            DrawUVOnTexture(uv, Color.red);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Label UV not saved.");
                    }
                }
                else
                {
                    Debug.LogWarning("Eye Gaze Provider not found.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during SavePositiveLabel: {ex.Message}");
            }
        }

        private void TrueLabel()
        {
            if (!isLabeling || isRecording)
            {
                Debug.LogWarning("True Label command ignored: not in labeling mode.");
                return;
            }

            Debug.Log("True Label command received.");

            SaveLabel(1);
        }

        private void FalseLabel()
        {
            if (!isLabeling || isRecording)
            {
                Debug.LogWarning("False Label command ignored: not in labeling mode.");
                return;
            }

            Debug.Log("False Label command received.");

            SaveLabel(0);
        }



        private void SendLabel()
        {
            Debug.Log("Send Label command received.");

            // Set the labeling state to false
            isLabeling = false;

            // Update the recording status
            UpdateRecordingStatus();

            // Send the label UVs to ROS
            SendLabelUVs();

            // Reset the label data
            ResetLabelData();

            // Make the display transparent
            MakeVideoDisplayTransparent();
        }

        private void SubscribeToStream()
        {
            try
            {
                Debug.Log($"Subscribing to topic: {videoTopic}");

                // Subscribe to the video topic and update the texture when a message is received
                rosConnection.Subscribe<compressedRosImgMsg>(
                    videoTopic,
                    msg =>
                    {
                        if (videoDisplayGo.activeSelf && isStreaming)
                        {
                            ImageConversion.LoadImage(texture, msg.data);
                        }

                        //ImageConversion.LoadImage(texture, msg.data);
                    }
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during SubscribeToStream: {ex.Message}");
            }
        }

        // method to handle unsubscribing
        private void UnsubscribeFromStream(string topic)
        {
            isStreaming = false;

            // Log or check if we are successfully unsubscribing
            Debug.Log($"Unsubscribing from topic: {topic}");

            // Unsubscribe from the given ROS topic
            rosConnection.Unsubscribe(topic);
        }

        private void UpdateRecordingStatus()
        {
            try
            {
                // Define calmer color shades

                //Color RecOffColorMossGreen = new Color(167f / 255f, 201f / 255f, 87f / 255f);  // #A7C957
                Color RecOffColorLimeGreen = new Color(138f / 255f, 201f / 255f, 38f / 255f); // Normalized RGB for #8AC926

                Color RecOnColorCrimsonRed = new Color(230f / 255f, 57f / 255f, 70f / 255f);  // #E63946

                Color LabelColorMango = new Color(253f / 255f, 190f / 255f, 2f / 255f); // #FDBE02

                // Update RecordingStatus text and color based on isRecording state
                if (isRecording)
                {
                    RecordingStatus.text = "Recording: ON";
                    RecordingStatus.color = RecOffColorLimeGreen;
                }
                else if (isLabeling)
                {
                    RecordingStatus.text = $"Labeling: Object {labelUVs.Count()}";
                    RecordingStatus.color = LabelColorMango;
                }
                else
                {
                    RecordingStatus.text = "Recording: OFF";
                    RecordingStatus.color = RecOnColorCrimsonRed;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while updating recording status: {ex.Message}");
            }
        }

        private void SendRecordCommand(bool isRecordingCommand)
        {
            try
            {
                var recordCommand = new RosBoolMsg { data = isRecordingCommand };
                rosConnection.Publish(recordCommandTopic, recordCommand);
                Debug.Log($"Sent record command: {(isRecordingCommand ? "Start" : "Stop")} to topic {recordCommandTopic}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while sending record command to ROS: {ex.Message}");
            }
        }

        void OnDestroy()
        {
            try
            {
                if (keywordRecognizer != null && keywordRecognizer.IsRunning)
                {
                    keywordRecognizer.Stop();
                    keywordRecognizer.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during cleanup in OnDestroy: {ex.Message}");
            }
        }

        // For realsense camera, compression is required else real feed and subscribed feed lags a lot, not in sync
        // For Fetch robot compressed camera
        void ReceiveCompressedImage(compressedRosImgMsg message)
        {
            // Extract the compressed image data
            byte[] imageData = message.data;

            // Schedule the texture update to the main thread
            ScheduleCompressedTextureUpdate(imageData);
        }

        // Schedule the texture update for compressed images to be applied on the main thread
        private async void ScheduleCompressedTextureUpdate(byte[] imageData)
        {
            // Await until the main thread can apply the texture
            await Task.Yield();  // Ensures the texture update happens on the main thread

            // Load the compressed image data into the texture
            texture.LoadImage(imageData);  // Load directly from compressed data
            texture.Apply();  // Apply on main thread

            // Set the updated texture to the video display
            videoDisplay.texture = texture;

            // Optionally log the size of the image received
            Debug.Log($"Compressed image applied: {imageData.Length} bytes");
        }

        // For Fetch robot camera
        void ReceiveImage(RosImgMsg message)
        {
            // do vflip always for fetch camera, hflip is not needed
            byte[] flippedImageData = iTeachSkills.ROS.Utils.ConvertBGRToRGBAndFlip(message.data, (int)message.width, (int)message.height, false, false, true);

            // Schedule the texture update to the main thread
            ScheduleTextureUpdate(flippedImageData);
        }


        // Schedule the texture update to be applied on the main thread
        private async void ScheduleTextureUpdate(byte[] flippedImageData)
        {
            // Await until the main thread can apply the texture
            await Task.Yield();  // Ensures the texture update happens on the main thread

            texture.LoadRawTextureData(flippedImageData);
            texture.Apply();  // Apply on main thread
        }

        private void SendLabelUVs()
        {
            try
            {
                // Create a ROS message with the UVs JSON
                var data = new StringMsg { data = labelUVs.SaveToString() };
                // Publish the label UVs to the ROS topic
                rosConnection.Publish(sendPromptsTopic, data);
                Debug.Log("Sent label UVs to ROS topic: " + sendPromptsTopic);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while sending label UVs to ROS: {ex.Message}");
            }
        }

        private void UpdateVisMarker(Ray ray)
        {
            Vector3? hitPoint = PerformHitTest(ray);
            if (hitPoint != null)
            {
                UpdateMarkerPos(ref hitPointMarker, hitPoint.Value, true);
            }
            else
            {
                ResetMarker(ref hitPointMarker);
            }
        }

        private void ResetMarker(ref GameObject marker)
        {
            Destroy(marker);    // Destroy the marker
        }

        private void InitMarker(ref GameObject marker)
        {
            marker = Instantiate(visualMarkerPrefab, new Vector3(0, 0, 0), Quaternion.identity) as GameObject;
            marker.SetActive(true);
        }


        // Corners in local space: BottomLeft, TopLeft, TopRight, BottomRight
        private Vector3[] GetVideoDisplayCorners()
        {
            // Get the RectTransform component
            RectTransform rt = videoDisplayGo.GetComponent<RectTransform>();

            // Get the corners of the video display
            rt.ForceUpdateRectTransforms();

            Vector3[] corners = new Vector3[4];
            rt.GetLocalCorners(corners);

            return corners;
        }


        private void UpdateMarkerPos(ref GameObject marker, Vector3 newPos, bool show)
        {
            if (marker == null)
            {
                InitMarker(ref marker);
            }
            else
            {
                marker.SetActive(show);
                marker.transform.position = newPos;
            }
        }

        private Vector3? PerformHitTest(Ray ray)
        {
            RaycastHit hitInfo = new RaycastHit();
            bool isHit = UnityEngine.Physics.Raycast(ray, out hitInfo);

            if (isHit)
            {
                //Debug.Log("Hit point: " + hitInfo.point);
                return hitInfo.point;
            }
            else
            {
                //Debug.Log("No hit point");
                return null;
            }
        }

        private Vector2? GetCursorPosInTexture(Vector3 hitPosition)
        {
            Vector2? hitPointUV = null;

            try
            {
                Vector3 imageSize = displayCorners[2] - displayCorners[0];
                Vector3 hitPos = hitPosition - displayCorners[0];

                float uvx = hitPos.x / imageSize.x;
                float uvy = hitPos.y / imageSize.y;
                hitPointUV = new Vector2(uvx, uvy);
            }
            catch (UnityEngine.Assertions.AssertionException)
            {
                Debug.LogError(">> AssertionException");
            }

            return hitPointUV;
        }

        private void DrawUVOnTexture(Vector3? uv, Color color, Int32 size = 5)
        {
            if (uv != null)
            {
                int x = Mathf.FloorToInt(uv.Value.x * texture.width);
                int y = Mathf.FloorToInt(uv.Value.y * texture.height);
                for (int i = x - size; i < x + size; i++)
                {
                    for (int j = y - size; j < y + size; j++)
                    {
                        texture.SetPixel(i, j, color);
                    }
                }
                texture.Apply();
            }
        }

        private void ResetLabelData()
        {
            // Clear the label data
            labelUVs.Clear();
            labelUVs.NewList();

            // Reset the visual marker
            ResetMarker(ref hitPointMarker);
        }
    }
}