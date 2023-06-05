using System;
using UnityEngine;
// ReSharper disable RedundantDefaultMemberInitializer

public interface IPidController<T>
{
    public T Tick(T error, float deltaTime);
    public void ResetIntegral();
}
[Serializable]
public class Vector3PidController : IPidController<Vector3>
{
    [SerializeField]private float pGain = 1.0f;
    [SerializeField]private float dGain = 1.0f;
    [SerializeField]private float iGain = 0.0f;
    [SerializeField][Tooltip("maximum amplitude of the stored integral")] private float integralCap = 10.0f;
    // public Vector3PidController(float pGain, float dGain, float iGain = 0.0f)
    // {
    //     this.pGain = pGain;
    //     this.dGain = dGain;
    //     this.iGain = iGain;
    // }

    private Vector3 _previousError;
    private bool _dErrorInit = false;
    private Vector3 _integral;
    public Vector3 Tick(Vector3 error, float deltaTime)
    {
        var p = error * pGain;
        if (!_dErrorInit)
        {
            _previousError = error;
            _dErrorInit = true;
            
        }
        
        var d = (error - _previousError) * dGain;
        _integral = Vector3.ClampMagnitude(_integral + (error * deltaTime), integralCap);
        var i = _integral * iGain;
        _previousError = error;
        return p + d + i;
    }

    public void ResetIntegral()
    {
        _integral = Vector3.zero;
    }
}
