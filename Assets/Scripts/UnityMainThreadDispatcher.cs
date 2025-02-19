// ----------------------------------------------------------------------------------------------------
// Work done while being at the Intelligent Robotics and Vision Lab at the University of Texas, Dallas
// Please check the licenses of the respective works utilized here before using this script.
// 🖋️ Jishnu Jaykumar Padalunkal (2024).
// ----------------------------------------------------------------------------------------------------


using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;

    // Queue for actions to be executed on the main thread
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();

    // Property to access the singleton instance
    public static UnityMainThreadDispatcher Instance
    {
        get
        {
            if (_instance == null)
            {
                // Create a new GameObject with this component if it doesn't exist
                GameObject obj = new GameObject("MainThreadDispatcher");
                _instance = obj.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(obj); // Prevent destruction on scene load
            }
            return _instance;
        }
    }

    // Enqueue an action to be executed on the main thread
    public static void Enqueue(Action action)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }

    void Update()
    {
        // Execute actions queued for the main thread
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }
}
