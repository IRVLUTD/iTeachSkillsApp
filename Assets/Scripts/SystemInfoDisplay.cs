// ----------------------------------------------------------------------------------------------------
// Work done while being at the Intelligent Robotics and Vision Lab at the University of Texas, Dallas
// Please check the licenses of the respective works utilized here before using this script.
// 🖋️ Jishnu Jaykumar Padalunkal (2024).
// ----------------------------------------------------------------------------------------------------


using TMPro;
using System.Net;
using UnityEngine;
using System.Diagnostics;
using Unity.Robotics.ROSTCPConnector; // Import ROS TCP Connector for publishing messages
using iTeachSkills.ROS;

using Debug = UnityEngine.Debug;
using System.Runtime.InteropServices; // Alias to resolve ambiguity

#if WINDOWS_UWP
using Windows.Networking.Connectivity;
using Windows.System.Power;
#endif

public class SystemInfoDisplay : MonoBehaviour
{
    public TextMeshProUGUI batteryPercentageText;
    public TextMeshProUGUI batteryEmojiText;
    public TextMeshProUGUI ssidText;
    public TextMeshProUGUI ssidEmojiText;
    public TextMeshProUGUI ipAddressText;
    public TextMeshProUGUI ipEmojiText;

    // Store a reference to the ROS connection
    private ROSConnection rosConnection;

    private Color textColor = new Color(241f / 255f, 250f / 255f, 238f / 255f); // Normalized RGB for #F1FAEE

    //private Color textColor = new Color(254f / 255f, 250f / 255f, 224f / 255f); // Normalized RGB for #FEFAE0

    void Start()
    {
        batteryPercentageText = GameObject.Find("BatteryIndicator/BatteryPercentageText").GetComponent<TextMeshProUGUI>();
        batteryEmojiText = GameObject.Find("BatteryIndicator/BatteryEmojiText").GetComponent<TextMeshProUGUI>();

        ssidText = GameObject.Find("NetworkIndicator/SSIDIndicator/SSIDText").GetComponent<TextMeshProUGUI>();
        ssidEmojiText = GameObject.Find("NetworkIndicator/SSIDIndicator/SSIDEmojiText").GetComponent<TextMeshProUGUI>();

        ipAddressText = GameObject.Find("NetworkIndicator/IPIndicator/IPText").GetComponent<TextMeshProUGUI>();
        ipEmojiText = GameObject.Find("NetworkIndicator/IPIndicator/IPEmojiText").GetComponent<TextMeshProUGUI>();

        // Apply the color to all text fields
        ApplyTextColor();

        UpdateBatteryInfo();
        UpdateNetworkInfo();
    }

    void ApplyTextColor()
    {
        // Apply the color to each text component
        batteryPercentageText.color = textColor;
        batteryEmojiText.color = textColor;
        ssidText.color = textColor;
        ssidEmojiText.color = textColor;
        ipAddressText.color = textColor;
        ipEmojiText.color = textColor;
    }

    void UpdateBatteryInfo()
    {
#if WINDOWS_UWP
        // PowerManager.RemainingChargePercent provides the battery percentage directly.
        int batteryPercent = PowerManager.RemainingChargePercent;
        batteryPercentageText.text = $"Battery: {batteryPercent}%";
        batteryEmojiText.text = ""; // Optionally add battery emojis based on battery percentage
#else
        batteryPercentageText.text = "N/A";
        batteryEmojiText.text = "";
#endif
    }

    void UpdateNetworkInfo()
    {
#if WINDOWS_UWP
        var profile = NetworkInformation.GetInternetConnectionProfile();
        string ssid = profile?.WlanConnectionProfileDetails.GetConnectedSsid() ?? "N/A";

        // Filter for IPv4 address
        string ipAddress = "N/A";
        foreach (var address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
        {
            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) // IPv4
            {
                ipAddress = address.ToString();
                break;
            }
        }

        ssidText.text = $"SSID: {ssid}";
        ipAddressText.text = $"IP: {ipAddress}";
        ssidEmojiText.text = "";
        ipEmojiText.text = "";
#else
        ssidText.text = "SSID: N/A";
        ipAddressText.text = "IP: N/A";
        ssidEmojiText.text = "";
        ipEmojiText.text = "";
#endif
    }
}
