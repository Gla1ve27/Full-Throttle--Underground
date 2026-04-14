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
public class UCP_RES : MonoBehaviour {

    RealisticEngineSound res;
    Drivetrain drivetrain;
    SoundController soundCtrlr;
    private AudioClip none;
	void Start ()
    {
        res = gameObject.GetComponent<RealisticEngineSound>();
        drivetrain = gameObject.transform.root.GetComponent<Drivetrain>();
        soundCtrlr = gameObject.transform.root.GetComponent<SoundController>();

        // max rpm
        res.maxRPMLimit = drivetrain.maxRPM;
        res.carMaxSpeed = 250;
        //disable stock engine sounds
        soundCtrlr.engineThrottle = none;
        soundCtrlr.engineNoThrottle = none;
	}
	void Update ()
    {
        // engine current rpm
        res.engineCurrentRPM = drivetrain.rpm;
        res.carCurrentSpeed = drivetrain.velo * 3.6f; // km/h
        // is gas pedal pressed
        if (drivetrain.throttle > 0.4f)
            res.gasPedalPressing = true;
        else
            res.gasPedalPressing = false;

        // is shifting
        if (drivetrain.changingGear)
            res.isShifting = true;
        else
            res.isShifting = false;
	}
}
#endif
