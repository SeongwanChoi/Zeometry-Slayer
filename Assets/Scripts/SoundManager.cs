using UnityEngine;

/// <summary>
/// 게임 전체의 효과음(SFX) 재생을 총괄하는 매니저 (BGM 제외, 순수 SFX 전용):
/// - UI 클릭, 유닛 선택, 손패 카드 선택/드래그, AttackBox 카드 장착/해제
/// - 플레이어 스킬 버프(공격력 업 / 방어력 업 / 크리티컬 위력 업) 사용 및 사용 불가(코스트 부족/쿨타임) 알림음
/// - 턴 전환 배너 슬라이드, 스타 획득/분배, 플레이어 공격 오라 차징
/// - 카드 속성별(Buster / Arts / Quick) 타격음, 크리티컬 히트 추가 폭발음, 적 공격 피격음
/// - 유닛 사망 폭발음, 승리/튜토리얼 완료 및 패배 결과창 효과음
/// </summary>
public class SoundManager : Singleton<SoundManager>
{
    [Header("전역 효과음(SFX) 볼륨 설정")]
    [Range(0f, 1f)]
    [SerializeField] private float masterSfxVolume = 0.85f;
    [SerializeField] private bool isMuted = false;

    private const string PrefKeySfxVolume = "ZS_MasterSfxVolume";
    private const string PrefKeySfxMuted = "ZS_MasterSfxMuted";

    public float MasterSfxVolume => masterSfxVolume;
    public bool IsMuted => isMuted;
    public float EffectiveVolume => isMuted ? 0f : masterSfxVolume;

    [Header("1. UI 및 조작 효과음 (Mechanics)")]
    [Tooltip("일반 UI 버튼 클릭 (일시정지 메뉴, 튜토리얼 다음 버튼, 결과창 버튼 등)")]
    [SerializeField] private AudioClip uiClickClip;

    [Tooltip("아군(Player) 또는 적(Enemy) 유닛 클릭 선택 효과음")]
    [SerializeField] private AudioClip unitSelectClip;

    [Tooltip("손패의 카드를 클릭하거나 드래그하기 시작할 때 효과음")]
    [SerializeField] private AudioClip cardPickupClip;

    [Tooltip("카드를 AttackBox에 장착(등록)할 때 효과음")]
    [SerializeField] private AudioClip cardEquipClip;

    [Tooltip("턴 시작 배너(PLAYER TURN / ATTACK / ENEMY TURN) 전환 효과음")]
    [SerializeField] private AudioClip turnTransitionClip;

    [Tooltip("코스트 부족 또는 쿨타임 중일 때 스킬 버튼 클릭 실패 효과음")]
    [SerializeField] private AudioClip skillBlockedClip;

    [Header("2. 플레이어 스킬 버프 및 차징 효과음 (Skill / Bonus)")]
    [Tooltip("공격력 업(ATTACK_UP) 스킬 사용 시 효과음")]
    [SerializeField] private AudioClip skillAttackUpClip;

    [Tooltip("방어력 업(DEFENSE_UP) 스킬 사용 시 효과음")]
    [SerializeField] private AudioClip skillDefenseUpClip;

    [Tooltip("크리티컬 위력 업(CRIT_DAMAGE_UP) 스킬 사용 시 효과음")]
    [SerializeField] private AudioClip skillCritUpClip;

    [Tooltip("플레이어 공격 직전 후면 오라 차징 효과음")]
    [SerializeField] private AudioClip playerAuraClip;

    [Header("3. 전투 타격 및 피격 효과음 (Shoot)")]
    [Tooltip("Buster(Red) 카드 공격 적중 효과음")]
    [SerializeField] private AudioClip attackBusterClip;

    [Tooltip("Arts(Blue) 카드 공격 적중 효과음")]
    [SerializeField] private AudioClip attackArtsClip;

    [Tooltip("Quick(Green) 카드 공격 적중 효과음")]
    [SerializeField] private AudioClip attackQuickClip;

    [Tooltip("크리티컬 히트 발동 시 추가 타격 효과음")]
    [SerializeField] private AudioClip criticalHitClip;

    [Tooltip("적(Enemy) 공격 시 플레이어 피격 효과음")]
    [SerializeField] private AudioClip enemyAttackHitClip;

    [Tooltip("스타 획득 또는 카드에 스타 분배 시 효과음")]
    [SerializeField] private AudioClip starClip;

    [Header("4. 유닛 사망 및 전투 결과 효과음")]
    [Tooltip("유닛 체력이 0이 되어 사망할 때 효과음")]
    [SerializeField] private AudioClip unitDeathClip;

    [Tooltip("승리 / 튜토리얼 완료 UI 표시 효과음")]
    [SerializeField] private AudioClip victoryClip;

    [Tooltip("패배 UI 표시 효과음")]
    [SerializeField] private AudioClip defeatClip;

    private AudioSource sfxAudioSource;

    protected override bool KeepAliveAcrossScenes => true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitializeOnSceneLoad()
    {
        _ = Ins;
    }

    public override void Init()
    {
        base.Init();
        LoadSavedVolumeSettings();
        EnsureAudioSource();
        LoadDefaultClipsIfNeeded();
    }

    private void LoadSavedVolumeSettings()
    {
        if (PlayerPrefs.HasKey(PrefKeySfxVolume))
        {
            masterSfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeySfxVolume, 0.85f));
        }

        if (PlayerPrefs.HasKey(PrefKeySfxMuted))
        {
            isMuted = PlayerPrefs.GetInt(PrefKeySfxMuted, 0) == 1;
        }
    }

    public void SetMasterSfxVolume(float volume, bool playPreviewClick = false)
    {
        masterSfxVolume = Mathf.Clamp01(volume);
        if (masterSfxVolume > 0.001f && isMuted)
        {
            isMuted = false;
            PlayerPrefs.SetInt(PrefKeySfxMuted, 0);
        }

        PlayerPrefs.SetFloat(PrefKeySfxVolume, masterSfxVolume);
        PlayerPrefs.Save();

        if (playPreviewClick && !isMuted && masterSfxVolume > 0.001f)
        {
            PlayUIClick();
        }
    }

    public void SetMuted(bool muted)
    {
        isMuted = muted;
        PlayerPrefs.SetInt(PrefKeySfxMuted, isMuted ? 1 : 0);
        PlayerPrefs.Save();

        if (!isMuted && masterSfxVolume > 0.001f)
        {
            PlayUIClick();
        }
    }

    public void ToggleMute()
    {
        SetMuted(!isMuted);
    }

    private void OnEnable()
    {
        EnsureAudioSource();
        LoadDefaultClipsIfNeeded();

        SelectableUnit.OnPlayerSkillUsedEvent -= HandlePlayerSkillUsed;
        SelectableUnit.OnPlayerSkillUsedEvent += HandlePlayerSkillUsed;

        AttackBoxManager.OnCardAddedToAttackBoxEvent -= HandleCardAddedToAttackBox;
        AttackBoxManager.OnCardAddedToAttackBoxEvent += HandleCardAddedToAttackBox;
    }

    private void OnDisable()
    {
        SelectableUnit.OnPlayerSkillUsedEvent -= HandlePlayerSkillUsed;
        AttackBoxManager.OnCardAddedToAttackBoxEvent -= HandleCardAddedToAttackBox;
    }

    private void EnsureAudioSource()
    {
        if (sfxAudioSource == null)
        {
            sfxAudioSource = GetComponent<AudioSource>();
            if (sfxAudioSource == null)
            {
                sfxAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        sfxAudioSource.playOnAwake = false;
        sfxAudioSource.loop = false;
        sfxAudioSource.spatialBlend = 0f; // 2D 사운드로 고정하여 카메라 거리와 무관하게 균일한 음량 재생
    }

    /// <summary>
    /// 에디터 또는 런타임 초기화 시 미할당된 AudioClip 필드를 기본 사운드 에셋으로 자동 연결합니다.
    /// </summary>
    public void LoadDefaultClipsIfNeeded()
    {
#if UNITY_EDITOR
        const string root = "Assets/Sound/Sci-Fi Weapons-Bullet Hell Sound Effects Pack/AUDIO/";

        if (uiClickClip == null)
            uiClickClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(root + "Mechanics/SFX_GUNS_MECH_SCIFI_Analogue_Set_Part_1.wav");

        if (unitSelectClip == null)
            unitSelectClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(root + "Mechanics/SFX_GUNS_MECH_SCIFI_Analogue_Set_Part_2.wav");

        if (cardPickupClip == null)
            cardPickupClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(root + "Mechanics/SFX_GUNS_MECH_SCIFI_Analogue_Reload_Magazine_Out_1.wav");

        if (cardEquipClip == null)
            cardEquipClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(root + "Mechanics/SFX_GUNS_MECH_SCIFI_Analogue_Reload_Magazine_In_1.wav");

        if (turnTransitionClip == null)
            turnTransitionClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(root + "Mechanics/SFX_GUNS_MECH_SCIFI_HiTech_Gun2_Set_to_Charge.wav");

        if (skillBlockedClip == null)
            skillBlockedClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(root + "Mechanics/SFX_GUNS_MECH_SCIFI_HiTech_Gun_Out_of_Ammo.wav");

        if (skillAttackUpClip == null)
            skillAttackUpClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(root + "Z_Bonus/SFX_SCIFI_ENERGY_Anime_Weapon_Charge_Start_1.wav");

        if (skillDefenseUpClip == null)
            skillDefenseUpClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(root + "Skill/SFX_SCIFI_ENERGY_Skill_Whoosh_Electrified_1.wav");

        if (skillCritUpClip == null)
            skillCritUpClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(root + "Skill/SFX_SCIFI_SKILL_Laser_Electric_Whoosh_1.wav");

        if (playerAuraClip == null)
            playerAuraClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(root + "Z_Bonus/SFX_SCIFI_ENERGY_Anime_Weapon_Charge_End_1.wav");

        if (attackBusterClip == null)
            attackBusterClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(root + "Shoot/Cannon/SFX_SCIFI_WEAPON_Cannon_Shoot1_Electrified_1.wav");

        if (attackArtsClip == null)
            attackArtsClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(root + "Shoot/Energy/SFX_SCIFI_WEAPON_Energy_Shoot_Big_Gun1_1.wav");

        if (attackQuickClip == null)
            attackQuickClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(root + "Shoot/Laser/SFX_LASER_SCIFI_Shoot_Small_Weapon4_1.wav");

        if (criticalHitClip == null)
            criticalHitClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(root + "Shoot/Electric/SFX_SCIFI_WEAPON_Electric_Shotgun_Shoot_Big_1.wav");

        if (enemyAttackHitClip == null)
            enemyAttackHitClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(root + "Shoot/Cannon/SFX_SCIFI_WEAPON_Cannon_Shoot4_1.wav");

        if (starClip == null)
            starClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(root + "Shoot/Arcade/SFX_LASER_SCIFI_Arcade_Shoot_Small_Weapon1_1.wav");

        if (unitDeathClip == null)
            unitDeathClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(root + "Shoot/Arcade/SFX_ARCADE_Retro_Explosion_Big_1.wav");

        if (victoryClip == null)
            victoryClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(root + "Z_Bonus/SFX_SCIFI_ENERGY_Anime_Weapon_Charge_End_2.wav");

        if (defeatClip == null)
            defeatClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(root + "Shoot/Arcade/SFX_ARCADE_Retro_Explosion_Big_2.wav");
#endif
    }

    // =========================================================================
    // 이벤트 핸들러
    // =========================================================================

    private void HandlePlayerSkillUsed(SelectableUnit unit, ESkillType skillType)
    {
        PlaySkillBuff(skillType);
    }

    private void HandleCardAddedToAttackBox(Card card)
    {
        PlayCardEquip();
    }

    // =========================================================================
    // 개별 효과음 재생 API
    // =========================================================================

    public void PlayOneShot(AudioClip clip, float volumeMultiplier = 1f)
    {
        if (clip == null || isMuted) return;
        EnsureAudioSource();
        if (sfxAudioSource == null) return;

        float finalVol = Mathf.Clamp01(masterSfxVolume * volumeMultiplier);
        if (finalVol > 0f)
        {
            sfxAudioSource.PlayOneShot(clip, finalVol);
        }
    }

    public void PlayUIClick()
    {
        LoadDefaultClipsIfNeeded();
        PlayOneShot(uiClickClip, 0.85f);
    }

    public void PlayUnitSelect()
    {
        LoadDefaultClipsIfNeeded();
        PlayOneShot(unitSelectClip != null ? unitSelectClip : uiClickClip, 0.85f);
    }

    public void PlayCardPickup()
    {
        LoadDefaultClipsIfNeeded();
        PlayOneShot(cardPickupClip, 0.8f);
    }

    public void PlayCardEquip()
    {
        LoadDefaultClipsIfNeeded();
        PlayOneShot(cardEquipClip, 0.9f);
    }

    public void PlayTurnTransition()
    {
        LoadDefaultClipsIfNeeded();
        PlayOneShot(turnTransitionClip, 0.75f);
    }

    public void PlaySkillBlocked()
    {
        LoadDefaultClipsIfNeeded();
        PlayOneShot(skillBlockedClip, 0.85f);
    }

    public void PlaySkillBuff(ESkillType skillType)
    {
        LoadDefaultClipsIfNeeded();
        switch (skillType)
        {
            case ESkillType.ATTACK_UP:
                PlayOneShot(skillAttackUpClip, 0.85f);
                break;
            case ESkillType.DEFENSE_UP:
                PlayOneShot(skillDefenseUpClip, 0.85f);
                break;
            case ESkillType.CRIT_DAMAGE_UP:
                PlayOneShot(skillCritUpClip, 0.85f);
                break;
        }
    }

    public void PlayPlayerAura()
    {
        LoadDefaultClipsIfNeeded();
        PlayOneShot(playerAuraClip, 0.7f);
    }

    public void PlayCardAttackHit(ECardColor cardColor)
    {
        LoadDefaultClipsIfNeeded();
        switch (cardColor)
        {
            case ECardColor.RED:
                PlayOneShot(attackBusterClip, 0.95f);
                break;
            case ECardColor.BLUE:
                PlayOneShot(attackArtsClip, 0.9f);
                break;
            case ECardColor.GREEN:
                PlayOneShot(attackQuickClip, 0.9f);
                break;
            default:
                PlayOneShot(attackArtsClip, 0.9f);
                break;
        }
    }

    public void PlayCriticalHit()
    {
        LoadDefaultClipsIfNeeded();
        PlayOneShot(criticalHitClip, 1.0f);
    }

    public void PlayEnemyAttackHit()
    {
        LoadDefaultClipsIfNeeded();
        PlayOneShot(enemyAttackHitClip, 0.95f);
    }

    public void PlayStarEffect()
    {
        LoadDefaultClipsIfNeeded();
        PlayOneShot(starClip, 0.75f);
    }

    public void PlayUnitDeath()
    {
        LoadDefaultClipsIfNeeded();
        PlayOneShot(unitDeathClip, 0.95f);
    }

    public void PlayVictory()
    {
        LoadDefaultClipsIfNeeded();
        PlayOneShot(victoryClip, 0.95f);
    }

    public void PlayDefeat()
    {
        LoadDefaultClipsIfNeeded();
        PlayOneShot(defeatClip, 0.95f);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        LoadDefaultClipsIfNeeded();
    }
#endif
}
