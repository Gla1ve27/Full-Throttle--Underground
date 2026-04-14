//______________________________________________//
//___________Realistic Engine Sounds____________//
//______________________________________________//
//_______Copyright © 2019 Yugel Mobile__________//
//______________________________________________//
//_________ http://mobile.yugel.net/ ___________//
//______________________________________________//
//________ http://fb.com/yugelmobile/ __________//
//______________________________________________//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if REALISTIC_ENGINE_SOUND_UNITYCARPRO_DEMO
public class CameraController : MonoBehaviour {

    CarCameras carCamera;

    public string cameraMainName = "Camera Main";
    public GameObject exterior;
    public GameObject interior;
    // stock
    private GameObject carCam;
    private GameObject stockAudioListener;

    void Start()
    {
        stockAudioListener = GameObject.Find("" + cameraMainName);
        stockAudioListener.GetComponent<AudioListener>().enabled = false;
        gameObject.AddComponent<AudioListener>();
        carCamera = stockAudioListener.GetComponent<CarCameras>();
    }
	
	void Update ()
    {
        if (carCamera.mycamera == CarCameras.Cameras.FixedTo) // interior camera
        {
            if (carCamera.mouseOrbitFixedCamera)
            {
                interior.SetActive(true);
                exterior.SetActive(false);
            }
            else
            {
                interior.SetActive(false);
                exterior.SetActive(true);
            }
        }
        else // exterior camera
        {
            interior.SetActive(false);
            exterior.SetActive(true);
        }
	}
}
#endif
