using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;


[RequireComponent(typeof(Rigidbody))]//This is here to provide angular velocity
public class AttitudeControl : MonoBehaviour
{
    //maximum torque the craft can apply to itself, here we assume it's isotropic, for production I would fetch this value from a scriptable object
    [SerializeField]private float attitudeAuthority = 150.0f;
    [SerializeField]private float maxAngularSpeed = Mathf.PI / 2.0f;
    
    private Vector3 _currentWorldDownDirection = Vector3.down;
    private Transform _transform;
    private Rigidbody _rb;
    private void Awake()
    {
        _transform = transform;
        _rb = GetComponent<Rigidbody>();
        _rb.maxAngularVelocity = maxAngularSpeed;
    }

    private Quaternion GetNeutralAttitude()
    {
        var neutralAttitude = Quaternion.LookRotation(_transform.forward, -_currentWorldDownDirection);
        #if UNITY_EDITOR
        Debug.DrawRay(_transform.position,  neutralAttitude * _transform.forward,Color.red,5.0f);
        #endif
        return neutralAttitude;
    }

    #if UNITY_EDITOR
    private void OnGUI()
    {
        if (GUILayout.Button("neutral roll test"))
        {
            StopAllCoroutines();
            StartCoroutine(NeutralRoll());
        }

        if (GUILayout.Button("neutral roll snap"))
        {
            _transform.rotation = GetNeutralAttitude();
        }

        if (GUILayout.Button("random attitude"))
        {
            StopAllCoroutines();
            _transform.rotation = Random.rotation;
        }
        GUILayout.Label("attitude" + _attitudeVis.ToString());
        GUILayout.Label("Forward" + _transform.forward.ToString());
        GUILayout.Label("error:"+_errorVis.ToString());
        GUILayout.Label("PID:"+_pidVis.ToString());
        GUILayout.Label("Torque:"+_torqueVis.ToString());
        var position = _transform.position;
        Debug.DrawLine(position,position + _currentWorldDownDirection*10.0f);
    }
#endif
    [SerializeField]private float targetAttitudeTolerance = 0.05f;
    private IEnumerator NeutralRoll()
    {
        yield return RollTo(GetNeutralAttitude());
    }

    
    [Header("PID controller parameters")] //These would need to be estimated from ship data
    [FormerlySerializedAs("PGain")] [SerializeField] private float pGain = 1.0f;
    [FormerlySerializedAs("DGain")] [SerializeField] private float dGain = 1.0f;
    //[FormerlySerializedAs("IGain")] [SerializeField] private float iGain = 0.0f;
    private Vector3 _attitudeVis;
    private Vector3 _pidVis;
    private Vector3 _torqueVis;
    private Vector3 _errorVis;
    //TODO: Extract PID controller
    //TODO: Convert angles to minimal angles
    private IEnumerator RollTo(Quaternion attitude)
    {
        // ReSharper disable once InconsistentNaming
        Vector3 PIDTick(Vector3 currentError, Vector3 previousError)
        {
            var p = -currentError * pGain;
            var d = (previousError - currentError) * dGain;
            var i = Vector3.zero;//(currentError - previousError) * (_rb.angularDrag * iGain);//really rough guess for now at it should not matter
            return p + d + i;
        }

        Vector3 error;
        // ReSharper disable once InconsistentNaming
        Vector3 PIDValue;
        WaitForFixedUpdate waiter = new WaitForFixedUpdate();
        {//initialization tick to prevent derivative kick(we avoid getting a huge initial rate of change of the error)
            var currentAttitude = _transform.rotation.eulerAngles;
            _attitudeVis = currentAttitude;
            var targetAttitudeAngle = attitude.eulerAngles;
            var xError = (currentAttitude.x - targetAttitudeAngle.x + 540)% 360 - 180;
            var yError = (currentAttitude.y - targetAttitudeAngle.y + 540)% 360 - 180;
            var zError = (currentAttitude.z - targetAttitudeAngle.z + 540)% 360 - 180;
            error = new Vector3(xError, yError, zError);
            PIDValue = PIDTick(error,error);//D = 0, it won't always be correct, but it will rarely cause problem
            _pidVis = PIDValue;
            var torqueValue = Vector3.ClampMagnitude(PIDValue, attitudeAuthority);
            _torqueVis = torqueValue;
            Debug.DrawRay(transform.position,torqueValue);
            _rb.AddRelativeTorque(torqueValue);
            yield return waiter;
        }
        do
        {
            var currentAttitude = _transform.rotation.eulerAngles;
            _attitudeVis = currentAttitude;
            var targetAttitudeAngle = attitude.eulerAngles;
            var xError = (currentAttitude.x - targetAttitudeAngle.x + 540)% 360 - 180;
            var yError = (currentAttitude.y - targetAttitudeAngle.y + 540)% 360 - 180;
            var zError = (currentAttitude.z - targetAttitudeAngle.z + 540)% 360 - 180;
            var previousError = error;
            error = new Vector3(xError, yError, zError);
            _errorVis = error;
            PIDValue = PIDTick(error,previousError);
            _pidVis = PIDValue;
            var torqueValue = Vector3.ClampMagnitude(PIDValue,attitudeAuthority);
            _torqueVis = torqueValue;
            
            Debug.DrawRay(transform.position,torqueValue);
            _rb.AddRelativeTorque(torqueValue);
            yield return waiter;
        } while (error.magnitude > targetAttitudeTolerance || 
                 PIDValue.magnitude > 0.005f || 
                 _rb.angularVelocity.magnitude > 0.05f);//When the pid behaves correctly, only checking the PID value should be enough
        Debug.Log("Maneuver completed");
    }

    public void UpdateDownDirection(Vector3 localDownDirection)
    {
        _currentWorldDownDirection = localDownDirection;
        //Note: we can let a potential ongoing roll correction finish, or we can interrupt them here. 
        //I'll let them conclude with current target attitude here
    }
}