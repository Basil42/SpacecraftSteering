using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class SteeringInputMockup : MonoBehaviour
{
    [SerializeField] AttitudeController attitudeController;

    private void OnGUI()
    {
        if (GUILayout.Button("Neutral roll"))
        {
            attitudeController?.RollToNeutral();
        }

        if (GUILayout.Button("Random Attitude"))
        {
            transform.rotation = Random.rotation;
        }
    }

    private void Start()
    {
        attitudeController?.RollToNeutral();
    }
}