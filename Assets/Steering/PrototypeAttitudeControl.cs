#define CSV_EXPORT
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;
// ReSharper disable RedundantDefaultMemberInitializer


[RequireComponent(typeof(Rigidbody))]//This is here to provide angular velocity
public class PrototypeAttitudeControl : MonoBehaviour
{
    //maximum torque the craft can apply to itself, here we assume it's isotropic, for production I would fetch this value from a scriptable object
    [SerializeField]private float attitudeAuthority = 150.0f;
    [SerializeField]private float maxAngularSpeed = Mathf.PI / 2.0f;
    
    private Vector3 _currentWorldDownDirection = Vector3.down;
    private Transform _transform;
    private Rigidbody _rb;
#if CSV_EXPORT
    private List<Quaternion> _attitudeList = new ();
    private List<Vector3> _errorList = new ();
    private List<Vector3> _dErrorList = new ();//redundant but simpler
    private List<Vector3> _torqueList = new ();
#endif
    private void Awake()
    {
        _transform = transform;
        _rb = GetComponent<Rigidbody>();
        _rb.maxAngularVelocity = maxAngularSpeed;
    }

    private Quaternion GetNeutralAttitude()
    {
        var neutralAttitude = Quaternion.LookRotation(_transform.forward, -_currentWorldDownDirection);
        return neutralAttitude;
    }

    #if UNITY_EDITOR
    private Quaternion _targetAttitudeVis;
    [FormerlySerializedAs("previs")] [SerializeField] private Transform previewTransform;
    private void OnGUI()
    {
        GUI.contentColor = Color.black;
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
            var unitV2 = Random.insideUnitCircle.normalized;
            _transform.rotation = Quaternion.LookRotation(_transform.forward,_transform.TransformDirection(new Vector3(unitV2.x,unitV2.y,0f)));
        }

        if (GUILayout.Button("random maneuver"))
        {
            StopAllCoroutines();
            // var unitV2 = Random.insideUnitCircle.normalized;
            // var targetAttitude  = Quaternion.LookRotation(_transform.forward,_transform.TransformDirection(new Vector3(unitV2.x,unitV2.y,0f)));
            // Debug.Log("forward:" + _transform.forward + "up:" + new Vector3(unitV2.x,unitV2.y,0f));
            // _targetAttitudeVis = targetAttitude;
            var targetAttitude = Random.rotation;
            previewTransform.rotation = targetAttitude;
            StartCoroutine(RollTo(targetAttitude));
        }

        if (GUILayout.Button("fixed target maneuver"))
        {
            StopAllCoroutines();
            StartCoroutine(RollTo(Quaternion.LookRotation(new Vector3(-0.42f, 0.39f, -0.82f),
                _transform.TransformDirection(new Vector3(0.11f, 0.99f, 0)))));
        }
        GUILayout.Label("error:"+_errorVis);
        GUILayout.Label("dError" + _dErrorVis);
        GUILayout.Label("iError" + _iErrorVis);
        GUILayout.Label("PID:"+_pidVis);
        GUILayout.Label("Torque:"+_torqueVis);
        GUILayout.Label("target attitude:" +_targetAttitudeVis.eulerAngles);
        GUILayout.Label(_saturationVis ? "Saturated!" : "ok");
        #if CSV_EXPORT
        if (GUILayout.Button("Save last maneuver"))
        {
            if (_attitudeList == null || _attitudeList.Count == 0) Debug.LogWarning("No data to save, aborting.");
            else SaveManeuverDataAsCsv();
        }
        #endif
    }
    #if CSV_EXPORT

    private void SaveManeuverDataAsCsv()
    {
        StreamWriter stream = new StreamWriter("Assets/Maneuver" + DateTime.Now.ToString("dd-MM-yyyy_hh-mm-ss") + ".CSV");
        stream.WriteLine("Attitude,,,,Error,,,DError,,,Torque,,");
        for (int i = 0; i < _attitudeList.Count; i++)//assuming the list are the same length here
        {
            var attitude = _attitudeList[i];
            var error = _errorList[i];
            var dError = _dErrorList[i];
            var torque = _torqueList[i];
            stream.WriteLine($"{attitude.x},{attitude.y},{attitude.z},{attitude.w}," +
                             $"{error.x},{error.y},{error.z}," +
                             $"{dError.x},{dError.y},{dError.z}," +
                             $"{torque.x},{torque.y},{torque.z}");
        }
        stream.Close();
        #if UNITY_EDITOR
        AssetDatabase.Refresh();
        #endif
    }
    #endif
#endif
    [SerializeField]private float targetAttitudeTolerance = 0.05f;
    private IEnumerator NeutralRoll()
    {
        yield return RollTo(GetNeutralAttitude());
    }

    
    [Header("PD controller parameters")] //These would need to be estimated from ship data
    [FormerlySerializedAs("PGain")] [SerializeField] private float pGain = 1.0f;
    [FormerlySerializedAs("DGain")] [SerializeField] private float dGain = 1.0f;
    [SerializeField] float iGain = 0f;
    [SerializeField] private bool dynamicDGain = false;
    [SerializeField][Tooltip("maximum dGain authorized, should of course be larger than the initial dGain")] private float dGainmax = 20.0f;
    [SerializeField] private float dGainRateOfChange = 1.0f;//naive solution, it should probably depend on more factor than just time, probably the magnitude of D
    private Vector3 _pidVis;
    private Vector3 _torqueVis;
    private Vector3 _errorVis;
    private Vector3 _dErrorVis;
    private Vector3 _iErrorVis;
    private bool _saturationVis;

    //TODO: Extract PID controller
    //TODO: Convert angles to minimal angles
    private IEnumerator RollTo(Quaternion attitude)
    {
#if CSV_EXPORT
        _attitudeList.Clear();
        _errorList.Clear();
        _dErrorList.Clear();
        _torqueList.Clear();
#endif
        var integralValue = Vector3.zero;
        var modifiedDGain = dGain;
        // ReSharper disable once InconsistentNaming
        Vector3 PIDTick(Vector3 currentError, Vector3 previousError)
        {
            var p = -currentError * pGain;
            var dxError = (previousError.x - currentError.x + 540) % 360 - 180;
            var dyError = (previousError.y - currentError.y + 540) % 360 - 180;
            var dzError = (previousError.z - currentError.z + 540) % 360 - 180;
            var d = new Vector3(dxError,dyError,dzError) * (dynamicDGain? modifiedDGain : dGain);
#if CSV_EXPORT
            _dErrorList.Add(d);
#endif
            integralValue += currentError * Time.fixedDeltaTime;
            integralValue = Vector3.ClampMagnitude(integralValue, attitudeAuthority);
            if (dynamicDGain)
                modifiedDGain = Mathf.Clamp(modifiedDGain + dGainRateOfChange * Time.fixedDeltaTime, 0f, dGainmax);
            var i = integralValue * iGain;//really rough guess for now at it should not matter
            #if UNITY_EDITOR
            _pidVis = p + d + i;
            _errorVis = currentError;
            _dErrorVis = new Vector3(dxError, dyError, dzError);
            _iErrorVis = integralValue;
            var position = _transform.position;
            Debug.DrawRay(position,_transform.TransformDirection(p),Color.red);
            Debug.DrawRay(position,_transform.TransformDirection(d),Color.magenta);
            Debug.DrawRay(position,_transform.TransformDirection(i),Color.yellow);
            #endif
            return p + d + i;
        }

        Vector3 error;
        // ReSharper disable once InconsistentNaming
        Vector3 PIDValue;
        WaitForFixedUpdate waiter = new WaitForFixedUpdate();
        {//initialization tick to prevent derivative kick(we avoid getting a huge initial rate of change of the error)
            var currentAttitude = _transform.rotation.eulerAngles;
            var targetAttitudeAngle = attitude.eulerAngles;//the divergence issue likely come from here
            var xError = (currentAttitude.x - targetAttitudeAngle.x + 540)% 360 - 180;
            var yError = (currentAttitude.y - targetAttitudeAngle.y + 540)% 360 - 180;
            var zError = (currentAttitude.z - targetAttitudeAngle.z + 540)% 360 - 180;
            error = new Vector3(xError, yError, zError);
            PIDValue = PIDTick(error,error);//D = 0, it won't always be correct, but it will rarely cause problem
            var torqueValue = Vector3.ClampMagnitude(PIDValue, attitudeAuthority);
            #if UNITY_EDITOR
            Debug.Log("initial error: " + error.ToString());
            _torqueVis = torqueValue;
            _pidVis = PIDValue;
            Debug.DrawRay(transform.position,torqueValue);
            #endif
            _rb.AddRelativeTorque(torqueValue);
#if CSV_EXPORT
            _attitudeList.Add(_transform.rotation);
            _errorList.Add(error);
            //dError added in the PID controller
            _torqueList.Add(torqueValue);
#endif
            yield return waiter;
        }
        do
        {
            var currentAttitude = _transform.rotation.eulerAngles;
            var targetAttitudeAngle = attitude.eulerAngles;
            var xError = (currentAttitude.x - targetAttitudeAngle.x + 540)% 360 - 180;
            var yError = (currentAttitude.y - targetAttitudeAngle.y + 540)% 360 - 180;
            var zError = (currentAttitude.z - targetAttitudeAngle.z + 540)% 360 - 180;
            var previousError = error;
            error = new Vector3(xError, yError, zError);
            PIDValue = PIDTick(error,previousError);
            var torqueValue = Vector3.ClampMagnitude(PIDValue,attitudeAuthority);
            
            #if UNITY_EDITOR
            _torqueVis = torqueValue;
            _pidVis = PIDValue;
            _saturationVis = PIDValue.magnitude > attitudeAuthority;
            var position = _transform.position;
            Debug.DrawRay(position, attitude * Vector3.forward,Color.blue);
            Debug.DrawRay(position, attitude * Vector3.up,Color.cyan);
            Debug.DrawRay(position, _transform.TransformDirection(torqueValue),Color.green);
            #endif
            _rb.AddRelativeTorque(torqueValue);
#if CSV_EXPORT
            _attitudeList.Add(_transform.rotation);
            _errorList.Add(error);
            //dError added in the PID controller
            _torqueList.Add(torqueValue);
#endif
            yield return waiter;
        } while (error.magnitude > targetAttitudeTolerance || 
                 _rb.angularVelocity.magnitude > 0.5f);//When the pid behaves correctly, only checking the PID value should be enough
        Debug.Log("Maneuver completed");
        _rb.angularVelocity = Vector3.zero;
    }

    public void UpdateDownDirection(Vector3 localDownDirection)
    {
        _currentWorldDownDirection = localDownDirection;
        //Note: we can let a potential ongoing roll correction finish, or we can interrupt them here. 
        //I'll let them conclude with current target attitude here
    }
}