using DG.Tweening;
using System.Collections;
using TMPro;
using UnityEngine;

public class StreakUI : MonoBehaviour
{
    #region References

    [SerializeField] private GameObject iconPrefab;
    [SerializeField] private GameObject rowPrefab;
    [SerializeField] private Transform rowContainer;
    [SerializeField] private Vector2 spawnOrigin;

    [Header("Combo Name")]
    [SerializeField] private TextMeshProUGUI comboNameLabel;
    [SerializeField] private float nameHoldDuration = 0.8f;
    [SerializeField] private TMPDissolveDriver comboNameDissolve;

    #endregion

    #region State

    private StreakRowUI activeRow;
    private StreakRowUI previousRow;
    private Coroutine _holdRoutine;
    public StreakRowUI ActiveRow => activeRow;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        CombatEvents.OnAttackStartedWithIcon += OnAttackStarted;
        CombatEvents.OnPlayerHitTarget       += OnHitTarget;
        CombatEvents.OnComboCompleted        += OnComboCompleted;
        CombatEvents.OnComboFailed           += OnComboFailed;
    }

    private void OnDisable()
    {
        CombatEvents.OnAttackStartedWithIcon -= OnAttackStarted;
        CombatEvents.OnPlayerHitTarget       -= OnHitTarget;
        CombatEvents.OnComboCompleted        -= OnComboCompleted;
        CombatEvents.OnComboFailed           -= OnComboFailed;
    }

    #endregion

    #region Event Handlers

    private void OnAttackStarted(AttackData data, bool isChained)
    {
        if (data.icon == null) return;

        if (data.isDodgeAttack)
            Debug.Log($"[StreakUI] Dodge attack incoming: '{data.attackName}' — isDodgeAttack=true"); // <== REMOVE LATER

        if (activeRow == null || !activeRow || activeRow.IsLocked || activeRow.IsFull)
            activeRow = SpawnRow();

        GameObject iconGO = Instantiate(iconPrefab, activeRow.transform);
        StreakIconUI icon  = iconGO.GetComponent<StreakIconUI>();
        icon.SetIcon(data.icon);
        icon.FlyToSlot(spawnOrigin);
        activeRow.AddIcon(icon);

        if (data.isDodgeAttack)
        {
            activeRow.OnComboComplete(data.attackName);
            ShowComboName(data.attackName);
            previousRow = activeRow;
            activeRow   = null;
            return;
        }

        if (activeRow.IsFull)
        {
            CombatEvents.RaiseStreakRowFull();
            activeRow = null;
        }
    }

    private void OnHitTarget(AttackType type, float damage, GameObject target, Vector3 hitPos, Vector3 dir) { }

    private void OnComboCompleted(string comboName)
    {
        if (activeRow == null) return;
        StartCoroutine(CompleteRowNextFrame(activeRow, comboName));
    }

    private IEnumerator CompleteRowNextFrame(StreakRowUI row, string comboName)
    {
        yield return null;
        if (row == null) yield break;
        row.OnComboComplete(comboName);
        ShowComboName(comboName);
        previousRow = row;
        activeRow   = null;
    }

    private void ShowComboName(string name)
    {
        if (comboNameLabel == null) return;
        comboNameLabel.text = name;
        comboNameLabel.transform.localScale = Vector3.one;
        if (_holdRoutine != null) StopCoroutine(_holdRoutine);
        _holdRoutine = StartCoroutine(HoldThenDematerialize());
    }

    private IEnumerator HoldThenDematerialize()
    {
        comboNameDissolve.Materialize();
        yield return new WaitForSeconds(comboNameDissolve.DissolveDuration + nameHoldDuration);
        comboNameDissolve.Dematerialize();
        _holdRoutine = null;
    }

    private void OnComboFailed()
    {
        if (activeRow == null) return;
        activeRow.OnComboFail();
        activeRow   = null;
        previousRow = null;
    }

    #endregion

    #region Helpers

    private StreakRowUI SpawnRow()
    {
        if (previousRow != null)
            previousRow.FloatAway();

        previousRow = activeRow;

        GameObject rowGO = Instantiate(rowPrefab, rowContainer);
        return rowGO.GetComponent<StreakRowUI>();
    }

    #endregion
}
