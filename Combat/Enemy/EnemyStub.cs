using UnityEngine;

public class EnemyStub : Agent
{
    protected override void Die()
    {
        base.Die();
        Debug.Log("EnemyStub has died.");
    }
}