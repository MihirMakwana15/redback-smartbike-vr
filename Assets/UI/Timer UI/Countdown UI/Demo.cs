using UnityEngine;

public class Demo : MonoBehaviour
{
    [SerializeField] private CountdownTimerUI timer1;
    [SerializeField] private int timerDuration = 15;
    [SerializeField] private float startDelay = 3f;

    private void Start()
    {
        if (timer1 == null)
        {
            timer1 = FindObjectOfType<CountdownTimerUI>();
        }

        if (timer1 != null)
        {
            timer1.SetDuration(timerDuration)
                  .BeginWithDelay(startDelay);
        }
        else
        {
            Debug.LogError("CountdownTimerUI component was not found.");
        }
    }
}