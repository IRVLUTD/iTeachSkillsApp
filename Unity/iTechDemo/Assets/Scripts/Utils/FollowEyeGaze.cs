using UnityEngine;
using Debug=UnityEngine.Debug;

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Utilities;

using Utils = iTeachSkills.Utils;


namespace iTeachSkills.EyeTracking
{
    public class FollowEyeGaze : MonoBehaviour
    {
        [SerializeField] private GameObject eyeTrackingCursor;
        public static Vector3 hitPoint;

        void Awake()
        {
            eyeTrackingCursor = GameObject.Find("EyeGazeCursor");
        }


        private void Update()
        {
            var eyeGazeProvider = CoreServices.InputSystem?.EyeGazeProvider;

            if (eyeGazeProvider == null)
            {
                return;
            }
            else
            {
                Ray ray = new Ray(eyeGazeProvider.GazeOrigin, eyeGazeProvider.GazeDirection);
                hitPoint = Utils.PerformHitTest(ray);
                //if (hitPoint.Equals(Vector3.zero))
                //{
                //    Utils.UpdatePosition(ref eyeTrackingCursor, Vector3.zero, false);
                //    //Utils.UpdatePosition(ref eyeTrackingCursor, eyeGazeProvider.GazeOrigin + eyeGazeProvider.GazeDirection.normalized * 2.0f, true);
                //}
                //else
                //{
                //    Utils.UpdatePosition(ref eyeTrackingCursor, hitPoint, true);
                //}

                eyeTrackingCursor.transform.position = eyeGazeProvider.GazeOrigin + eyeGazeProvider.GazeDirection.normalized * 2.0f;
            }
        }


        public static Vector3 GetHitPosition()
        {
            return hitPoint;
        }
    }
}
