using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit;
using iTeachSkills.DataCapture;

namespace iTeachSkills.EyeTracker
{
    public class EyeTracker : MonoBehaviour
    {
        [SerializeField]
        private GameObject markerPrefab;
        private GameObject hitPointMarker;
        [SerializeField]
        private GameObject objectOfInterest;


        private void Start()
        {
            hitPointMarker = Instantiate(markerPrefab, new Vector3(0, 0, 0), Quaternion.identity);
        }

        private void Update()
        {
            var eyeGazeProvider = CoreServices.InputSystem?.EyeGazeProvider;

            if (eyeGazeProvider == null) { return; }


            Debug.Log("Gaze Origin: " + eyeGazeProvider.GazeOrigin);
            Debug.Log("Gaze Direction: " + eyeGazeProvider.GazeDirection);

            UpdateMarkerVis(new Ray(eyeGazeProvider.GazeOrigin, eyeGazeProvider.GazeDirection));
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
                //UpdateMarkerPos(ref hitPointMarker, hitPoint.Value, false);

            }
        }

        private void ResetMarker(ref GameObject marker)
        {
            Destroy(marker);    // Destroy the marker
        }

        private void InitMarker(ref GameObject marker)
        {
            marker = Instantiate(markerPrefab, new Vector3(0, 0, 0), Quaternion.identity);
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
