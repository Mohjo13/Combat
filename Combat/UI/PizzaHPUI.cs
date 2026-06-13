using UnityEngine;
using UnityEngine.UI;

public class PizzaHealthUI : MonoBehaviour
{
    [SerializeField] private Image[] pizzaSlices; // Size is 8

    public void UpdateHealth(int currentHP)
    {
        currentHP = Mathf.Clamp(currentHP, 0, pizzaSlices.Length);

        for (int i = 0; i < pizzaSlices.Length; i++)
        {
            pizzaSlices[i].enabled = i < currentHP;
        }
    }
}