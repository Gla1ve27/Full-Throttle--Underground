using UnityEngine;
using UnityEngine.UI;

public class TimeOfDay : MonoBehaviour
{
	public const float seconds_in_day = 86400f;

	public Text display_time;

	public float time_scale = 1;
	public float seconds_passed = 0;

	int seconds = 0;
	int minutes = 0;
	int hours = 0;
    
	/// <summary>
	/// Set new time of day
	/// </summary>
	/// <param name="hrs">Hours 0 - 23</param>
	/// <param name="mins">Minutes 0 - 59</param>
	/// <param name="sec"> Seconds 0 - 59</param>
	public void SetTimeOfDay(float hrs, float mins, float sec) {
		hrs = EvaluateMinMaxTime(hrs, 0, 23);
		mins = EvaluateMinMaxTime(mins, 0, 59);
		sec = EvaluateMinMaxTime(sec, 0, 59);

		float new_seconds_passed = sec + mins * 60 + hrs * 60 * 60;
		seconds_passed = new_seconds_passed;
	}
    void Update()
    {
		if(display_time != null) {
			SetClockDisplay();
		}
    }
	void LateUpdate() {

		seconds_passed += Time.deltaTime * time_scale;

		if (seconds_passed > seconds_in_day) {
			seconds_passed = 0;
		}

		hours = (int)seconds_passed / 60 / 60;
		minutes = (int)(seconds_passed / 60) % 60;
		seconds = (int)seconds_passed % 60;
	}
	void SetClockDisplay() {
		display_time.text = "";

		if (hours.ToString().Length == 1) {
			display_time.text += "0" + hours.ToString() + ":";
		} else {
			display_time.text += hours.ToString() + ":";
		}

		if (minutes.ToString().Length == 1) {
			display_time.text += "0" + minutes.ToString() + ":";
		} else {
			display_time.text += minutes.ToString() + ":";
		}

		if (seconds.ToString().Length == 1) {
			display_time.text += "0" + seconds.ToString();
		} else {
			display_time.text += seconds.ToString();
		}
	}
	/// <summary>
	/// Sanitaze new time input
	/// </summary>
	/// <param name="t"></param>
	/// <param name="min"></param>
	/// <param name="max"></param>
	/// <returns></returns>
	float EvaluateMinMaxTime(float t, float min, float max) {
		if(t > max) {
			t = max;
		} else if(t < min) {
			t = min;
		}
		return t;
	}
}