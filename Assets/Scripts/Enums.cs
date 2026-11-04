using UnityEngine;

public enum ETurn
{
    PLAYER = 1,
    ENEMY = 2,
    ATTACK = 3
}

/// <summary>
/// 카드 종류 (색상 및 정식 명칭):
/// - RED   = Buster (공격 시 데미지 * 1.5)
/// - BLUE  = Arts   (공격 시 데미지 * 1.0, 코스트 +1 획득)
/// - GREEN = Quick  (공격 시 데미지 * 1.0, 스타 1~10 랜덤 획득)
/// </summary>
public enum ECardColor
{
    RED = 0,
    BLUE = 1,
    GREEN = 2,

    BUSTER = RED,
    ARTS = BLUE,
    QUICK = GREEN
}

public static class CardTypeExtensions
{
    public static string GetCardName(this ECardColor cardColor)
    {
        switch (cardColor)
        {
            case ECardColor.RED:   return "Buster";
            case ECardColor.BLUE:  return "Arts";
            case ECardColor.GREEN: return "Quick";
            default:               return "Arts";
        }
    }
}
