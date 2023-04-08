using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class SteeringInputMockup : MonoBehaviour
{
    private INeutralRollController _rollController;
    private IHeadingController _headingController;
    private Rigidbody _rb;

    private void Awake()
    {
        _rollController = GetComponent<INeutralRollController>();
        _headingController = GetComponent<IHeadingController>();
        _rb = GetComponent<Rigidbody>();
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Neutral roll"))
        {
            _rollController?.RollToNeutral();
        }

        if (GUILayout.Button("Random Attitude"))
        {
            _rollController?.StopRoll();
            transform.rotation = Random.rotation;
        }

        if (GUILayout.Button("Heading test"))
        {
            _headingController?.ChangeHeadingTo(Vector3.right);
        }
        GUILayout.Label(_rb.angularVelocity.magnitude.ToString(CultureInfo.InvariantCulture));
        
    }

    
}