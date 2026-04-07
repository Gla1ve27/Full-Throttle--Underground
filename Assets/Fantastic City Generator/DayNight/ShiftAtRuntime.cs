using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShiftAtRuntime : MonoBehaviour
{
    DayNight dayNight;

    private void Start()
    {

        dayNight = Object.FindFirstObjectByType<DayNight>();

    }

    private void Update()
    {


        if (Input.GetKeyDown(KeyCode.N))
        {
            if (dayNight)
            {
                dayNight.isNight = !dayNight.isNight;
                dayNight.ChangeMaterial();

            }
        }

    }

}
