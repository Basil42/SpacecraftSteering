using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class SteeringInputMockup : MonoBehaviour
{ 
    [SerializeField] AttitudeController attitudeController;

    private void OnGUI()
    {
        if (GUILayout.Button("Neutral roll"))
        {
            attitudeController?.RollToNeutral();
        }
    }
}
