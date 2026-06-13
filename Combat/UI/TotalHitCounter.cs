using TMPro;
using UnityEngine;

public class TotalHitCounter : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI totalHitsText;
    [SerializeField] private float comboResetDelay = 1f;

    private float totalHits;
    private float resetTimer;
    private bool timerRunning;

    private void Start()
    {
        UpdateDisplay();
    }
    private void Update()
    {
        if (!timerRunning) return;

        resetTimer -= Time.deltaTime;

        if (resetTimer <= 0f)
        {
            ResetCounter();
        }
    }
    public void AddHit(float amount)
    {
        totalHits += amount;

        resetTimer = comboResetDelay;
        timerRunning = true;

        UpdateDisplay();
    }

    public void ResetCounter()
    {
        Debug.Log($"");
        totalHits = 0;
        resetTimer = 0f;
        timerRunning = false;

        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (totalHitsText == null) return;

        if (totalHits <= 0)
        {
            totalHitsText.text = "";
        }
        else
        {
            totalHitsText.text = $"COMBO x{totalHits}";
        }
    }
}
