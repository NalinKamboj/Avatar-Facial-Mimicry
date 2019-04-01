using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColorScript : MonoBehaviour {
    public GameObject plane;
    private new MeshRenderer renderer;

    private void Start()
    {
        Debug.Log("ColorScript running");
    }

    public void OnClickChangeColor()
    {
        renderer = plane.GetComponent<MeshRenderer>();
        renderer.enabled = !renderer.enabled;
    }
}
