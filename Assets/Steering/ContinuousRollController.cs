using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ContinuousRollController : MonoBehaviour, INeutralRollController
{
    private Vector3 _localDown = Vector3.down;
    [SerializeField] private float rollAuthority = 150f;
    [SerializeField] private Vector3PidController rollPidController;
    [SerializeField] private float errorTolerance;

    private Transform _transform;
    private Rigidbody _rb;

    private void Awake()
    {
        _transform = transform;
        _rb = GetComponent<Rigidbody>();
        enabled = false;//assuming we'd want to explicitly enable it
    }

    private void OnEnable()
    {
        rollPidController.ResetIntegral();
        rollPidController.Tick(ComputeRollError(), Time.fixedDeltaTime);//this will suppress derivative kick
    }
    //moving most used values on the stack, we shouldn't need to access them
    private Vector3 _torqueValue;
    private void FixedUpdate()
    {
        var rollError = ComputeRollError();
        var pidValue = rollPidController.Tick(rollError, Time.fixedDeltaTime);
        _torqueValue = Vector3.ClampMagnitude(pidValue, rollAuthority);
        _torqueValue = _transform.InverseTransformDirection(_torqueValue);//converted for animation purposes
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Mathf.Abs(_torqueValue.x) > errorTolerance || Mathf.Abs(_torqueValue.y) > errorTolerance)//fairly confident that it can't happen
        {
            Debug.LogWarning("Roll controller is generating pitch or yaw, it is ignored, but might be a sign that something is wrong with it.");
        }
        _rb.AddRelativeTorque(new Vector3(0.0f,0.0f,_torqueValue.z));//extra precaution, removed in production mode
        #else
        _rb.AddRelativeTorque(_torqueValue);
        
        #endif
    }

    private Vector3 ComputeRollError()
    {
        var targetDown = Vector3.ProjectOnPlane(_localDown, _transform.forward);
        if (targetDown.magnitude < errorTolerance) targetDown = -_transform.up;//in case we're orthogonal to the horizon
        var currentDownDirection = -_transform.up;
        var rollError = Vector3.Cross(currentDownDirection, targetDown);
        if (rollError.magnitude < errorTolerance &&
            Mathf.Abs(Vector3.Angle(currentDownDirection, targetDown) - 180.0f) < errorTolerance)
        {
            rollError = _transform.forward;
        }

        var errorAngle = Vector3.Angle(currentDownDirection, targetDown);
        var errorFactor = errorAngle / 180f;
        rollError = rollError.normalized * errorFactor;
        return rollError;
    }

    public void RollToUpDirection(Vector3 up)
    {
        _localDown = -up;
        enabled = true;
    }

    public void RollToDownDirection(Vector3 down)
    {
        _localDown = down;
        enabled = true;
    }

    public void StopRoll()
    {
        enabled = false;
    }

    public void RollToNeutral()
    {
        enabled = true;
    }

    public void SetNeutralDirection(Vector3 direction)//this can be hooked to an event for example
    {
        _localDown = direction;
    }
}
