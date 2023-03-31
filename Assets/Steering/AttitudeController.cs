using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class AttitudeController : MonoBehaviour, INeutralRollController,IRollController
{
    [Header("Ship properties")]//probably store these in a SO instead of serializing here
    [SerializeField]private float attitudeAuthority = 150.0f;
    [SerializeField,Tooltip("Warning: can't be edited at runtime through the inspector")]private float maxAngularSpeed = Mathf.PI / 2.0f;
    [Header("Controllers")] 
    [SerializeField] private Vector3PidController rollController;
    [SerializeField] private Vector3PidController angularSpeedDampingController;
    [SerializeField] private float targetAttitudeTolerance = 0.05f;
    [SerializeField] private float targetAngularVelocityTolerance = 0.5f;
    private Transform _transform;
    private Rigidbody _rb;
    private void Awake()
    {
        _transform = transform;
        _rb = GetComponent<Rigidbody>();
        _rb.maxAngularVelocity = maxAngularSpeed;
    }

    private Vector3 GetLocalDownDirection()
    {
        //TODO: find down direction that preserves heading
        return Vector3.down;//get the actual value from wherever
    }

    public void RollToNeutral()
    {
        RollToDownDirection(GetLocalDownDirection());
    }

    public void RollToUpDirection(Vector3 up)
    {
        RollToDownDirection(-up);
    }

    public void RollToDownDirection(Vector3 down)
    {
        StopAllCoroutines();
        StartCoroutine(RollToDownDirectionRoutine(down));
    }

    private IEnumerator RollToDownDirectionRoutine(Vector3 down)
    {
        rollController.ResetIntegral();
        angularSpeedDampingController.ResetIntegral();
        
        Vector3 rollPidValue;
        Vector3 angularSpeedDampeningPidValue;
        Vector3 rollError;
        Vector3 angularSpeedError;
        WaitForFixedUpdate waiter = new WaitForFixedUpdate();
        do
        {
            var currentDownDirection = -_transform.up;
            rollError = Vector3.Cross(currentDownDirection, down);
            rollPidValue = rollController.Tick(rollError, Time.fixedDeltaTime);
            
            angularSpeedError = -_rb.angularVelocity;
            angularSpeedDampeningPidValue = angularSpeedDampingController.Tick(angularSpeedError, Time.fixedDeltaTime);
            _rb.AddTorque(Vector3.ClampMagnitude(rollPidValue + angularSpeedDampeningPidValue, attitudeAuthority));
            yield return waiter;

        } while (rollError.magnitude > targetAttitudeTolerance ||
                 _rb.angularVelocity.magnitude > targetAngularVelocityTolerance);
        Debug.Log("Maneuver complete");
        _rb.angularVelocity = Vector3.zero;
    }
}

public interface IRollController
{
    public void RollToUpDirection(Vector3 up);
    public void RollToDownDirection(Vector3 down);
}

public interface INeutralRollController
{
    public void RollToNeutral();
}
