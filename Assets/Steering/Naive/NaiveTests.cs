using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class NaiveTests : MonoBehaviour
{
    private void OnGUI()
    {
        GUILayout.BeginVertical();
        if(GUILayout.Button("randomAttitude"))
        {
            transform.rotation = Random.rotation;
        }

        if (GUILayout.Button("Activate Roll controller"))
        {
            GetComponent<NaiveRollController>().enabled = true;
        }
        GUILayout.EndVertical();
    }
}
