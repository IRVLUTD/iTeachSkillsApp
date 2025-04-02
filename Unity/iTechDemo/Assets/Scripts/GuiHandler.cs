using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Robotics.ROSTCPConnector;

namespace iTeachSkills
{

    public class GuiHandler : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI SSID_Text;
        [SerializeField] private TextMeshProUGUI IP_Text;
        [SerializeField] private TextMeshProUGUI Battery_Text;
        [SerializeField] private TextMeshProUGUI Warning_Text;
        [SerializeField] private TextMeshProUGUI Command_Text;
        [SerializeField] private TextMeshProUGUI Status_Text;
        [SerializeField] private TextMeshProUGUI ROS_Text;
        [SerializeField] private TextMeshProUGUI Summary_Text;

        [Tooltip("Default time in second for text fading")]
        [SerializeField] private float textDisplayTime = 1f;

        private ROSConnection ros;

        void Awake()
        {
            ros = gameObject.GetComponent<ROSConnection>();
        }

        public void Initialize()
        {
            UpdateSystemInfo();
            Status_Text.text = $"<b><color=#ff0000>Recording: OFF</color><space=2em><color=#ff0000>Labeling: OFF</color></b>";
            Warning_Text.text = "";
            Command_Text.text = "";
            Summary_Text.text = "";
            UpdateRosStatus();
            //StartCoroutine(UpdateRosStatusCoroutine(3f));
            InvokeRepeating(nameof(UpdateRosStatus), 3f, 6f);
        }

        public void UpdateSummary(string message)
        {
            if (Summary_Text == null) return;
            Summary_Text.text = message;
        }

        public void UpdateSSID()
        {
            if (SSID_Text == null) return;
            string ssid = Utils.GetSSID();
            SSID_Text.text = string.IsNullOrEmpty(ssid) 
                ? "<b>SSID: <color=#c1121f>N/A</color></b>" 
                : $"<b><color=#ffffff>SSID: {ssid}</color></b>";
        }

        public void UpdateIP()
        {
            if (IP_Text == null) return;
            string ipAddr = Utils.GetIPAddress();
            IP_Text.text = string.IsNullOrEmpty(ipAddr) 
                ? "<b>IP: <color=#c1121f>N/A</color></b>"
                : $"<b>IP: <color=#ffffff>{ipAddr}</color></b>";
        }

        public void UpdateBattery()
        {
            if (Battery_Text == null) return;

            int percentage = Utils.GetBatteryPercentage();
            string color = percentage < 10 ? "#c1121f" 
                : (percentage < 20 ? "#ffb703" : "#a7c957");
            Battery_Text.text = $"<b>Battery: <color={color}>{percentage}%</color></b>";
        }

        public void UpdateStatus(bool isRecording, int labelIndex)
        {
            if (Status_Text == null) return;

            string record = isRecording ? "<color=#a7c957>Recording: ON</color>" : "<color=#ff0000>Recording: OFF</color>";
            string label = labelIndex > 0 ? $"<color=#a7c957>Labeling: Object #{labelIndex}</color>" : "<color=#ff0000>Labeling: OFF</color>";

            Status_Text.text = $"<b>{record}<space=2em>{label}</b>";
        }

        public void UpdateRosStatus()
        {
            if (ROS_Text == null) return;

            //var ros = iTeachSkills.ROS.RosHandler.rosConnection;

            if (ros == null)
            {
                ROS_Text.text = $"<color=#ffb703>ROS: Disconnected</color>";
            }
            else if (!ros.HasConnectionError)
            {
                ROS_Text.text = $"<color=#a7c957>ROS: Connected at {ros.RosIPAddress}:{ros.RosPort} </color>";
            }
            else
            {
                ROS_Text.text = $"<color=#c1121f>ROS: Connection Error</color>";
            }
        }


        public void UpdateCommand(string message)
        {
            if (Command_Text == null) return;

            try
            {
                Command_Text.text = $"<color=#38a3a5><b>Command</b>: {message}</color>";

                // Stop previous fade coroutine before starting new one
                StopCoroutine(ShowTextWithFade(Command_Text, textDisplayTime));

                StartCoroutine(ShowTextWithFade(Command_Text, textDisplayTime));
            }
            catch (System.Exception ex)
            {
                iTeachSkills.Utils.LogError($"Error in UpdateCommand: {ex.Message}");
            }
        }


        public void UpdateWarning(string message)
        {
            if (Warning_Text == null) return;

            iTeachSkills.Utils.LogInfo($"Warning Received: {message}");

            try
            {
                Warning_Text.text = $"<color=#fb8500><b>WARNING</b>: {message}</color>";

                StopCoroutine(ShowTextWithFade(Warning_Text, textDisplayTime));

                StartCoroutine(ShowTextWithFade(Warning_Text, textDisplayTime));
            }
            catch (System.Exception ex)
            {
                iTeachSkills.Utils.LogError($"Error in UpdateWarning: {ex.Message}");
            }
        }

        
        private void UpdateSystemInfo()
        {
            UpdateSSID();
            UpdateIP();
            UpdateBattery();
        }


        private IEnumerator UpdateRosStatusCoroutine(float sec)
        {
            yield return new WaitForSeconds(sec);
            UpdateRosStatus();
        }


        private IEnumerator ShowTextWithFade(TextMeshProUGUI tmpUGUI, float displayTime)
        {
            if (tmpUGUI == null) yield break;

            StopCoroutine(FadeText(tmpUGUI, true));
            StopCoroutine(FadeText(tmpUGUI, false));

            yield return StartCoroutine(FadeText(tmpUGUI, true));
            yield return new WaitForSeconds(displayTime);
            yield return StartCoroutine(FadeText(tmpUGUI, false));
        }


        private IEnumerator FadeText(TextMeshProUGUI tmpUGUI, bool fadeIn, float duration = 1f)
        {
            if (tmpUGUI == null) yield break;

            float elapsedTime = 0f;
            float startAlpha = fadeIn ? 0f : 1f;
            float endAlpha = fadeIn ? 1f : 0f;
            Color originalColor = tmpUGUI.color;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float alpha = Mathf.Lerp(startAlpha, endAlpha, elapsedTime / duration);
                tmpUGUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                yield return null;
            }

            tmpUGUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, endAlpha);
        }
    }
}
