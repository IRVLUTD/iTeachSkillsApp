// ----------------------------------------------------------------------------------------------------
// Work done while being at the Intelligent Robotics and Vision Lab at the University of Texas, Dallas
// Please check the licenses of the respective works utilized here before using this script.
// 🖋️ Jishnu Jaykumar Padalunkal (2024).
// ----------------------------------------------------------------------------------------------------

// System related
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

// Unity related
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug; // Alias to resolve ambiguity

// Ros related
using Unity.Robotics.ROSTCPConnector; // Import ROS TCP Connector for publishing messages
using iTeachSkills.ROS;
using RosBoolMsg = RosMessageTypes.Std.BoolMsg; // Import ROS Bool message type
using RosImgMsg = RosMessageTypes.Sensor.ImageMsg;
using compressedRosImgMsg = RosMessageTypes.Sensor.CompressedImageMsg;

using TMPro; // Import TextMesh Pro namespace
//using Microsoft.MixedReality.Toolkit.Input; // Import Mixed Reality Toolkit for input handling
//using Microsoft.MixedReality.Toolkit;

using UnityEngine.Windows.Speech;
using System.Threading.Tasks;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit;
using System.Security.AccessControl;
using UnityEngine.Timeline;

namespace iTeachSkills.DataCapture
{

    public class DataCaptureHandler : MonoBehaviour
    {
        // Store a reference to the ROS connection
        private ROSConnection rosConnection;

        // Video related
        public RawImage videoDisplay;
        public bool videoDisplayTransparent = false; // Tracks if the video display is transparent or not

        private Texture2D texture;
        public TextMeshProUGUI WarningText; // Reference to WarningText TextMesh Pro object
        public TextMeshProUGUI CommandText; // Reference to CommandText TextMesh Pro object
        public TextMeshProUGUI RecordingStatus; // Reference to RecordingStatus TextMesh Pro object
        public GameObject Canvas; // Reference to the Canvas object
        public Camera MainCamera;

        private const string videoTopic = "/head_camera/rgb/image_raw/compressed";
        private bool isStreaming = false; // Tracks if the robot stream is active

        // Voice related
        private KeywordRecognizer keywordRecognizer;
        private Dictionary<string, Action> actions = new Dictionary<string, Action>();
        private const string recordCommandTopic = "/hololens/out/record_command"; // Single ROS topic for recording commands
        private bool isRecording = false; // Tracks recording state
        private float textDisplayTime = 1f; // 1 sec show time for cmd and warning texts

        // ROS IP Port display
        public TextMeshProUGUI ROS_IP_Port;

        // Point Prompt Labels by Eye Gaze
        [SerializeField]
        private GameObject visualMarkerPrefab;
        [SerializeField]
        private GameObject labelMarkerPrefab;
        private GameObject hitPointMarker;
        private List<Vector3> labelPositions = new List<Vector3>();
        //private GameObject[] labelMarkers;
        private List<GameObject> labelMarkers;
        private bool isLabeling = false;

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
            int initialWidth = 640;  // Replace with your camera’s resolution
            int initialHeight = 480;
            texture = new Texture2D(initialWidth, initialHeight, TextureFormat.RGB24, false);
            videoDisplay.texture = texture;

            // Eye Gaze related
            hitPointMarker = Instantiate(visualMarkerPrefab, new Vector3(0, 0, 0), Quaternion.identity);




            // Define commands and associated actions
            actions.Add("stream", RenderRobotStreamOnCanvas);
            actions.Add("start capture", StartRecord);
            actions.Add("stop capture", StopRecord);
            actions.Add("start label", StartLabel);
            actions.Add("save label", SaveLabel);

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
            if (isLabeling)
            {
                var eyeGazeProvider = CoreServices.InputSystem?.EyeGazeProvider;
                if (eyeGazeProvider == null || videoDisplayTransparent)
                {
                    return;
                }
                else {
                    UpdateMarkerVis(new Ray(eyeGazeProvider.GazeOrigin, eyeGazeProvider.GazeDirection));
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

            // Check the connection status if needed
            if (rosConnection != null && !rosConnection.HasConnectionError)
            {
                // Display connected message with IP and Port
                if (ROS_IP_Port != null)
                {
                    ROS_IP_Port.color = connectedColorGreen; // Green color for successful connection
                    ROS_IP_Port.text = $"Connected to ROS Server at {rosConnection.RosIPAddress}:{rosConnection.RosPort}";
                }
                Debug.Log("Successfully connected to ROS at " + rosConnection.RosIPAddress);

                // register the publisher topic
                rosConnection.RegisterPublisher<RosBoolMsg>(recordCommandTopic);
            }
            else
            {
                // Display failure message
                if (ROS_IP_Port != null)
                {
                    ROS_IP_Port.color = connectionFailureColorRed; // Green color for successful connection
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
            SetVideoDisplayTransparency(0f); // Fully transparent
        }

        public void MakeVideoDisplayOpaque()
        {
            SetVideoDisplayTransparency(1f); // Fully opaque
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

                    labelMarkers = new List<GameObject>();

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
                UpdateRecordingStatus(); // Update UI
                MakeVideoDisplayTransparent(); // VideoDisplay Pane -> Transparent
                SendRecordCommand(isRecording); // Send `false` to ROS topic
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during StopRecord: {ex.Message}");
            }
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
                        byte[] imageData = msg.data;
                        texture.LoadImage(imageData);
                        texture.Apply();
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
            //rosConnection.Unsubscribe<compressedRosImgMsg>(topic);
        }

        private void UpdateRecordingStatus()
        {
            try
            {
                // Define calmer color shades

                //Color RecOffColorMossGreen = new Color(167f / 255f, 201f / 255f, 87f / 255f);  // #A7C957
                Color RecOffColorLimeGreen = new Color(138f / 255f, 201f / 255f, 38f / 255f); // Normalized RGB for #8AC926

                Color RecOnColorCrimsonRed = new Color(230f / 255f, 57f / 255f, 70f / 255f);  // #E63946

                // Update RecordingStatus text and color based on isRecording state
                if (isRecording)
                {
                    RecordingStatus.text = "Recording: ON";
                    RecordingStatus.color = RecOffColorLimeGreen;
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

        private void StartLabel()
        {
            Debug.Log("Start Label command received.");

            isLabeling = true;


        }

        private void SaveLabel()
        {
            Debug.Log("Save Label command received.");

            if (isLabeling)
            {
                // Save the current label position
                if (hitPointMarker != null)
                {
                    labelPositions.Add(hitPointMarker.transform.position);
                    Debug.Log("Label position saved: " + hitPointMarker.transform.position);
                }
                else
                {
                    Debug.LogWarning("Eye Gaze Provider not found.");
                }

                // Update the label markers
                MakeLabelMarkers();

                isLabeling = false;
            }
            else
            {
                Debug.LogWarning("Save Label command ignored: not in labeling mode.");
            }
        }

        private void UpdateMarkerVis(Ray ray)
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
        }

        private void ResetLabelMarkers()
        {
            if (labelMarkers != null)
            {
                foreach (var marker in labelMarkers)
                {
                    Destroy(marker);
                }
            }
        }

        private void MakeLabelMarkers()
        {
            if (hitPointMarker != null)
            {
                var marker = Instantiate(labelMarkerPrefab, new Vector3(0, 0, 0), Quaternion.identity) as GameObject;
                UpdateMarkerPos(ref marker, hitPointMarker.transform.position, true);
                labelMarkers.Add(marker);
                ResetMarker(ref hitPointMarker);
            }
        }




        private void UpdateMarkerPos(ref GameObject marker, Vector3 newPos, bool show)
        {
            if (marker == null)
            {
                InitMarker(ref marker);
            }
            marker.SetActive(show);
            marker.transform.position = newPos;
        }

        private Vector3? PerformHitTest(Ray ray)
        {
            RaycastHit hitInfo = new RaycastHit();
            bool isHit = UnityEngine.Physics.Raycast(ray, out hitInfo);

            if (isHit)
            {
                Debug.Log("Hit point: " + hitInfo.point);
                return hitInfo.point;
            }
            else
            {
                Debug.Log("No hit point");
                return null;
            }
        }

    }
}