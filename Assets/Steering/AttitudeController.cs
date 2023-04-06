using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

//TODO: take a target attitude using quaternion.lookatRotation, extract an up and forward vector from it and run a third PID controller for forward attitude, they could balance out correctly
[RequireComponent(typeof(Rigidbody))]
public class AttitudeController : MonoBehaviour, INeutralRollController,IRollController,IHeadingController
{
    [Header("Ship properties")]//probably store these in a SO instead of serializing here
    [SerializeField]private float attitudeAuthority = 150.0f;
    [SerializeField,Tooltip("Warning: can't be edited at runtime through the inspector")]private float maxAngularSpeed = Mathf.PI / 2.0f;//it is possible to overload some of the serialization methods to dynamically update the rigidbody properties
    [Header("Controllers")] 
    [SerializeField] private Vector3PidController rollController;
    [SerializeField] private Vector3PidController angularSpeedDampingController;
    [SerializeField] private Vector3PidController headingController;
    [SerializeField] private float errorTolerance = 0.005f;
    [SerializeField] private float targetAngularVelocityTolerance = 0.5f;//This could be replaced by a dynamic value equal to the maximum delta of angular velocity the object can produce in a physics tick
    private Transform _transform;
    private Rigidbody _rb;
    private void Awake()
    {
        _transform = transform;
        _rb = GetComponent<Rigidbody>();
        _rb.maxAngularVelocity = maxAngularSpeed;
    }

    private Vector3 GetCurrentDownDirection()
    {
        //TODO: find down direction that preserves heading
        return Vector3.down;//get the actual value from wherever
    }

    private Vector3 GetCurrentHorizonAlignedDownDirection()
    {
        var result = Vector3.ProjectOnPlane(GetCurrentDownDirection(), _transform.forward);
        if (result.magnitude < errorTolerance)
        {
            return -_transform.up; //case where the object is oriented to be orthogonal to the horizon plane
        }
        Debug.DrawRay(_transform.position,result,Color.red,5.0f);
        return result;
    }

    public void RollToNeutral()
    {
        RollToDownDirection(GetCurrentHorizonAlignedDownDirection());
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
        float errorFactor;
        float errorAngle;
        WaitForFixedUpdate waiter = new WaitForFixedUpdate();
        do
        {
            var currentDownDirection = -_transform.up;
            rollError = Vector3.Cross(currentDownDirection, down);
            if (rollError.magnitude < errorTolerance && Mathf.Abs(Vector3.Angle(currentDownDirection,down) - 180.0f) < errorTolerance)//handling 180 edge case
            {
                rollError = transform.forward;
            }

            errorAngle = Vector3.Angle(currentDownDirection, down);
            errorFactor = -(Mathf.Cos((errorAngle/180f)*Mathf.PI)-1f);
            rollError = rollError.normalized * errorFactor;
                        //we use cross product to get the torque axis, but we remap it's magnitude to be 2 at 180 rather than 0
            rollPidValue = rollController.Tick(rollError, Time.fixedDeltaTime);
            
            angularSpeedError = -_rb.angularVelocity;
            angularSpeedDampeningPidValue = angularSpeedDampingController.Tick(angularSpeedError, Time.fixedDeltaTime);
            var torqueValue = Vector3.ClampMagnitude(rollPidValue + angularSpeedDampeningPidValue, attitudeAuthority);
            torqueValue = _transform.InverseTransformDirection(torqueValue);
            _rb.AddRelativeTorque(torqueValue);
            yield return waiter;

        } while (rollError.magnitude > errorTolerance ||
                 _rb.angularVelocity.magnitude > targetAngularVelocityTolerance);
        Debug.Log("Maneuver complete");
        _rb.angularVelocity = Vector3.zero;
    }

    public void TurnToward(Vector3 worldPosition)
    {
        ChangeHeadingTo((worldPosition - _transform.position).normalized);
    }

    
    public void ChangeHeadingTo(Vector3 direction)
    {
        
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

public interface IHeadingController
{
    public void TurnToward(Vector3 worldPosition);
    public void ChangeHeadingTo(Vector3 direction);
}
