using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

[RequireComponent(typeof(Rigidbody))]
public class NaiveRollController : MonoBehaviour
{
    private Rigidbody _rb;
    [SerializeField]private float attitudeAuthority = 15f;
    private Vector3 localDown = Vector3.down;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        
    }

    //private bool positiveTorque = true;
    private float stopDistVis;
    private float angularErrorVis;
    private const float ErrorTolerance = 0.005f;
    private const float WobbleTolerance = 0.005f;

    private void FixedUpdate()
    {
        Transform trans = transform;
        float mass = _rb.mass;
        var angularVel = _rb.angularVelocity;

        //Get current dRoll (dRoll)
        var dRoll = (trans.InverseTransformDirection(angularVel.normalized) * angularVel.magnitude).z;

        //get time to dRoll = 0
        var dT0 = dRoll/(mass * attitudeAuthority);
        //get time to targetRoll
        var currentDown = -trans.up;
        var forward = trans.forward;
        var targetDown = Vector3.ProjectOnPlane(localDown, forward);
        var angularError = (Vector3.SignedAngle(currentDown, targetDown, forward)/180f)*Mathf.PI;
        var tTarget = angularError / dRoll;
        //get stopping angular distance
        var stopDist = (dRoll * dT0) + (attitudeAuthority * mass * dT0 * dT0);
        #if UNITY_EDITOR
        //vis
        stopDistVis = stopDist;
        angularErrorVis = angularError;
        // if (stopDist * angularError > 0f != positiveTorque)
        // {
        //     positiveTorque = !positiveTorque;
        //     GetComponentInChildren<Renderer>().material.color = positiveTorque ? Color.green : Color.red;
        // }
        Assert.IsTrue(stopDist >= 0f);
        #endif
        if (Mathf.Abs(angularError) < ErrorTolerance && Mathf.Abs(dRoll) < WobbleTolerance)
        {
            Debug.Log("done");
            enabled = false;
            return;
        }
        //accelerate toward neutral
        if (dRoll * angularError < 0 //we're rolling toward greater error
            ||stopDist < Mathf.Abs(angularError))//we can still accelerate while being able to stop in time
        {
            _rb.AddRelativeTorque((Vector3.forward * angularError).normalized * attitudeAuthority);
        }
        //accelerate away from neutral
        else if (Mathf.Abs(angularError) <= stopDist) //breaking
        {
            _rb.AddRelativeTorque(-(Vector3.forward* angularError).normalized * attitudeAuthority);
        }
        else
        {
            Debug.Log("unhandled case");
        }
    }


    // private void OnGUI()
    // {
    //     GUILayout.BeginVertical();
    //     var angularVel = _rb.angularVelocity;
    //     GUILayout.Label((transform.InverseTransformDirection(angularVel.normalized) * angularVel.magnitude).ToString());
    //     GUILayout.Label($"Error {angularErrorVis}");
    //     GUILayout.Label($"stopDist {stopDistVis}");
    //     GUILayout.Label(((Vector3.forward * angularErrorVis).normalized * attitudeAuthority).ToString());
    //     GUILayout.EndVertical();
    // }
    
}
