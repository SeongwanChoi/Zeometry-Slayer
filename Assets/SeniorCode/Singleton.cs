using UnityEngine;

/// <summary>
/// 제네릭 MonoBehaviour 싱글톤 베이스 클래스:
/// - KeepAliveAcrossScenes가 false(기본값)이면 씬 종속 싱글톤으로 동작하여 씬 전환 시 새 인스턴스로 깔끔하게 교체됩니다.
/// - KeepAliveAcrossScenes가 true(예: SoundManager)이면 DontDestroyOnLoad를 통해 씬 전환 간에도 인스턴스를 유지합니다.
/// </summary>
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;

    public static T Ins
    {
        get
        {
            if (instance == null)
            {
                // 오브젝트 이름이 클래스 이름과 다르더라도(예: Canvas 안의 PlayerHand, AttackBox)
                // 씬에 이미 존재하는 T 컴포넌트를 먼저 찾아서 그대로 활용합니다.
                instance = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

                if (instance == null)
                {
                    GameObject obj = GameObject.Find(typeof(T).Name);
                    if (obj == null)
                    {
                        obj = new GameObject(typeof(T).Name);
                        instance = obj.AddComponent<T>();
                    }
                    else
                    {
                        instance = obj.GetComponent<T>();
                    }
                }
            }

            return instance;
        }
    }

    /// <summary>
    /// 씬 전환 시에도 파괴되지 않고 유지할지 여부 (기본값 false: 씬별 고유 매니저는 씬 전환 시 새로 교체됨)
    /// </summary>
    protected virtual bool KeepAliveAcrossScenes => false;

    protected virtual void Awake()
    {
        if (instance != null && instance != this)
        {
            if (KeepAliveAcrossScenes)
            {
                Destroy(gameObject);
                return;
            }

            // 씬 종속 싱글톤인 경우 이전 씬에서 남아있던 인스턴스를 정리하고 현재 씬의 새 인스턴스로 교체
            Destroy(instance.gameObject);
        }

        instance = this as T;
        // KeepAliveAcrossScenes가 활성화된 루트 오브젝트일 때만 DontDestroyOnLoad 호출
        if (KeepAliveAcrossScenes && transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }
        Init();
    }

    protected virtual void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public virtual void Init() { } // 호출하면 DontDestroy목록으로 생길테니까
}
