using UnityEngine;

public enum ETurn
{
    PLAYER = 1,
    ENEMY = 2,
    ATTACK = 3
}

/// <summary>
/// 카드 종류 (색상 및 정식 명칭):
/// - RED          = Buster       (2코스트, 공격 시 데미지 * 2)
/// - BLUE         = Arts         (1코스트, 공격 시 데미지 * 1, 카드 1장 드로우 + 다음 턴 코스트 +1)
/// - GREEN        = Quick        (1코스트, 공격 시 데미지 * 1, 스타 생성)
/// - SUPER_BUSTER = Super Buster (2코스트, Player_3 3스킬로 생성, 손패의 Buster 카드를 흡수해 공격력 누적)
/// </summary>
public enum ECardColor
{
    RED = 0,
    BLUE = 1,
    GREEN = 2,
    SUPER_BUSTER = 3,

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
            case ECardColor.RED:          return "Buster";
            case ECardColor.BLUE:         return "Arts";
            case ECardColor.GREEN:        return "Quick";
            case ECardColor.SUPER_BUSTER: return "Super Buster";
            default:                      return "Arts";
        }
    }

    public static int GetBaseCost(this ECardColor cardColor)
    {
        switch (cardColor)
        {
            case ECardColor.RED:          return 2;
            case ECardColor.SUPER_BUSTER: return 2;
            case ECardColor.BLUE:         return 1;
            case ECardColor.GREEN:        return 1;
            default:                      return 1;
        }
    }

    public static bool IsBusterFamily(this ECardColor cardColor)
    {
        return cardColor == ECardColor.RED || cardColor == ECardColor.SUPER_BUSTER;
    }
}

/// <summary>
/// 플레이어 스킬 슬롯 종류 (왼쪽부터 배치 순서: 1스킬, 2스킬, 3스킬)
/// - 각 플레이어(Player_1, Player_2, Player_3)마다 고유한 효과를 가집니다.
/// - 기존 코드 호환성을 위해 ATTACK_UP / DEFENSE_UP / CRIT_DAMAGE_UP 별칭을 유지합니다.
/// </summary>
public enum ESkillType
{
    SKILL_1 = 0,
    SKILL_2 = 1,
    SKILL_3 = 2,

    ATTACK_UP = SKILL_1,
    DEFENSE_UP = SKILL_2,
    CRIT_DAMAGE_UP = SKILL_3
}
