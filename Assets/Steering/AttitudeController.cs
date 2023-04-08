using System.Collections;
using UnityEngine;

//TODO: take a target attitude using quaternion.lookatRotation, extract an up and forward vector from it and run a third PID controller for forward attitude, they could balance out correctly
[RequireComponent(typeof(Rigidbody))]
public class AttitudeController : MonoBehaviour, INeutralRollController,IHeadingController
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

    private Coroutine _rollRoutine;
    public void RollToDownDirection(Vector3 down)
    {
        StopAllCoroutines();
        angularSpeedDampingController.ResetIntegral();//TODO move in OnEnable
        _rollRoutine = StartCoroutine(RollToDownDirectionRoutine(down));
    }

    public void StopRoll()
    {
        //temporary implementation, mostly for debug purposes
        _rb.angularVelocity = Vector3.zero;
    }

    private IEnumerator RollToDownDirectionRoutine(Vector3 down)
    {
        rollController.ResetIntegral();
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
            errorFactor = errorAngle/180f;
            rollError = rollError.normalized * errorFactor;
                        //we use cross product to get the torque axis, but we remap it's magnitude to be 2 at 180 rather than 0
            rollPidValue = rollController.Tick(rollError, Time.fixedDeltaTime);
            
            angularSpeedError = -_rb.angularVelocity;
            angularSpeedDampeningPidValue = angularSpeedDampingController.Tick(angularSpeedError, Time.fixedDeltaTime);
            var totalPidValue = rollPidValue + angularSpeedDampeningPidValue;
            var torqueValue = Vector3.ClampMagnitude(rollPidValue + angularSpeedDampeningPidValue, attitudeAuthority);
            torqueValue = _transform.InverseTransformDirection(torqueValue);
            _rb.AddRelativeTorque(torqueValue);
            yield return waiter;

        } while (rollError.magnitude > errorTolerance ||
                 _rb.angularVelocity.magnitude > targetAngularVelocityTolerance);
        Debug.Log("Maneuver complete");
        _rb.angularVelocity = Vector3.zero;//TODO: move in Ondisable
    }

    public void TurnToward(Vector3 worldPosition)
    {
        ChangeHeadingTo((worldPosition - _transform.position).normalized);
    }

    private Coroutine _headingCoroutine;
    public void ChangeHeadingTo(Vector3 direction)
    {
        if (_headingCoroutine != null)
        {
            StopCoroutine(_headingCoroutine);
            _headingCoroutine = null;
        }
        angularSpeedDampingController.ResetIntegral();//not super happy about that

        StartCoroutine(ChangeHeadingRoutine(direction));
    }

    private IEnumerator ChangeHeadingRoutine(Vector3 direction)
    {
        headingController.ResetIntegral();
        Vector3 headingPidValue;
        //not doing the speed dampening here, I'll move it to update
        Vector3 headingError;
        float errorFactor;
        float errorAngle;
        WaitForFixedUpdate waiter = new WaitForFixedUpdate();
        do
        {
            var currentHeading = _transform.forward;
            headingError = Vector3.Cross(currentHeading, direction);
            if (headingError.magnitude < errorTolerance &&
                Mathf.Abs(Vector3.Angle(currentHeading, direction) - 180.0f) < errorTolerance)
            {
                headingError = transform.up;
            }

            errorAngle = Vector3.Angle(currentHeading, direction);
            errorFactor = errorAngle / 180.0f;
            headingError = headingError.normalized * errorFactor;
            headingPidValue = headingController.Tick(headingError, Time.fixedDeltaTime);
            var torqueValue = Vector3.ClampMagnitude(headingPidValue, attitudeAuthority);//TODO: check both x and y axis separately
            torqueValue = _transform.InverseTransformDirection(torqueValue);
            _rb.AddRelativeTorque(torqueValue);
            yield return waiter;
        } while (headingError.magnitude > errorTolerance ||
                 _rb.angularVelocity.magnitude > targetAngularVelocityTolerance);

        _headingCoroutine = null;
        Debug.Log("heading maneuver completed");
    }
    
}

public interface IRollController
{
    public void RollToUpDirection(Vector3 up);
    public void RollToDownDirection(Vector3 down);

    public void StopRoll();

}

public interface INeutralRollController : IRollController
{
    public void RollToNeutral();
}

public interface IHeadingController
{
    public void TurnToward(Vector3 worldPosition);
    public void ChangeHeadingTo(Vector3 direction);
}
