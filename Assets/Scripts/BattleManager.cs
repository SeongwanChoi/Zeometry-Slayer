using UnityEngine;

public class BattleManager : Singleton<BattleManager>
{
    private ETurn currunt_Turn;

    private void Start()
    {
        currunt_Turn = ETurn.PLAYER;
    }

    private void Update()
    {
        if (currunt_Turn == ETurn.PLAYER)
            PTurn();
        else
            EnTurn();
    }

    public void TurnEnd()
    {
        // 플레이어 턴 종료 시 Attack Box에 반드시 6장이 들어와 있는지 체크
        if (currunt_Turn == ETurn.PLAYER && AttackBoxManager.Ins != null)
        {
            if (!AttackBoxManager.Ins.IsReadyToExecute)
            {
                Debug.LogWarning($"[BattleManager] Attack Box에 카드가 반드시 {AttackBoxManager.Ins.RequiredCardCount}장 들어와야 턴을 종료할 수 있습니다! (현재 {AttackBoxManager.Ins.CurrentCardCount}장)");
                return;
            }
        }

        currunt_Turn = 3 - currunt_Turn;
    }

    private void PTurn()
    {
        Debug.Log("플레이어 행동");
    }

    private void EnTurn()
    {
        Debug.Log("적 행동");
    }
}
