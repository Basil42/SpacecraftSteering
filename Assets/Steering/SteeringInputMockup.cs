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
    private IAttitudeControl _attitudeController;
    private Rigidbody _rb;
    [SerializeField] private float maxAngularVelocity = 7.0f;

    private void Awake()
    {
        _rollController = GetComponent<INeutralRollController>();
        _headingController = GetComponent<IHeadingController>();
        _attitudeController = GetComponent<IAttitudeControl>();
        _rb = GetComponent<Rigidbody>();
        _rb.maxAngularVelocity = maxAngularVelocity;
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

        if (GUILayout.Button("Random attitude maneuver"))
        {
            _attitudeController?.SetDesiredAttitudeTo(Random.rotation);
        }
        GUILayout.Label(_rb.angularVelocity.magnitude.ToString(CultureInfo.InvariantCulture));
        
    }

    
}