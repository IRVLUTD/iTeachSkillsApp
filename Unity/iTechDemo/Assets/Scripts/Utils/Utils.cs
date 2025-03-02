using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Net;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;


#if WINDOWS_UWP
using Windows.Networking.Connectivity;
using Windows.System.Power;
#endif

namespace iTeachSkills
{
    public class Utils
    {
        public static void LogInfo(string message)
        {
            Debug.Log(message);
        }

        public static void LogError(string message)
        {
            Debug.LogError(message);
        }

        public static void LogWarning(string message)
        {
            Debug.LogWarning(message);
        }

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

        public static int GetBatteryPercentage()
        {
            int batteryPercentage = -1;

#if WINDOWS_UWP
            batteryPercentage = PowerManager.RemainingChargePercent;
#endif
            return batteryPercentage;
        }

        public static string GetSSID()
        {
            string ssid = "";

#if WINDOWS_UWP
            var icp = NetworkInformation.GetInternetConnectionProfile();
            if (icp != null && icp.WlanConnectionProfileDetails != null)
            {
                ssid = icp.WlanConnectionProfileDetails.GetConnectedSsid();
            }
#endif
            return ssid;
        }

        public static string GetIPAddress()
        {
            string ipAddress = "";

            // Find the WiFi IPv4 address
            foreach (var address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) // IPv4
                {
                    ipAddress = address.ToString();
                    break;
                }
            }
            return ipAddress;
        }

        public static void UpdatePosition(ref GameObject go, Vector3 newPos, bool show)
        {
            go.SetActive(show);
            go.transform.position = newPos;
        }

        public static Vector3 PerformHitTest(Ray ray)
        {
            RaycastHit hitInfo = new RaycastHit();
            bool isHit = UnityEngine.Physics.Raycast(ray, out hitInfo);

            if (isHit)
            {
                return hitInfo.point;
            }
            else
            {
                return Vector3.zero;
            }
        }

        private void DrawTriangleOnTexture(Texture2D texture, Vector3? uv, Color color, int size = 5)
        {
            if (texture == null)
            {
                Debug.LogError("DrawTriangleOnTexture: Texture is null!");
                return;
            }

            if (uv == null) return;

            int x = Mathf.FloorToInt(uv.Value.x * texture.width);
            int y = Mathf.FloorToInt(uv.Value.y * texture.height);

            // Loop to draw the filled triangle
            for (int i = 0; i < size; i++)
            {
                for (int j = -i; j <= i; j++)
                {
                    int pixelX = x + j;
                    int pixelY = y - i;

                    // Ensure we don't go outside the texture bounds
                    if (pixelX >= 0 && pixelX < texture.width && pixelY >= 0 && pixelY < texture.height)
                    {
                        texture.SetPixel(pixelX, pixelY, color);
                    }
                }
            }
        }

    }
}

